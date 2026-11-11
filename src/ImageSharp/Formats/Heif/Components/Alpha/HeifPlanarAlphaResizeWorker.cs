// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Formats.Heif.Components.Alpha;

/// <summary>
/// Resizes a native HEIF luma plane and composes the result as alpha using a bounded sliding window.
/// </summary>
/// <typeparam name="TBuffer">The codec adapter exposing the reconstructed component planes.</typeparam>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
/// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
internal sealed class HeifPlanarAlphaResizeWorker<TBuffer, TSample, TLoader> : HeifAlphaRowSource
    where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
    where TSample : unmanaged
    where TLoader : struct, IHeifSampleConverter<TSample>
{
    /// <summary>
    /// The configuration used for pooled allocation and pixel conversion.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The codec-native component planes.
    /// </summary>
    private readonly TBuffer buffer;

    /// <summary>
    /// The resolved H.273 component-range parameters.
    /// </summary>
    private readonly HeifColorConversionParameters parameters;

    /// <summary>
    /// The visible luma rectangle within the reconstructed plane.
    /// </summary>
    private readonly Rectangle sourceRectangle;

    /// <summary>
    /// The destination region receiving the resized alpha plane.
    /// </summary>
    private readonly Rectangle destinationRectangle;

    /// <summary>
    /// The horizontal box-filter kernels for the full presented width.
    /// </summary>
    private readonly ResizeKernelMap horizontalKernels;

    /// <summary>
    /// The vertical box-filter kernels for the full presented height.
    /// </summary>
    private readonly ResizeKernelMap verticalKernels;

    /// <summary>
    /// The horizontally filtered rows retained by the sliding window. Row Y holds the first-pass pixels of source row
    /// <c>currentWindow.Min + Y</c>, one pixel for each destination X.
    /// </summary>
    private readonly IMemoryOwner<Vector4> firstPassBuffer;

    /// <summary>
    /// The reusable normalized source or resized destination row.
    /// </summary>
    private readonly IMemoryOwner<float> componentOwner;

    /// <summary>
    /// The two replicated source rows that the shared resize kernels convolve at the same time.
    /// </summary>
    /// <remarks>
    /// The buffer holds floats, not vectors, so that the rows can start at any multiple of 4 bytes. Each row starts on
    /// a 64-byte boundary in the buffer, and is followed by the zero padding of the horizontal kernels.
    /// See <see cref="CalculateFirstPassValues"/>.
    /// </remarks>
    private readonly IMemoryOwner<float> sourceRowsOwner;

    /// <summary>
    /// The distance in floats from the first source row in <see cref="sourceRowsOwner"/> to the second. This is a
    /// multiple of 16 floats (64 bytes), so that both rows start on a 64-byte boundary.
    /// </summary>
    private readonly int sourceRowStride;

    /// <summary>
    /// The reusable row of vertically filtered pixels. The alpha is the first component of each pixel.
    /// </summary>
    private readonly IMemoryOwner<Vector4> resultVectorOwner;

    /// <summary>
    /// The reusable 16-bit source and destination alpha packing row.
    /// </summary>
    private readonly IMemoryOwner<L16> alphaOwner;

    /// <summary>
    /// The number of source rows retained when the window advances.
    /// </summary>
    private readonly int windowBandHeight;

    /// <summary>
    /// The total number of source rows retained by the bounded working window.
    /// </summary>
    private readonly int workerHeight;

    /// <summary>
    /// The source-row interval that the first-pass buffer holds.
    /// </summary>
    private RowInterval currentWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifPlanarAlphaResizeWorker{TBuffer, TSample, TLoader}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used for pooled allocation and pixel conversion.</param>
    /// <param name="buffer">The codec-native component planes.</param>
    /// <param name="parameters">The resolved H.273 component-range parameters.</param>
    /// <param name="sourceRectangle">The visible luma rectangle within the reconstructed plane.</param>
    /// <param name="destinationRectangle">The destination region receiving the top-left portion of the presented alpha plane.</param>
    /// <param name="outputSize">The complete presentation extent before cropping.</param>
    public HeifPlanarAlphaResizeWorker(
        Configuration configuration,
        TBuffer buffer,
        in HeifColorConversionParameters parameters,
        Rectangle sourceRectangle,
        Rectangle destinationRectangle,
        Size outputSize)
    {
        this.configuration = configuration;
        this.buffer = buffer;
        this.parameters = parameters;
        this.sourceRectangle = sourceRectangle;
        this.destinationRectangle = destinationRectangle;

        // As in ResizeProcessor, the horizontal kernels start at multiples of 4 pixels, so that the 4-pixel loads of the
        // horizontal operators start on a cache line. The vertical kernels keep their exact starts.
        BoxResampler resampler = default;
        this.horizontalKernels = ResizeKernelMap.Calculate(in resampler, outputSize.Width, sourceRectangle.Width, configuration.MemoryAllocator, true);
        this.verticalKernels = ResizeKernelMap.Calculate(in resampler, outputSize.Height, sourceRectangle.Height, configuration.MemoryAllocator, false);

        // Retaining one complete maximum-diameter band is sufficient for every vertical kernel that crosses a
        // window boundary. Those first-pass rows can be copied forward instead of normalized and filtered again.
        this.windowBandHeight = this.verticalKernels.MaxDiameter;

        // As in ResizeWorker, the first pass is stored as [window Y][destination X]. Bounding the source-Y dimension by
        // the configured working-buffer limit keeps memory independent of the complete alpha-plane size.
        int workingBufferLimitInBytes = Math.Min(
            configuration.WorkingBufferSizeHintInBytes,
            configuration.MemoryAllocator.GetBufferCapacityInBytes());

        int windowBandCount = ResizeHelper.CalculateResizeWorkerHeightInWindowBands(
            this.windowBandHeight,
            destinationRectangle.Width,
            workingBufferLimitInBytes);

        // The window holds a whole number of bands. Thus Slide keeps exactly one overlap band and fills the rest of the
        // window with rows that are new to the first pass.
        this.workerHeight = Math.Min(sourceRectangle.Height, windowBandCount * this.windowBandHeight);

        // The first pass writes every pixel of a window row before the vertical pass reads it, so the buffer needs no clear.
        this.firstPassBuffer = configuration.MemoryAllocator.Allocate<Vector4>(checked(this.workerHeight * destinationRectangle.Width));

        // The horizontal operators read PaddedLength taps. A kernel at the end of the row reads up to
        // PaddedLength - MaxDiameter pixels after the row, with the weight zero. These pixels must be finite. The clean
        // buffer gives them the value zero, and the conversion of a source row never writes them.
        //
        // Each row has the source pixels and then this padding. The stride rounds the row up to a multiple of
        // 4 pixels (64 bytes), so that the second row starts on a 64-byte boundary when the first row does.
        // The memory allocator does not align its buffers, so the buffer has 15 more floats. The rows start at the
        // first 64-byte boundary in the buffer, 0 to 15 floats after its start.
        int padding = this.horizontalKernels.PaddedLength - this.horizontalKernels.MaxDiameter;
        this.sourceRowStride = ((sourceRectangle.Width + padding + 3) & ~3) * 4;
        this.componentOwner = configuration.MemoryAllocator.Allocate<float>(Math.Max(sourceRectangle.Width, destinationRectangle.Width));
        this.sourceRowsOwner = configuration.MemoryAllocator.Allocate<float>((this.sourceRowStride * 2) + 15, AllocationOptions.Clean);
        this.resultVectorOwner = configuration.MemoryAllocator.Allocate<Vector4>(destinationRectangle.Width);
        this.alphaOwner = configuration.MemoryAllocator.Allocate<L16>(Math.Max(sourceRectangle.Width, destinationRectangle.Width));
        this.currentWindow = new RowInterval(0, this.workerHeight);
        this.CalculateFirstPassValues(this.currentWindow);
    }

    /// <summary>
    /// Releases all allocator-owned working buffers.
    /// </summary>
    public override void Dispose()
    {
        this.firstPassBuffer.Dispose();
        this.componentOwner.Dispose();
        this.sourceRowsOwner.Dispose();
        this.resultVectorOwner.Dispose();
        this.alphaOwner.Dispose();
        this.horizontalKernels.Dispose();
        this.verticalKernels.Dispose();
    }

    /// <summary>
    /// Resizes the next row of the requested destination rectangle.
    /// </summary>
    /// <param name="y">The next destination row, in increasing order starting at zero.</param>
    /// <returns>The normalized row, valid until the next row is read.</returns>
    public override Span<float> ReadRow(int y)
    {
        int width = this.destinationRectangle.Width;
        Span<float> resizedAlpha = this.componentOwner.GetSpan()[..width];
        Span<Vector4> results = this.resultVectorOwner.GetSpan()[..width];
        ReadOnlySpan<ResizeKernel> verticalKernelSpan = this.verticalKernels.GetKernelSpan();
        int currentWindowMax = this.currentWindow.Max;

        ref ResizeKernel kernel = ref Unsafe.Add(ref MemoryMarshal.GetReference(verticalKernelSpan), this.destinationRectangle.Y + y);
        int kernelEnd = kernel.StartIndex + kernel.Length;

        // Destination kernels advance monotonically through source Y. Slide until the complete kernel is in the cached
        // first-pass interval. The kept overlap band means that no shared source row is calculated twice.
        while (kernelEnd > currentWindowMax)
        {
            this.Slide();
            currentWindowMax = this.currentWindow.Max;
        }

        // The kernel reads its rows of the window. Neighboring destination pixels are neighbors in each row, and the rows
        // are width pixels apart. A pixel is 4 floats.
        Span<Vector4> firstPass = this.firstPassBuffer.GetSpan();
        ResizeKernel.ConvolveColumns(
            ref Unsafe.Add(ref this.verticalKernels.GetWeightsReference(), (uint)kernel.WeightsOffset),
            ref Unsafe.As<Vector4, float>(ref firstPass[(kernel.StartIndex - this.currentWindow.Min) * width]),
            (uint)width * 4,
            this.verticalKernels.MaxDiameter,
            width,
            ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(results)));

        // The source pixels replicate the alpha in each component, so the first component is the alpha.
        for (int x = 0; x < resizedAlpha.Length; x++)
        {
            resizedAlpha[x] = results[x].X;
        }

        return resizedAlpha;
    }

    /// <summary>
    /// Advances the bounded working window while preserving its overlapping source-row band.
    /// </summary>
    private void Slide()
    {
        // The old bottom band is the only set of first-pass rows that a future kernel can share with the new window.
        // Its height equals the largest vertical-kernel diameter, covering the maximum possible overlap.
        int minimumY = this.currentWindow.Max - this.windowBandHeight;
        int maximumY = Math.Min(minimumY + this.workerHeight, this.sourceRectangle.Height);

        // The rows are contiguous, so the bottom band is one block. A slide occurs only when the window holds at least
        // 2 bands, so the source and destination blocks do not overlap.
        int width = this.destinationRectangle.Width;
        Span<Vector4> firstPass = this.firstPassBuffer.GetSpan();
        firstPass.Slice((this.workerHeight - this.windowBandHeight) * width, this.windowBandHeight * width).CopyTo(firstPass);

        this.currentWindow = new RowInterval(minimumY, maximumY);

        // The retained band already contains normalized and horizontally filtered values. Only rows below it are new.
        this.CalculateFirstPassValues(this.currentWindow.Slice(this.windowBandHeight));
    }

    /// <summary>
    /// Normalizes and horizontally filters the source rows entering the current working window.
    /// </summary>
    /// <param name="interval">The source-row interval requiring first-pass values.</param>
    private void CalculateFirstPassValues(RowInterval interval)
    {
        // Start the two rows at the first 64-byte boundary of the buffer. The horizontal map aligns each kernel start
        // to a multiple of 4 pixels (64 bytes), so all pixel loads of the operators then start on a cache line.
        // The offset is calculated again for each call, because the garbage collector can move a managed buffer.
        // If the offset changes, the padding after a row can contain old pixels of an earlier row. These pixels are
        // finite, and their weights are zero, so the results do not change.
        int sourceWidth = this.sourceRectangle.Width;
        int destinationWidth = this.destinationRectangle.Width;
        Span<float> rowsBuffer = this.sourceRowsOwner.GetSpan();
        int rowsStart = ResizeKernel.GetCacheLineOffset(rowsBuffer);
        Span<Vector4> row0 = MemoryMarshal.Cast<float, Vector4>(rowsBuffer.Slice(rowsStart, sourceWidth * 4));
        Span<Vector4> row1 = MemoryMarshal.Cast<float, Vector4>(rowsBuffer.Slice(rowsStart + this.sourceRowStride, sourceWidth * 4));
        ref float row0Base = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(row0));
        ref float row1Base = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(row1));
        Span<Vector4> firstPass = this.firstPassBuffer.GetSpan();
        ref ResizeKernel kernelBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(this.horizontalKernels.GetKernelSpan()), this.destinationRectangle.X);
        ref float weights = ref this.horizontalKernels.GetWeightsReference();
        int length = this.horizontalKernels.PaddedLength;

        // The operators convolve 2 source rows at the same time, so that each weight load serves both rows.
        // If the interval has an odd number of rows, the last step has 1 row. That step gives the same row and the same
        // first-pass row to both inputs of the operators. They calculate the same values twice and store them to the
        // same place.
        for (int y = interval.Min; y < interval.Max; y += 2)
        {
            bool pair = y + 1 < interval.Max;
            this.ConvertSourceRow(y, row0);
            if (pair)
            {
                this.ConvertSourceRow(y + 1, row1);
            }

            // Each source row is horizontally filtered once for every destination X and stored in window row
            // y - currentWindow.Min. A vertical kernel can then reuse this first-pass row wherever adjacent destination
            // kernels overlap it.
            int y1 = pair ? y + 1 : y;
            ResizeKernel.ConvolveRows(
                ref kernelBase,
                ref weights,
                ref row0Base,
                ref pair ? ref row1Base : ref row0Base,
                length,
                destinationWidth,
                ref Unsafe.As<Vector4, float>(ref firstPass[(y - this.currentWindow.Min) * destinationWidth]),
                ref Unsafe.As<Vector4, float>(ref firstPass[(y1 - this.currentWindow.Min) * destinationWidth]));
        }
    }

    /// <summary>
    /// Normalizes one source alpha row and replicates it into vectors for the shared resize kernels.
    /// </summary>
    /// <param name="y">The source row, relative to the visible source rectangle.</param>
    /// <param name="destination">The vectors, one for each source pixel.</param>
    private void ConvertSourceRow(int y, Span<Vector4> destination)
    {
        int sourceWidth = this.sourceRectangle.Width;
        Span<float> normalized = this.componentOwner.GetSpan()[..sourceWidth];
        Span<L16> sourceAlpha = this.alphaOwner.GetSpan()[..sourceWidth];
        ReadOnlySpan<TSample> source = this.buffer.GetLumaRowSpan(this.sourceRectangle.Y + y).Slice(this.sourceRectangle.X, sourceWidth);
        HeifPlanarAlphaCompositor.NormalizeAlphaRow<TSample, TLoader>(source, normalized, in this.parameters);

        // ResizeKernel is the SIMD convolution primitive of the general image resizer. Alpha copied into Vector4
        // lets that kernel operate on the planar row. The L16 round trip gives the same result as a resize of a full
        // L16 alpha image, but the worker does not allocate that image.
        HeifSampleConversion.PackL16(normalized, sourceAlpha);
        PixelOperations<L16>.Instance.ToUnassociatedScaledVector4(this.configuration, sourceAlpha, destination);
    }
}

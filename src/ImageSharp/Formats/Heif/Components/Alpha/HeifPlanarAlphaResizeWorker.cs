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
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
/// <typeparam name="TBuffer">The codec adapter exposing the reconstructed component planes.</typeparam>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
/// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
internal sealed class HeifPlanarAlphaResizeWorker<TPixel, TBuffer, TSample, TLoader> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
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
    /// The packed color frame receiving alpha values.
    /// </summary>
    private readonly ImageFrame<TPixel> destination;

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
    /// The transposed horizontally filtered rows retained by the sliding window.
    /// </summary>
    private readonly Buffer2D<Vector4> transposedFirstPassBuffer;

    /// <summary>
    /// The reusable normalized source or resized destination row.
    /// </summary>
    private readonly IMemoryOwner<float> componentOwner;

    /// <summary>
    /// The reusable replicated source row consumed by the shared resize kernels.
    /// </summary>
    private readonly IMemoryOwner<Vector4> sourceVectorOwner;

    /// <summary>
    /// The reusable 16-bit source and destination alpha packing row.
    /// </summary>
    private readonly IMemoryOwner<L16> alphaOwner;

    /// <summary>
    /// The reusable high-bit-depth destination color row.
    /// </summary>
    private readonly IMemoryOwner<Rgba64> colorOwner;

    /// <summary>
    /// Whether stored color samples must be converted to unassociated alpha.
    /// </summary>
    private readonly bool premultiplied;

    /// <summary>
    /// The number of source rows retained when the window advances.
    /// </summary>
    private readonly int windowBandHeight;

    /// <summary>
    /// The total number of source rows retained by the bounded working window.
    /// </summary>
    private readonly int workerHeight;

    /// <summary>
    /// The source-row interval currently represented by the transposed first-pass buffer.
    /// </summary>
    private RowInterval currentWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifPlanarAlphaResizeWorker{TPixel, TBuffer, TSample, TLoader}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used for pooled allocation and pixel conversion.</param>
    /// <param name="buffer">The codec-native component planes.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="parameters">The resolved H.273 component-range parameters.</param>
    /// <param name="sourceRectangle">The visible luma rectangle within the reconstructed plane.</param>
    /// <param name="destinationRectangle">The destination region receiving the top-left portion of the presented alpha plane.</param>
    /// <param name="horizontalKernels">The horizontal box-filter kernels for the full presented width.</param>
    /// <param name="verticalKernels">The vertical box-filter kernels for the full presented height.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    public HeifPlanarAlphaResizeWorker(
        Configuration configuration,
        TBuffer buffer,
        ImageFrame<TPixel> destination,
        in HeifColorConversionParameters parameters,
        Rectangle sourceRectangle,
        Rectangle destinationRectangle,
        ResizeKernelMap horizontalKernels,
        ResizeKernelMap verticalKernels,
        bool premultiplied)
    {
        this.configuration = configuration;
        this.buffer = buffer;
        this.destination = destination;
        this.parameters = parameters;
        this.sourceRectangle = sourceRectangle;
        this.destinationRectangle = destinationRectangle;
        this.premultiplied = premultiplied;

        this.horizontalKernels = horizontalKernels;
        this.verticalKernels = verticalKernels;

        // Retaining one complete maximum-diameter band is sufficient for every vertical kernel that crosses a
        // window boundary. Those first-pass rows can be copied forward instead of normalized and filtered again.
        this.windowBandHeight = this.verticalKernels.MaxDiameter;

        // As in ResizeWorker, the first pass is stored transposed as [destination X][source Y]. Bounding the source-Y
        // dimension by the configured working-buffer limit keeps memory independent of the complete alpha-plane size.
        int workingBufferLimitInBytes = Math.Min(
            configuration.WorkingBufferSizeHintInBytes,
            configuration.MemoryAllocator.GetBufferCapacityInBytes());

        int windowBandCount = ResizeHelper.CalculateResizeWorkerHeightInWindowBands(
            this.windowBandHeight,
            destinationRectangle.Width,
            workingBufferLimitInBytes);

        // A whole number of bands lets Slide retain exactly one overlap band and fill the remaining window with rows
        // that have not entered the first pass before.
        this.workerHeight = Math.Min(sourceRectangle.Height, windowBandCount * this.windowBandHeight);
        this.transposedFirstPassBuffer = configuration.MemoryAllocator.Allocate2D<Vector4>(
            this.workerHeight,
            destinationRectangle.Width,
            preferContiguosImageBuffers: true,
            options: AllocationOptions.Clean);

        this.componentOwner = configuration.MemoryAllocator.Allocate<float>(Math.Max(sourceRectangle.Width, destinationRectangle.Width));
        this.sourceVectorOwner = configuration.MemoryAllocator.Allocate<Vector4>(sourceRectangle.Width);
        this.alphaOwner = configuration.MemoryAllocator.Allocate<L16>(Math.Max(sourceRectangle.Width, destinationRectangle.Width));
        this.colorOwner = configuration.MemoryAllocator.Allocate<Rgba64>(destinationRectangle.Width);
        this.currentWindow = new RowInterval(0, this.workerHeight);
    }

    /// <summary>
    /// Releases all allocator-owned working buffers.
    /// </summary>
    public void Dispose()
    {
        this.transposedFirstPassBuffer.Dispose();
        this.componentOwner.Dispose();
        this.sourceVectorOwner.Dispose();
        this.alphaOwner.Dispose();
        this.colorOwner.Dispose();
    }

    /// <summary>
    /// Resizes and composes the complete requested destination rectangle.
    /// </summary>
    public void Compose()
    {
        // Populate the horizontal first pass for the initial bounded source-row interval. Later windows retain their
        // overlap and calculate only newly entering rows.
        this.CalculateFirstPassValues(this.currentWindow);

        Span<Vector4> transposed = this.transposedFirstPassBuffer.DangerousGetSingleSpan();
        Span<float> resizedAlpha = this.componentOwner.GetSpan()[..this.destinationRectangle.Width];
        Span<L16> packedAlpha = this.alphaOwner.GetSpan()[..this.destinationRectangle.Width];
        Span<Rgba64> packedColor = this.colorOwner.GetSpan()[..this.destinationRectangle.Width];
        ReadOnlySpan<ResizeKernel> verticalKernelSpan = this.verticalKernels.GetKernelSpan();
        ref ResizeKernel verticalKernelBase = ref MemoryMarshal.GetReference(verticalKernelSpan);
        ref float resizedAlphaBase = ref MemoryMarshal.GetReference(resizedAlpha);
        int currentWindowMin = this.currentWindow.Min;
        int currentWindowMax = this.currentWindow.Max;
        nuint width = (uint)this.destinationRectangle.Width;
        nuint workerHeight = (uint)this.workerHeight;
        nuint twoWorkerHeights = workerHeight * 2;

        for (int y = 0; y < this.destinationRectangle.Height; y++)
        {
            ref ResizeKernel kernel = ref Unsafe.Add(ref verticalKernelBase, y);
            int kernelEnd = kernel.StartIndex + kernel.Length;

            // Destination kernels advance monotonically through source Y. Slide until the complete kernel lies in
            // the cached first-pass interval; the retained overlap prevents any shared source row being recalculated.
            while (kernelEnd > currentWindowMax)
            {
                this.Slide();
                currentWindowMin = this.currentWindow.Min;
                currentWindowMax = this.currentWindow.Max;
            }

            // Values for one destination X are contiguous along source Y in the transposed buffer. ConvolveCore
            // therefore reads the vertical kernel without gathers, while workerHeight advances to the next X column.
            ref Vector4 column = ref transposed[kernel.StartIndex - currentWindowMin];
            nuint x = 0;
            for (; x + 1 < width; x += 2)
            {
                Unsafe.Add(ref resizedAlphaBase, x) = kernel.ConvolveCore(ref column).X;
                ref Vector4 nextColumn = ref Unsafe.Add(ref column, workerHeight);
                Unsafe.Add(ref resizedAlphaBase, x + 1) = kernel.ConvolveCore(ref nextColumn).X;
                column = ref Unsafe.Add(ref column, twoWorkerHeights);
            }

            if (x < width)
            {
                Unsafe.Add(ref resizedAlphaBase, x) = kernel.ConvolveCore(ref column).X;
            }

            HeifPlanarAlphaCompositor.ApplyAlphaRow(
                this.configuration,
                this.destination,
                this.destinationRectangle.X,
                this.destinationRectangle.Y + y,
                resizedAlpha,
                packedAlpha,
                packedColor,
                this.premultiplied);
        }
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

        // Buffer2D columns represent source Y because the first pass is transposed. Move the retained bottom band to
        // offset zero for every destination-X column before replacing the remainder of the window.
        this.transposedFirstPassBuffer.DangerousCopyColumns(
            this.workerHeight - this.windowBandHeight,
            0,
            this.windowBandHeight);

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
        int sourceWidth = this.sourceRectangle.Width;
        int destinationWidth = this.destinationRectangle.Width;
        Span<float> normalized = this.componentOwner.GetSpan()[..sourceWidth];
        Span<L16> sourceAlpha = this.alphaOwner.GetSpan()[..sourceWidth];
        Span<Vector4> sourceVectors = this.sourceVectorOwner.GetSpan()[..sourceWidth];
        Span<Vector4> transposed = this.transposedFirstPassBuffer.DangerousGetSingleSpan();
        ReadOnlySpan<ResizeKernel> horizontalKernelSpan = this.horizontalKernels.GetKernelSpan();
        ref ResizeKernel horizontalKernelBase = ref MemoryMarshal.GetReference(horizontalKernelSpan);
        nuint workerHeight = (uint)this.workerHeight;

        for (int y = interval.Min; y < interval.Max; y++)
        {
            ReadOnlySpan<TSample> source = this.buffer.GetLumaRowSpan(this.sourceRectangle.Y + y).Slice(this.sourceRectangle.X, sourceWidth);
            HeifPlanarAlphaCompositor.NormalizeAlphaRow<TSample, TLoader>(source, normalized, in this.parameters);

            // ResizeKernel is the same SIMD convolution primitive used by the general image resizer. Replicating alpha
            // into Vector4 lets that kernel operate on the planar row, while the L16 round trip preserves the result of
            // the removed Image<L16> path without materializing the complete alpha image.
            HeifSampleConversion.PackL16(normalized, sourceAlpha);
            PixelOperations<L16>.Instance.ToVector4(this.configuration, sourceAlpha, sourceVectors, PixelConversionModifiers.Scale);

            // The source row is horizontally filtered once for every destination X and stored at [X][window Y]. A
            // vertical kernel can then reuse this first-pass row wherever adjacent destination kernels overlap it.
            ref Vector4 firstPass = ref transposed[y - this.currentWindow.Min];
            int x = 0;
            for (; x + 1 < destinationWidth; x += 2)
            {
                ref ResizeKernel kernel0 = ref Unsafe.Add(ref horizontalKernelBase, x);
                ref ResizeKernel kernel1 = ref Unsafe.Add(ref horizontalKernelBase, x + 1);
                Unsafe.Add(ref firstPass, (nuint)x * workerHeight) = kernel0.Convolve(sourceVectors);
                Unsafe.Add(ref firstPass, (nuint)(x + 1) * workerHeight) = kernel1.Convolve(sourceVectors);
            }

            if (x < destinationWidth)
            {
                ref ResizeKernel kernel = ref Unsafe.Add(ref horizontalKernelBase, x);
                Unsafe.Add(ref firstPass, (nuint)x * workerHeight) = kernel.Convolve(sourceVectors);
            }
        }
    }
}

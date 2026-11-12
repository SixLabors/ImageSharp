// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Implements the resize algorithm using a sliding window of size
/// maximized by <see cref="Configuration.WorkingBufferSizeHintInBytes"/>.
/// The height of the window is a multiple of the vertical kernel's maximum diameter.
/// When sliding the window, the contents of the bottom window band are copied to the new top band.
/// For more details, and diagrams of the buffers and the SIMD operators, see "ResizeWorker.md".
/// </summary>
internal sealed class ResizeWorker<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// The first-pass rows of the window. Row Y holds the horizontally filtered pixels of source row
    /// <c>currentWindow.Min + Y</c>, one pixel for each destination X.
    /// </summary>
    private readonly IMemoryOwner<Vector4> firstPassBuffer;

    private readonly Configuration configuration;

    private readonly bool premultiply;

    private readonly bool compand;

    private readonly PixelColorRange colorRange;

    private readonly ResizeKernelMap horizontalKernelMap;

    private readonly Buffer2DRegion<TPixel> source;

    private readonly Rectangle sourceRectangle;

    /// <summary>
    /// The two source rows that the horizontal pass converts to vectors and convolves at the same time.
    /// </summary>
    /// <remarks>
    /// The buffer holds floats, not vectors, so that the rows can start at any multiple of 4 bytes. Each row starts on
    /// a 64-byte boundary in the buffer. See <see cref="CalculateFirstPassValues"/>.
    /// </remarks>
    private readonly IMemoryOwner<float> tempRowsBuffer;

    /// <summary>
    /// The distance in floats from the first source row in <see cref="tempRowsBuffer"/> to the second. This is a
    /// multiple of 16 floats (64 bytes), so that both rows start on a 64-byte boundary.
    /// </summary>
    private readonly int tempRowStride;

    private readonly IMemoryOwner<Vector4> tempColumnBuffer;

    private readonly ResizeKernelMap verticalKernelMap;

    private readonly Rectangle targetWorkingRect;

    private readonly Point targetOrigin;

    private readonly int windowBandHeight;

    private readonly int workerHeight;

    private RowInterval currentWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResizeWorker{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used for memory allocation.</param>
    /// <param name="source">The source pixel region.</param>
    /// <param name="compand">Whether to interpolate RGB in linear light by expanding and recompressing the sRGB transfer function.</param>
    /// <param name="premultiplyAlpha">Whether to interpolate color components with associated alpha.</param>
    /// <param name="horizontalKernelMap">The horizontal resampling kernels.</param>
    /// <param name="verticalKernelMap">The vertical resampling kernels.</param>
    /// <param name="targetWorkingRect">The target rectangle processed by this worker.</param>
    /// <param name="targetOrigin">The origin of the target image.</param>
    public ResizeWorker(
        Configuration configuration,
        Buffer2DRegion<TPixel> source,
        bool compand,
        bool premultiplyAlpha,
        ResizeKernelMap horizontalKernelMap,
        ResizeKernelMap verticalKernelMap,
        Rectangle targetWorkingRect,
        Point targetOrigin)
    {
        this.configuration = configuration;
        this.source = source;
        this.sourceRectangle = source.Bounds;
        this.compand = compand;

        PixelTypeInfo pixelInfo = TPixel.GetPixelTypeInfo();
        this.colorRange = pixelInfo.ColorRange;
        this.premultiply = premultiplyAlpha;
        this.horizontalKernelMap = horizontalKernelMap;
        this.verticalKernelMap = verticalKernelMap;
        this.targetWorkingRect = targetWorkingRect;
        this.targetOrigin = targetOrigin;

        this.windowBandHeight = verticalKernelMap.MaxDiameter;

        // We need to make sure the working buffer is contiguous:
        int workingBufferLimitHintInBytes = Math.Min(
            configuration.WorkingBufferSizeHintInBytes,
            configuration.MemoryAllocator.GetBufferCapacityInBytes());

        int numberOfWindowBands = ResizeHelper.CalculateResizeWorkerHeightInWindowBands(
            this.windowBandHeight,
            targetWorkingRect.Width,
            workingBufferLimitHintInBytes);

        this.workerHeight = Math.Min(this.sourceRectangle.Height, numberOfWindowBands * this.windowBandHeight);

        // The first pass writes every pixel of a window row before the vertical pass reads it, so the buffer needs no clear.
        this.firstPassBuffer = configuration.MemoryAllocator.Allocate<Vector4>(this.workerHeight * targetWorkingRect.Width);

        // The horizontal operators read PaddedLength taps. The taps after the length of each kernel have the weight zero,
        // but a kernel at the end of the row reads up to PaddedLength - MaxDiameter pixels after the row. These pixels
        // must be finite, because a weight of zero times a finite value is zero. The clean buffer gives them the value
        // zero, and the conversion of a source row never writes them.
        //
        // Each row has the source pixels and then this padding. The stride rounds the row up to a multiple of
        // 4 pixels (64 bytes), so that the second row starts on a 64-byte boundary when the first row does.
        // The memory allocator does not align its buffers, so the buffer has 15 more floats. The rows start at the
        // first 64-byte boundary in the buffer, 0 to 15 floats after its start.
        int padding = horizontalKernelMap.PaddedLength - horizontalKernelMap.MaxDiameter;
        this.tempRowStride = ((this.sourceRectangle.Width + padding + 3) & ~3) * 4;
        this.tempRowsBuffer = configuration.MemoryAllocator.Allocate<float>((this.tempRowStride * 2) + 15, AllocationOptions.Clean);
        this.tempColumnBuffer = configuration.MemoryAllocator.Allocate<Vector4>(targetWorkingRect.Width);

        this.currentWindow = new RowInterval(0, this.workerHeight);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.firstPassBuffer.Dispose();
        this.tempRowsBuffer.Dispose();
        this.tempColumnBuffer.Dispose();
    }

    /// <summary>
    /// Calculates the first-pass rows of the first window.
    /// </summary>
    public void Initialize()
        => this.CalculateFirstPassValues(this.currentWindow);

    /// <summary>
    /// Calculates the destination pixels of <paramref name="rowInterval"/> with the vertical kernels.
    /// </summary>
    /// <param name="rowInterval">The destination rows.</param>
    /// <param name="destination">The destination buffer.</param>
    public void FillDestinationPixels(RowInterval rowInterval, Buffer2D<TPixel> destination)
    {
        Span<Vector4> tempColSpan = this.tempColumnBuffer.GetSpan();
        Span<Vector4> firstPass = this.firstPassBuffer.GetSpan();

        int left = this.targetWorkingRect.Left;
        int width = this.targetWorkingRect.Width;

        // Normalize destination-space Y to kernel indices using uint arithmetic.
        // This relies on the contract that processing addresses are normalized (cropping/padding handled by targetOrigin).
        int targetOriginY = this.targetOrigin.Y;

        // Hoist invariant calculations outside the loop.
        int currentWindowMax = this.currentWindow.Max;
        int currentWindowMin = this.currentWindow.Min;

        // The kernels address floats. A pixel is 4 floats, so one first-pass row is width * 4 floats long.
        nuint rowStride = (uint)width * 4;
        int length = this.verticalKernelMap.MaxDiameter;

        // Ref-walk the kernel table to avoid bounds checks in the tight loop.
        ReadOnlySpan<ResizeKernel> vKernels = this.verticalKernelMap.GetKernelSpan();
        ref ResizeKernel vKernelBase = ref MemoryMarshal.GetReference(vKernels);
        ref float weightsBase = ref this.verticalKernelMap.GetWeightsReference();
        ref float tempRowBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(tempColSpan));

        for (int y = rowInterval.Min; y < rowInterval.Max; y++)
        {
            // Normalize destination-space Y to an unsigned kernel index.
            uint vIdx = (uint)(y - targetOriginY);
            ref ResizeKernel kernel = ref Unsafe.Add(ref vKernelBase, (nint)vIdx);

            // Slide the working window when the kernel would read beyond the current cached region.
            int kernelEnd = kernel.StartIndex + kernel.Length;
            while (kernelEnd > currentWindowMax)
            {
                this.Slide();
                currentWindowMax = this.currentWindow.Max;
                currentWindowMin = this.currentWindow.Min;
            }

            // The kernel reads its rows of the window. Neighboring destination pixels are neighbors in each row.
            ref float rows = ref Unsafe.As<Vector4, float>(ref firstPass[(kernel.StartIndex - currentWindowMin) * width]);
            ResizeKernel.ConvolveColumns(
                ref Unsafe.Add(ref weightsBase, (uint)kernel.WeightsOffset),
                ref rows,
                rowStride,
                length,
                width,
                ref tempRowBase);

            Span<TPixel> targetRowSpan = destination.DangerousGetRowSpan(y).Slice(left, width);
            if (this.compand)
            {
                // The resampler stores associated vectors only when requested; companding needs straight color first.
                if (this.premultiply)
                {
                    Numerics.UnPremultiply(tempColSpan);
                }

                SRgbCompanding.Compress(tempColSpan, this.colorRange);
                PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, tempColSpan, targetRowSpan);
            }
            else if (this.premultiply)
            {
                PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(this.configuration, tempColSpan, targetRowSpan);
            }
            else
            {
                PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, tempColSpan, targetRowSpan);
            }
        }
    }

    /// <summary>
    /// Moves the window down. The bottom band of rows becomes the top band, and the first pass calculates the other rows.
    /// </summary>
    private void Slide()
    {
        int minY = this.currentWindow.Max - this.windowBandHeight;
        int maxY = Math.Min(minY + this.workerHeight, this.sourceRectangle.Height);

        // The rows are contiguous, so the bottom band is one block. A slide occurs only when the window holds at least
        // 2 bands, so the source and destination blocks do not overlap.
        int width = this.targetWorkingRect.Width;
        Span<Vector4> firstPass = this.firstPassBuffer.GetSpan();
        firstPass.Slice((this.workerHeight - this.windowBandHeight) * width, this.windowBandHeight * width).CopyTo(firstPass);

        this.currentWindow = new RowInterval(minY, maxY);

        // Calculate the remainder:
        this.CalculateFirstPassValues(this.currentWindow.Slice(this.windowBandHeight));
    }

    /// <summary>
    /// Calculates the first-pass rows of the source rows of <paramref name="calculationInterval"/> with the horizontal kernels.
    /// </summary>
    /// <param name="calculationInterval">The source rows.</param>
    private void CalculateFirstPassValues(RowInterval calculationInterval)
    {
        // Start the two rows at the first 64-byte boundary of the buffer. The horizontal map aligns each kernel start
        // to a multiple of 4 pixels (64 bytes), so all pixel loads of the operators then start on a cache line.
        // The offset is calculated again for each call, because the garbage collector can move a managed buffer.
        // If the offset changes, the padding after a row can contain old pixels of an earlier row. These pixels are
        // finite, and their weights are zero, so the results do not change.
        Span<float> rowsBuffer = this.tempRowsBuffer.GetSpan();
        int rowsStart = ResizeKernel.GetCacheLineOffset(rowsBuffer);
        int sourceWidth = this.sourceRectangle.Width;
        Span<Vector4> row0 = MemoryMarshal.Cast<float, Vector4>(rowsBuffer.Slice(rowsStart, sourceWidth * 4));
        Span<Vector4> row1 = MemoryMarshal.Cast<float, Vector4>(rowsBuffer.Slice(rowsStart + this.tempRowStride, sourceWidth * 4));
        ref float row0Base = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(row0));
        ref float row1Base = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(row1));
        Span<Vector4> firstPass = this.firstPassBuffer.GetSpan();

        // Normalize destination-space X to kernel indices. The kernel of the first working column is at left - targetOriginX.
        // This relies on the contract that processing addresses are normalized (cropping/padding handled by targetOrigin).
        ReadOnlySpan<ResizeKernel> hKernels = this.horizontalKernelMap.GetKernelSpan();
        ref ResizeKernel kernelBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(hKernels), this.targetWorkingRect.Left - this.targetOrigin.X);
        int width = this.targetWorkingRect.Width;
        int currentWindowMin = this.currentWindow.Min;
        int length = this.horizontalKernelMap.PaddedLength;
        ref float weightsBase = ref this.horizontalKernelMap.GetWeightsReference();

        // The operators convolve 2 source rows at the same time, so that each weight load serves both rows.
        // If the interval has an odd number of rows, the last step has 1 row. That step gives the same row and the same
        // first-pass row to both inputs of the operators. They calculate the same values twice and store them to the
        // same place. This costs one extra row of work for each interval, and keeps one code path.
        for (int y = calculationInterval.Min; y < calculationInterval.Max; y += 2)
        {
            bool pair = y + 1 < calculationInterval.Max;
            this.ConvertSourceRow(y, row0);
            if (pair)
            {
                this.ConvertSourceRow(y + 1, row1);
            }

            // The pixels of destination X go to window row y - currentWindowMin, one pixel for each X.
            int y1 = pair ? y + 1 : y;
            ResizeKernel.ConvolveRows(
                ref kernelBase,
                ref weightsBase,
                ref row0Base,
                ref pair ? ref row1Base : ref row0Base,
                length,
                width,
                ref Unsafe.As<Vector4, float>(ref firstPass[(y - currentWindowMin) * width]),
                ref Unsafe.As<Vector4, float>(ref firstPass[(y1 - currentWindowMin) * width]));
        }
    }

    /// <summary>
    /// Converts one source row to scaled vectors in the representation of the resize: straight or associated alpha,
    /// and companded to linear light if requested.
    /// </summary>
    /// <param name="y">The source row, relative to the source region.</param>
    /// <param name="destination">The vectors, one for each source pixel.</param>
    private void ConvertSourceRow(int y, Span<Vector4> destination)
    {
        Span<TPixel> sourceRow = this.source.DangerousGetRowSpan(y);
        if (this.compand)
        {
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.configuration, sourceRow, destination);
            SRgbCompanding.Expand(destination, this.colorRange);

            // Interpolate in associated alpha only after expanding the straight color values.
            if (this.premultiply)
            {
                Numerics.Premultiply(destination);
            }
        }
        else if (this.premultiply)
        {
            PixelOperations<TPixel>.Instance.ToAssociatedScaledVector4(this.configuration, sourceRow, destination);
        }
        else
        {
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.configuration, sourceRow, destination);
        }
    }
}

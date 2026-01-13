// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Implements the resize algorithm using a sliding window of size
/// maximized by <see cref="Configuration.WorkingBufferSizeHintInBytes"/>.
/// The height of the window is a multiple of the vertical kernel's maximum diameter.
/// When sliding the window, the contents of the bottom window band are copied to the new top band.
/// For more details, and visual explanation, see "ResizeWorker.pptx".
/// </summary>
internal sealed class ResizeWorker<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Buffer2D<Vector4> transposedFirstPassBuffer;

    private readonly Configuration configuration;

    private readonly PixelConversionModifiers conversionModifiers;

    private readonly ResizeKernelMap horizontalKernelMap;

    private readonly Buffer2DRegion<TPixel> source;

    private readonly Rectangle sourceRectangle;

    private readonly IMemoryOwner<Vector4> tempRowBuffer;

    private readonly IMemoryOwner<Vector4> tempColumnBuffer;

    private readonly ResizeKernelMap verticalKernelMap;

    private readonly Rectangle targetWorkingRect;

    private readonly Point targetOrigin;

    private readonly int windowBandHeight;

    private readonly int workerHeight;

    private RowInterval currentWindow;

    public ResizeWorker(
        Configuration configuration,
        Buffer2DRegion<TPixel> source,
        PixelConversionModifiers conversionModifiers,
        ResizeKernelMap horizontalKernelMap,
        ResizeKernelMap verticalKernelMap,
        Rectangle targetWorkingRect,
        Point targetOrigin)
    {
        this.configuration = configuration;
        this.source = source;
        this.sourceRectangle = source.Rectangle;
        this.conversionModifiers = conversionModifiers;
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

        this.transposedFirstPassBuffer = configuration.MemoryAllocator.Allocate2D<Vector4>(
            this.workerHeight,
            targetWorkingRect.Width,
            preferContiguosImageBuffers: true,
            options: AllocationOptions.Clean);

        this.tempRowBuffer = configuration.MemoryAllocator.Allocate<Vector4>(this.sourceRectangle.Width);
        this.tempColumnBuffer = configuration.MemoryAllocator.Allocate<Vector4>(targetWorkingRect.Width);

        this.currentWindow = new RowInterval(0, this.workerHeight);
    }

    public void Dispose()
    {
        this.transposedFirstPassBuffer.Dispose();
        this.tempRowBuffer.Dispose();
        this.tempColumnBuffer.Dispose();
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public Span<Vector4> GetColumnSpan(int x, int startY)
        => this.transposedFirstPassBuffer.DangerousGetRowSpan(x)[(startY - this.currentWindow.Min)..];

    public void Initialize()
        => this.CalculateFirstPassValues(this.currentWindow);

    public void FillDestinationPixels(RowInterval rowInterval, Buffer2D<TPixel> destination)
    {
        Span<Vector4> tempColSpan = this.tempColumnBuffer.GetSpan();

        // When creating transposedFirstPassBuffer, we made sure it's contiguous.
        Span<Vector4> transposedFirstPassBufferSpan = this.transposedFirstPassBuffer.DangerousGetSingleSpan();

        int left = this.targetWorkingRect.Left;
        int width = this.targetWorkingRect.Width;
        nuint widthCount = (uint)width;

        // Normalize destination-space Y to kernel indices using uint arithmetic.
        // This relies on the contract that processing addresses are normalized (cropping/padding handled by targetOrigin).
        int targetOriginY = this.targetOrigin.Y;

        // Hoist invariant calculations outside the loop.
        int currentWindowMax = this.currentWindow.Max;
        int currentWindowMin = this.currentWindow.Min;
        nuint workerHeight = (uint)this.workerHeight;
        nuint workerHeight2 = workerHeight * 2;

        // Ref-walk the kernel table to avoid bounds checks in the tight loop.
        ReadOnlySpan<ResizeKernel> vKernels = this.verticalKernelMap.GetKernelSpan();
        ref ResizeKernel vKernelBase = ref MemoryMarshal.GetReference(vKernels);

        ref Vector4 tempRowBase = ref MemoryMarshal.GetReference(tempColSpan);

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

            int top = kernel.StartIndex - currentWindowMin;
            ref Vector4 colRef0 = ref transposedFirstPassBufferSpan[top];

            // Unroll by 2 and advance column refs via arithmetic to reduce inner-loop overhead.
            nuint i = 0;
            for (; i + 1 < widthCount; i += 2)
            {
                ref Vector4 colRef1 = ref Unsafe.Add(ref colRef0, workerHeight);

                Unsafe.Add(ref tempRowBase, i) = kernel.ConvolveCore(ref colRef0);
                Unsafe.Add(ref tempRowBase, i + 1) = kernel.ConvolveCore(ref colRef1);

                colRef0 = ref Unsafe.Add(ref colRef0, workerHeight2);
            }

            if (i < widthCount)
            {
                Unsafe.Add(ref tempRowBase, i) = kernel.ConvolveCore(ref colRef0);
            }

            Span<TPixel> targetRowSpan = destination.DangerousGetRowSpan(y).Slice(left, width);

            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, tempColSpan, targetRowSpan, this.conversionModifiers);
        }
    }

    private void Slide()
    {
        int minY = this.currentWindow.Max - this.windowBandHeight;
        int maxY = Math.Min(minY + this.workerHeight, this.sourceRectangle.Height);

        // Copy previous bottom band to the new top:
        // (rows <--> columns, because the buffer is transposed)
        this.transposedFirstPassBuffer.DangerousCopyColumns(
            this.workerHeight - this.windowBandHeight,
            0,
            this.windowBandHeight);

        this.currentWindow = new RowInterval(minY, maxY);

        // Calculate the remainder:
        this.CalculateFirstPassValues(this.currentWindow.Slice(this.windowBandHeight));
    }

    private void CalculateFirstPassValues(RowInterval calculationInterval)
    {
        Span<Vector4> tempRowSpan = this.tempRowBuffer.GetSpan();
        Span<Vector4> transposedFirstPassBufferSpan = this.transposedFirstPassBuffer.DangerousGetSingleSpan();

        nuint left = (uint)this.targetWorkingRect.Left;
        nuint right = (uint)this.targetWorkingRect.Right;
        nuint widthCount = right - left;

        // Normalize destination-space X to kernel indices using uint arithmetic.
        // This relies on the contract that processing addresses are normalized (cropping/padding handled by targetOrigin).
        nuint targetOriginX = (uint)this.targetOrigin.X;

        nuint workerHeight = (uint)this.workerHeight;
        int currentWindowMin = this.currentWindow.Min;

        // Ref-walk the kernel table to avoid bounds checks in the tight loop.
        ReadOnlySpan<ResizeKernel> hKernels = this.horizontalKernelMap.GetKernelSpan();
        ref ResizeKernel hKernelBase = ref MemoryMarshal.GetReference(hKernels);

        for (int y = calculationInterval.Min; y < calculationInterval.Max; y++)
        {
            Span<TPixel> sourceRow = this.source.DangerousGetRowSpan(y);

            PixelOperations<TPixel>.Instance.ToVector4(
                this.configuration,
                sourceRow,
                tempRowSpan,
                this.conversionModifiers);

            ref Vector4 firstPassBaseRef = ref transposedFirstPassBufferSpan[y - currentWindowMin];

            // Unroll by 2 to reduce loop and kernel lookup overhead.
            nuint x = left;
            nuint z = 0;

            for (; z + 1 < widthCount; x += 2, z += 2)
            {
                nuint hIdx0 = (uint)(x - targetOriginX);
                nuint hIdx1 = (uint)((x + 1) - targetOriginX);

                ref ResizeKernel kernel0 = ref Unsafe.Add(ref hKernelBase, (nint)hIdx0);
                ref ResizeKernel kernel1 = ref Unsafe.Add(ref hKernelBase, (nint)hIdx1);

                Unsafe.Add(ref firstPassBaseRef, z * workerHeight) = kernel0.Convolve(tempRowSpan);
                Unsafe.Add(ref firstPassBaseRef, (z + 1) * workerHeight) = kernel1.Convolve(tempRowSpan);
            }

            if (z < widthCount)
            {
                nuint hIdx = (uint)(x - targetOriginX);
                ref ResizeKernel kernel = ref Unsafe.Add(ref hKernelBase, (nint)hIdx);

                Unsafe.Add(ref firstPassBaseRef, z * workerHeight) = kernel.Convolve(tempRowSpan);
            }
        }
    }
}

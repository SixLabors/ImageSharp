// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Provides a map of the convolution kernel sampling offsets.
/// </summary>
internal sealed class KernelSamplingMap : IDisposable
{
    private readonly MemoryAllocator allocator;
    private bool isDisposed;
    private IMemoryOwner<int>? yOffsets;
    private IMemoryOwner<int>? xOffsets;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelSamplingMap"/> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    public KernelSamplingMap(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Builds a map of the sampling offsets for the kernel clamped by the given bounds.
    /// </summary>
    /// <param name="kernel">The convolution kernel.</param>
    /// <param name="bounds">The source bounds.</param>
    public void BuildSamplingOffsetMap(DenseMatrix<float> kernel, Rectangle bounds)
        => this.BuildSamplingOffsetMap(kernel.Rows, kernel.Columns, bounds, BorderWrappingMode.Repeat, BorderWrappingMode.Repeat);

    /// <summary>
    /// Builds a map of the sampling offsets for the kernel clamped by the given bounds.
    /// </summary>
    /// <param name="kernelHeight">The height (number of rows) of the convolution kernel to use.</param>
    /// <param name="kernelWidth">The width (number of columns) of the convolution kernel to use.</param>
    /// <param name="bounds">The source bounds.</param>
    public void BuildSamplingOffsetMap(int kernelHeight, int kernelWidth, Rectangle bounds)
        => this.BuildSamplingOffsetMap(kernelHeight, kernelWidth, bounds, BorderWrappingMode.Repeat, BorderWrappingMode.Repeat);

    /// <summary>
    /// Builds a map of the sampling offsets for the kernel clamped by the given bounds.
    /// </summary>
    /// <param name="kernelHeight">The height (number of rows) of the convolution kernel to use.</param>
    /// <param name="kernelWidth">The width (number of columns) of the convolution kernel to use.</param>
    /// <param name="bounds">The source bounds.</param>
    /// <param name="xBorderMode">The wrapping mode on the horizontal borders.</param>
    /// <param name="yBorderMode">The wrapping mode on the vertical borders.</param>
    public void BuildSamplingOffsetMap(int kernelHeight, int kernelWidth, Rectangle bounds, BorderWrappingMode xBorderMode, BorderWrappingMode yBorderMode)
    {
        this.yOffsets = this.allocator.Allocate<int>(bounds.Height * kernelHeight);
        this.xOffsets = this.allocator.Allocate<int>(bounds.Width * kernelWidth);

        int minY = bounds.Y;
        int maxY = bounds.Bottom - 1;
        int minX = bounds.X;
        int maxX = bounds.Right - 1;

        BuildOffsets(this.yOffsets, bounds.Height, kernelHeight, minY, maxY, yBorderMode);
        BuildOffsets(this.xOffsets, bounds.Width, kernelWidth, minX, maxX, xBorderMode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<int> GetRowOffsetSpan() => this.yOffsets!.GetSpan();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<int> GetColumnOffsetSpan() => this.xOffsets!.GetSpan();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.yOffsets?.Dispose();
            this.xOffsets?.Dispose();

            this.isDisposed = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildOffsets(IMemoryOwner<int> offsets, int boundsSize, int kernelSize, int min, int max, BorderWrappingMode borderMode)
    {
        int radius = kernelSize >> 1;
        Span<int> span = offsets.GetSpan();
        ref int spanBase = ref MemoryMarshal.GetReference(span);
        for (int chunk = 0; chunk < boundsSize; chunk++)
        {
            int chunkBase = chunk * kernelSize;
            for (int i = 0; i < kernelSize; i++)
            {
                Unsafe.Add(ref spanBase, (uint)(chunkBase + i)) = chunk + i + min - radius;
            }
        }

        CorrectBorder(span, kernelSize, min, max, borderMode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CorrectBorder(Span<int> span, int kernelSize, int min, int max, BorderWrappingMode borderMode)
    {
        int affectedSize = (kernelSize >> 1) * kernelSize;
        ref int spanBase = ref MemoryMarshal.GetReference(span);
        if (affectedSize >= span.Length && span.Length > 0)
        {
            // The bounds are no larger than the kernel radius: every offset can overshoot the
            // borders by a full sampling extent or more, which the single-pass head and tail
            // corrections below cannot fold back into range (and for bounds strictly smaller
            // than the radius they cannot even slice the span). Fold each offset exactly.
            CorrectBorderExact(span, min, max, borderMode);
        }
        else if (affectedSize > 0)
        {
            switch (borderMode)
            {
                case BorderWrappingMode.Repeat:
                    Numerics.Clamp(span[..affectedSize], min, max);
                    Numerics.Clamp(span[^affectedSize..], min, max);
                    break;
                case BorderWrappingMode.Mirror:
                    int min2dec = min + min - 1;
                    for (int i = 0; i < affectedSize; i++)
                    {
                        int value = span[i];
                        if (value < min)
                        {
                            span[i] = min2dec - value;
                        }
                    }

                    int max2inc = max + max + 1;
                    for (int i = span.Length - affectedSize; i < span.Length; i++)
                    {
                        int value = span[i];
                        if (value > max)
                        {
                            span[i] = max2inc - value;
                        }
                    }

                    break;
                case BorderWrappingMode.Bounce:
                    int min2 = min + min;
                    for (int i = 0; i < affectedSize; i++)
                    {
                        int value = span[i];
                        if (value < min)
                        {
                            span[i] = min2 - value;
                        }
                    }

                    int max2 = max + max;
                    for (int i = span.Length - affectedSize; i < span.Length; i++)
                    {
                        int value = span[i];
                        if (value > max)
                        {
                            span[i] = max2 - value;
                        }
                    }

                    break;
                case BorderWrappingMode.Wrap:
                    int diff = max - min + 1;
                    for (int i = 0; i < affectedSize; i++)
                    {
                        int value = span[i];
                        if (value < min)
                        {
                            span[i] = diff + value;
                        }
                    }

                    for (int i = span.Length - affectedSize; i < span.Length; i++)
                    {
                        int value = span[i];
                        if (value > max)
                        {
                            span[i] = value - diff;
                        }
                    }

                    break;
            }
        }
    }

    private static void CorrectBorderExact(Span<int> span, int min, int max, BorderWrappingMode borderMode)
    {
        int extent = max - min + 1;
        switch (borderMode)
        {
            case BorderWrappingMode.Repeat:
                Numerics.Clamp(span, min, max);
                break;

            case BorderWrappingMode.Mirror:
                // Reflection about the half-sample border: ..., min+1, min | min, min+1, ...
                int mirrorPeriod = 2 * extent;
                for (int i = 0; i < span.Length; i++)
                {
                    int offset = Modulo(span[i] - min, mirrorPeriod);
                    span[i] = min + (offset < extent ? offset : mirrorPeriod - 1 - offset);
                }

                break;

            case BorderWrappingMode.Bounce:
                // Reflection about the border sample itself: ..., min+2, min+1 | min, min+1, ...
                if (extent == 1)
                {
                    span.Fill(min);
                    break;
                }

                int bouncePeriod = (2 * extent) - 2;
                for (int i = 0; i < span.Length; i++)
                {
                    int offset = Modulo(span[i] - min, bouncePeriod);
                    span[i] = min + (offset < extent ? offset : bouncePeriod - offset);
                }

                break;

            case BorderWrappingMode.Wrap:
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = min + Modulo(span[i] - min, extent);
                }

                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Modulo(int value, int period)
    {
        int remainder = value % period;
        return remainder < 0 ? remainder + period : remainder;
    }
}

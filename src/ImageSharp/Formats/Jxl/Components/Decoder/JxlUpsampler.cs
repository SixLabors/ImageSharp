// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Expands JPEG XL float planes with the format's phase filters.
/// </summary>
/// <remarks>
/// Retains one kernel and row workspace for all color and extra-channel planes in a frame.
/// </remarks>
internal sealed partial class JxlUpsampler : IDisposable
{
    /// <summary>
    /// The two-source-pixel radius of each native frame-upsampling phase in stage_upsampling.cc.
    /// </summary>
    private const int UpsamplingRadius = 2;

    /// <summary>
    /// The five source taps on each axis of every frame-upsampling phase.
    /// </summary>
    private const int UpsamplingDiameter = 5;

    /// <summary>
    /// The 25 source weights for one output phase. Expanded kernels store those weights in row-major order.
    /// </summary>
    private const int UpsamplingKernelArea = UpsamplingDiameter * UpsamplingDiameter;
    private readonly ReadOnlyMemory<float> upsampling2Weights;
    private readonly ReadOnlyMemory<float> upsampling4Weights;
    private readonly ReadOnlyMemory<float> upsampling8Weights;
    private readonly IMemoryOwner<float> workspaceOwner;
    private readonly Memory<float> workspace;
    private readonly int kernelCapacity;
    private int preparedFactor;

    /// <summary>
    /// Indicates whether the combined kernel and row storage has been released.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlUpsampler"/> class for validated frame geometry and factors.
    /// </summary>
    /// <param name="image">The custom compact weights, or empty arrays that select the format defaults.</param>
    /// <param name="targetSize">The visible output dimensions shared by the component planes.</param>
    /// <param name="maximumSourceSize">The largest coded source dimensions among the planes to expand.</param>
    /// <param name="maximumFactor">The largest validated twofold, fourfold or eightfold factor to apply.</param>
    /// <param name="allocator">The tracked allocator for the single combined kernel and row workspace.</param>
    public JxlUpsampler(JxlHeader image, Size targetSize, Size maximumSourceSize, int maximumFactor, MemoryAllocator allocator)
        : this(image.Upsampling2Weights, image.Upsampling4Weights, image.Upsampling8Weights, targetSize, maximumSourceSize, maximumFactor, allocator)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlUpsampler"/> class with explicit compact filter weights.
    /// </summary>
    /// <param name="upsampling2Weights">The twofold weights, or empty memory for the format defaults.</param>
    /// <param name="upsampling4Weights">The fourfold weights, or empty memory for the format defaults.</param>
    /// <param name="upsampling8Weights">The eightfold weights, or empty memory for the format defaults.</param>
    /// <param name="targetSize">The visible output dimensions.</param>
    /// <param name="maximumSourceSize">The largest source dimensions among the planes to expand.</param>
    /// <param name="maximumFactor">The largest twofold, fourfold or eightfold factor to apply.</param>
    /// <param name="allocator">The allocator for the retained kernel and row workspace.</param>
    public JxlUpsampler(
        ReadOnlyMemory<float> upsampling2Weights,
        ReadOnlyMemory<float> upsampling4Weights,
        ReadOnlyMemory<float> upsampling8Weights,
        Size targetSize,
        Size maximumSourceSize,
        int maximumFactor,
        MemoryAllocator allocator)
    {
        this.upsampling2Weights = upsampling2Weights;
        this.upsampling4Weights = upsampling4Weights;
        this.upsampling8Weights = upsampling8Weights;
        this.TargetSize = targetSize;
        this.kernelCapacity = maximumFactor * maximumFactor * UpsamplingKernelArea;
        int ringRows = Math.Min(UpsamplingDiameter, maximumSourceSize.Height);
        long stride = (long)maximumSourceSize.Width + (2 * UpsamplingRadius);
        long length = this.kernelCapacity + ((ringRows + 2) * stride);
        if (length > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL upsampling workspace exceeds the supported buffer length.");
        }

        // Kernels and the original-row ring share one tracked owner. The two local-bound rows follow the ring.
        // Every Apply call borrows this storage; different factors replace only the kernel entries they use.
        this.workspaceOwner = allocator.Allocate<float>((int)length);
        this.workspace = this.workspaceOwner.Memory[..(int)length];
    }

    /// <summary>
    /// Gets the visible dimensions written by each expanded component plane.
    /// </summary>
    public Size TargetSize { get; }

    /// <summary>
    /// Mirrors a neighborhood coordinate about the half-sample image border, repeating the border sample.
    /// </summary>
    /// <param name="index">The possibly out-of-range neighborhood coordinate.</param>
    /// <param name="length">The positive axis extent.</param>
    /// <returns>The original physical sample index.</returns>
    public static int MirrorIndex(long index, int length)
    {
        // This is the symmetric border used by libjxl's render pipeline and convolution's mirror rule.
        // Widening the doubled period also handles an int-sized vertical extent without overflow.
        if ((ulong)index < (uint)length)
        {
            return (int)index;
        }

        long period = 2L * length;
        long position = index % period;
        if (position < 0)
        {
            position += period;
        }

        return (int)(position < length ? position : period - 1 - position);
    }

    /// <summary>
    /// Expands one float plane in place with the selected factor. A factor of one leaves the plane unchanged.
    /// </summary>
    /// <param name="plane">The final-capacity plane, with original samples in its top-left source rectangle.</param>
    /// <param name="sourceSize">The original sample dimensions, bounded by the constructor's maximum source dimensions.</param>
    /// <param name="factor">The validated onefold, twofold, fourfold or eightfold factor, bounded by the constructor's maximum.</param>
    /// <param name="cancellationToken">The token checked between source and output rows.</param>
    /// <remarks>
    /// Source rows remain available until their last stencil use. Output samples retain the original neighborhood's range,
    /// which can extend outside zero through one. This operation does not apply pixel or alpha bounds.
    /// </remarks>
    public void Apply(Buffer2D<float> plane, Size sourceSize, int factor, CancellationToken cancellationToken)
    {
        if (factor == 1)
        {
            return;
        }

        Span<float> workspace = this.workspace.Span;
        Span<float> kernels = workspace[..this.kernelCapacity];
        Span<float> window = workspace[this.kernelCapacity..];
        if (factor != this.preparedFactor)
        {
            ReadOnlySpan<float> weights = factor switch
            {
                2 => this.upsampling2Weights.Span,
                4 => this.upsampling4Weights.Span,
                _ => this.upsampling8Weights.Span,
            };

            PrepareUpsamplingKernels(factor, weights.IsEmpty ? GetDefaultUpsamplingWeights(factor) : weights, kernels);
            this.preparedFactor = factor;
        }

        Size targetSize = this.TargetSize;
        int width = sourceSize.Width;
        int height = sourceSize.Height;
        int stride = width + (2 * UpsamplingRadius);
        int ringRows = Math.Min(UpsamplingDiameter, height);
        Span<float> minimum = window.Slice(ringRows * stride, stride);
        Span<float> maximum = window.Slice((ringRows + 1) * stride, stride);
        InlineArray8<int> rowStorage = default;
        Span<int> rows = rowStorage;

        // At the bottom border only the last three physical rows can be requested by a mirrored five-row neighborhood.
        // Tiny images retain each physical row once. Later reflection selects these same cached rows without a second copy.
        for (int y = Math.Max(0, height - UpsamplingRadius - 1); y < height; y++)
        {
            LoadUpsamplingRow(plane, window, stride, ringRows, width, y);
        }

        for (int y = height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int offset = -UpsamplingRadius; offset <= UpsamplingRadius; offset++)
            {
                int physical = MirrorIndex((long)y + offset, height);
                rows[offset + UpsamplingRadius] = (physical % ringRows) * stride;
            }

            PrepareUpsamplingBounds(window, rows, minimum, maximum, width);
            UpsamplingOperator operation = new(window, rows, kernels, minimum, maximum);
            int outputY = y * factor;
            int phaseRows = Math.Min(factor, targetSize.Height - outputY);
            for (int phase = 0; phase < phaseRows; phase++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Span<float> output = plane.DangerousGetRowSpan(outputY + phase)[..targetSize.Width];
                UpsampleRow(in operation, output, factor, phase);
            }

            // Every write starts at row y*factor >= y. The next original row y-3 is above that range and is still untouched.
            // Reusing its modulo-five slot releases row y+2, which the next source row no longer needs.
            int entering = y - UpsamplingRadius - 1;
            if (entering >= 0)
            {
                LoadUpsamplingRow(plane, window, stride, ringRows, width, entering);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.workspaceOwner.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    /// Copies one untouched physical source row and fills its two mirrored horizontal neighbors.
    /// </summary>
    /// <param name="plane">The source and destination plane.</param>
    /// <param name="window">The reusable row ring.</param>
    /// <param name="stride">The padded source-row stride.</param>
    /// <param name="ringRows">The number of distinct physical row slots.</param>
    /// <param name="width">The source row width.</param>
    /// <param name="row">The untouched physical source row.</param>
    private static void LoadUpsamplingRow(Buffer2D<float> plane, Span<float> window, int stride, int ringRows, int width, int row)
    {
        Span<float> target = window.Slice((row % ringRows) * stride, stride);
        plane.DangerousGetRowSpan(row)[..width].CopyTo(target.Slice(UpsamplingRadius, width));
        for (int border = 0; border < UpsamplingRadius; border++)
        {
            target[border] = target[UpsamplingRadius + MirrorIndex(border - UpsamplingRadius, width)];
            target[UpsamplingRadius + width + border] = target[UpsamplingRadius + MirrorIndex((long)width + border, width)];
        }
    }

    /// <summary>
    /// Expands the triangular weight representation into every reflected phase kernel.
    /// </summary>
    /// <param name="factor">The twofold, fourfold or eightfold factor.</param>
    /// <param name="weights">The validated 15-, 55- or 210-entry compact weight table.</param>
    /// <param name="kernels">The complete factor-squared 25-entry kernels.</param>
    private static void PrepareUpsamplingKernels(int factor, ReadOnlySpan<float> weights, Span<float> kernels)
    {
        int half = factor / 2;
        for (int y = 0; y < half; y++)
        {
            for (int x = 0; x < half; x++)
            {
                int first = ((y * factor) + x) * UpsamplingKernelArea;
                int flipX = ((y * factor) + factor - 1 - x) * UpsamplingKernelArea;
                int flipY = (((factor - 1 - y) * factor) + x) * UpsamplingKernelArea;
                int flipBoth = (((factor - 1 - y) * factor) + factor - 1 - x) * UpsamplingKernelArea;
                for (int py = 0; py < UpsamplingDiameter; py++)
                {
                    for (int px = 0; px < UpsamplingDiameter; px++)
                    {
                        int i = (UpsamplingDiameter * x) + px;
                        int j = (UpsamplingDiameter * y) + py;
                        int low = Math.Min(i, j);
                        int high = Math.Max(i, j);
                        int index = (UpsamplingDiameter * half * low) - ((low * (low - 1)) / 2) + high - low;
                        float weight = weights[index];

                        // Diagonal symmetry selects the triangular entry. Reflections reverse both the phase and its five-tap source coordinates.
                        kernels[first + (py * UpsamplingDiameter) + px] = weight;
                        kernels[flipX + (py * UpsamplingDiameter) + UpsamplingDiameter - 1 - px] = weight;
                        kernels[flipY + ((UpsamplingDiameter - 1 - py) * UpsamplingDiameter) + px] = weight;
                        kernels[flipBoth + ((UpsamplingDiameter - 1 - py) * UpsamplingDiameter) + UpsamplingDiameter - 1 - px] = weight;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Computes the original five-by-five neighborhood limits once for every source position.
    /// </summary>
    /// <param name="window">The cached source rows.</param>
    /// <param name="rows">The five mirrored row offsets.</param>
    /// <param name="minimum">The vertical minima, then the complete neighborhood minima.</param>
    /// <param name="maximum">The vertical maxima, then the complete neighborhood maxima.</param>
    /// <param name="width">The logical source width.</param>
    private static void PrepareUpsamplingBounds(
        ReadOnlySpan<float> window,
        ReadOnlySpan<int> rows,
        Span<float> minimum,
        Span<float> maximum,
        int width)
    {
        ref float source = ref MemoryMarshal.GetReference(window);
        ref float minOutput = ref MemoryMarshal.GetReference(minimum);
        ref float maxOutput = ref MemoryMarshal.GetReference(maximum);
        int x = 0;
        int length = width + (2 * UpsamplingRadius);
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (length - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                Vector512<float> a = Vector512.LoadUnsafe(ref source, (nuint)(rows[0] + x));
                Vector512<float> b = Vector512.LoadUnsafe(ref source, (nuint)(rows[1] + x));
                Vector512<float> c = Vector512.LoadUnsafe(ref source, (nuint)(rows[2] + x));
                Vector512<float> d = Vector512.LoadUnsafe(ref source, (nuint)(rows[3] + x));
                Vector512<float> e = Vector512.LoadUnsafe(ref source, (nuint)(rows[4] + x));
                Vector512.Min(e, Vector512.Min(Vector512.Min(a, b), Vector512.Min(c, d))).StoreUnsafe(ref minOutput, (nuint)x);
                Vector512.Max(e, Vector512.Max(Vector512.Max(a, b), Vector512.Max(c, d))).StoreUnsafe(ref maxOutput, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (length - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                Vector256<float> a = Vector256.LoadUnsafe(ref source, (nuint)(rows[0] + x));
                Vector256<float> b = Vector256.LoadUnsafe(ref source, (nuint)(rows[1] + x));
                Vector256<float> c = Vector256.LoadUnsafe(ref source, (nuint)(rows[2] + x));
                Vector256<float> d = Vector256.LoadUnsafe(ref source, (nuint)(rows[3] + x));
                Vector256<float> e = Vector256.LoadUnsafe(ref source, (nuint)(rows[4] + x));
                Vector256.Min(e, Vector256.Min(Vector256.Min(a, b), Vector256.Min(c, d))).StoreUnsafe(ref minOutput, (nuint)x);
                Vector256.Max(e, Vector256.Max(Vector256.Max(a, b), Vector256.Max(c, d))).StoreUnsafe(ref maxOutput, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (length - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                Vector128<float> a = Vector128.LoadUnsafe(ref source, (nuint)(rows[0] + x));
                Vector128<float> b = Vector128.LoadUnsafe(ref source, (nuint)(rows[1] + x));
                Vector128<float> c = Vector128.LoadUnsafe(ref source, (nuint)(rows[2] + x));
                Vector128<float> d = Vector128.LoadUnsafe(ref source, (nuint)(rows[3] + x));
                Vector128<float> e = Vector128.LoadUnsafe(ref source, (nuint)(rows[4] + x));
                Vector128.Min(e, Vector128.Min(Vector128.Min(a, b), Vector128.Min(c, d))).StoreUnsafe(ref minOutput, (nuint)x);
                Vector128.Max(e, Vector128.Max(Vector128.Max(a, b), Vector128.Max(c, d))).StoreUnsafe(ref maxOutput, (nuint)x);
            }
        }

        for (; x < length; x++)
        {
            float a = window[rows[0] + x];
            float b = window[rows[1] + x];
            float c = window[rows[2] + x];
            float d = window[rows[3] + x];
            float e = window[rows[4] + x];
            minimum[x] = MathF.Min(e, MathF.Min(MathF.Min(a, b), MathF.Min(c, d)));
            maximum[x] = MathF.Max(e, MathF.Max(MathF.Max(a, b), MathF.Max(c, d)));
        }

        // A horizontal result at x reads original columns x through x+4 and replaces only x.
        // Every SIMD batch loads all five vectors before storing, so future columns still contain their vertical limits.
        x = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (width - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                Vector512<float> a = Vector512.LoadUnsafe(ref minOutput, (nuint)x);
                Vector512<float> b = Vector512.LoadUnsafe(ref minOutput, (nuint)(x + 1));
                Vector512<float> c = Vector512.LoadUnsafe(ref minOutput, (nuint)(x + 2));
                Vector512<float> d = Vector512.LoadUnsafe(ref minOutput, (nuint)(x + 3));
                Vector512<float> e = Vector512.LoadUnsafe(ref minOutput, (nuint)(x + 4));
                Vector512<float> resultMin = Vector512.Min(e, Vector512.Min(Vector512.Min(a, b), Vector512.Min(c, d)));
                a = Vector512.LoadUnsafe(ref maxOutput, (nuint)x);
                b = Vector512.LoadUnsafe(ref maxOutput, (nuint)(x + 1));
                c = Vector512.LoadUnsafe(ref maxOutput, (nuint)(x + 2));
                d = Vector512.LoadUnsafe(ref maxOutput, (nuint)(x + 3));
                e = Vector512.LoadUnsafe(ref maxOutput, (nuint)(x + 4));
                Vector512<float> resultMax = Vector512.Max(e, Vector512.Max(Vector512.Max(a, b), Vector512.Max(c, d)));
                resultMin.StoreUnsafe(ref minOutput, (nuint)x);
                resultMax.StoreUnsafe(ref maxOutput, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (width - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                Vector256<float> a = Vector256.LoadUnsafe(ref minOutput, (nuint)x);
                Vector256<float> b = Vector256.LoadUnsafe(ref minOutput, (nuint)(x + 1));
                Vector256<float> c = Vector256.LoadUnsafe(ref minOutput, (nuint)(x + 2));
                Vector256<float> d = Vector256.LoadUnsafe(ref minOutput, (nuint)(x + 3));
                Vector256<float> e = Vector256.LoadUnsafe(ref minOutput, (nuint)(x + 4));
                Vector256<float> resultMin = Vector256.Min(e, Vector256.Min(Vector256.Min(a, b), Vector256.Min(c, d)));
                a = Vector256.LoadUnsafe(ref maxOutput, (nuint)x);
                b = Vector256.LoadUnsafe(ref maxOutput, (nuint)(x + 1));
                c = Vector256.LoadUnsafe(ref maxOutput, (nuint)(x + 2));
                d = Vector256.LoadUnsafe(ref maxOutput, (nuint)(x + 3));
                e = Vector256.LoadUnsafe(ref maxOutput, (nuint)(x + 4));
                Vector256<float> resultMax = Vector256.Max(e, Vector256.Max(Vector256.Max(a, b), Vector256.Max(c, d)));
                resultMin.StoreUnsafe(ref minOutput, (nuint)x);
                resultMax.StoreUnsafe(ref maxOutput, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (width - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                Vector128<float> a = Vector128.LoadUnsafe(ref minOutput, (nuint)x);
                Vector128<float> b = Vector128.LoadUnsafe(ref minOutput, (nuint)(x + 1));
                Vector128<float> c = Vector128.LoadUnsafe(ref minOutput, (nuint)(x + 2));
                Vector128<float> d = Vector128.LoadUnsafe(ref minOutput, (nuint)(x + 3));
                Vector128<float> e = Vector128.LoadUnsafe(ref minOutput, (nuint)(x + 4));
                Vector128<float> resultMin = Vector128.Min(e, Vector128.Min(Vector128.Min(a, b), Vector128.Min(c, d)));
                a = Vector128.LoadUnsafe(ref maxOutput, (nuint)x);
                b = Vector128.LoadUnsafe(ref maxOutput, (nuint)(x + 1));
                c = Vector128.LoadUnsafe(ref maxOutput, (nuint)(x + 2));
                d = Vector128.LoadUnsafe(ref maxOutput, (nuint)(x + 3));
                e = Vector128.LoadUnsafe(ref maxOutput, (nuint)(x + 4));
                Vector128<float> resultMax = Vector128.Max(e, Vector128.Max(Vector128.Max(a, b), Vector128.Max(c, d)));
                resultMin.StoreUnsafe(ref minOutput, (nuint)x);
                resultMax.StoreUnsafe(ref maxOutput, (nuint)x);
            }
        }

        for (; x < width; x++)
        {
            minimum[x] = MathF.Min(minimum[x + 4], MathF.Min(MathF.Min(minimum[x], minimum[x + 1]), MathF.Min(minimum[x + 2], minimum[x + 3])));
            maximum[x] = MathF.Max(maximum[x + 4], MathF.Max(MathF.Max(maximum[x], maximum[x + 1]), MathF.Max(maximum[x + 2], maximum[x + 3])));
        }
    }

    /// <summary>
    /// Writes one expanded output row without storing cropped horizontal phases beyond its visible extent.
    /// </summary>
    /// <param name="operation">The original rows, selected kernel and local bounds.</param>
    /// <param name="output">The visible output row.</param>
    /// <param name="factor">The number of horizontal phases.</param>
    /// <param name="phaseY">The vertical phase.</param>
    private static void UpsampleRow(in UpsamplingOperator operation, Span<float> output, int factor, int phaseY)
    {
        int x = 0;
        int complete = output.Length / factor;
        if (Vector512.IsHardwareAccelerated && complete - x >= Vector512<float>.Count)
        {
            int count = (int)(complete - x).Vector512Count<float>();
            UpsampleColumns512(in operation, output, factor, phaseY, x, count);
            x += count * Vector512<float>.Count;
        }

        if (Vector256.IsHardwareAccelerated && complete - x >= Vector256<float>.Count)
        {
            int count = (int)(complete - x).Vector256Count<float>();
            UpsampleColumns256(in operation, output, factor, phaseY, x, count);
            x += count * Vector256<float>.Count;
        }

        if (Vector128.IsHardwareAccelerated && complete - x >= Vector128<float>.Count)
        {
            int count = (int)(complete - x).Vector128Count<float>();
            UpsampleColumns128(in operation, output, factor, phaseY, x, count);
            x += count * Vector128<float>.Count;
        }

        // The final source column can have fewer than factor visible phases. Keep its clipped stores in the scalar tail.
        int sourceColumns = ((output.Length - 1) / factor) + 1;
        for (; x < sourceColumns; x++)
        {
            int phases = Math.Min(factor, output.Length - (x * factor));
            for (int phase = 0; phase < phases; phase++)
            {
                operation.Invoke(x, (phaseY * factor) + phase, out float value);
                output[(x * factor) + phase] = value;
            }
        }
    }

    /// <summary>
    /// Interleaves complete 16-column batches into consecutive output phase groups.
    /// </summary>
    /// <param name="operation">The cached source rows, phase kernels and local bounds.</param>
    /// <param name="output">The expanded output row.</param>
    /// <param name="factor">The twofold, fourfold or eightfold factor.</param>
    /// <param name="phaseY">The vertical phase.</param>
    /// <param name="start">The first source column.</param>
    /// <param name="count">The number of complete SIMD batches.</param>
    private static void UpsampleColumns512(in UpsamplingOperator operation, Span<float> output, int factor, int phaseY, int start, int count)
    {
        // Every selected phase is assigned before interleaving. Unused phase slots are never read.
        Unsafe.SkipInit(out InlineArray8<Vector512<float>> storage);
        Span<Vector512<float>> phases = storage;
        ref float destination = ref MemoryMarshal.GetReference(output);
        int x = start;
        for (int block = 0; block < count; block++, x += Vector512<float>.Count)
        {
            for (int phase = 0; phase < factor; phase++)
            {
                operation.Invoke(x, (phaseY * factor) + phase, out phases[phase]);
            }

            // Every register contains the same phase from consecutive source positions. Interleaving completes each expanded pixel group.
            ref float target = ref Unsafe.Add(ref destination, x * factor);
            switch (factor)
            {
                case 2:
                    SimdUtils.Interleave2(phases[0], phases[1], ref target);
                    break;
                case 4:
                    SimdUtils.Interleave4(phases[0], phases[1], phases[2], phases[3], ref target);
                    break;
                default:
                    SimdUtils.Interleave8(phases[0], phases[1], phases[2], phases[3], phases[4], phases[5], phases[6], phases[7], ref target);
                    break;
            }
        }
    }

    /// <summary>
    /// Interleaves complete 8-column batches into consecutive output phase groups.
    /// </summary>
    /// <param name="operation">The cached source rows, phase kernels and local bounds.</param>
    /// <param name="output">The expanded output row.</param>
    /// <param name="factor">The twofold, fourfold or eightfold factor.</param>
    /// <param name="phaseY">The vertical phase.</param>
    /// <param name="start">The first source column.</param>
    /// <param name="count">The number of complete SIMD batches.</param>
    private static void UpsampleColumns256(in UpsamplingOperator operation, Span<float> output, int factor, int phaseY, int start, int count)
    {
        // Every selected phase is assigned before interleaving. Unused phase slots are never read.
        Unsafe.SkipInit(out InlineArray8<Vector256<float>> storage);
        Span<Vector256<float>> phases = storage;
        ref float destination = ref MemoryMarshal.GetReference(output);
        int x = start;
        for (int block = 0; block < count; block++, x += Vector256<float>.Count)
        {
            for (int phase = 0; phase < factor; phase++)
            {
                operation.Invoke(x, (phaseY * factor) + phase, out phases[phase]);
            }

            // Every register contains the same phase from consecutive source positions. Interleaving completes each expanded pixel group.
            ref float target = ref Unsafe.Add(ref destination, x * factor);
            switch (factor)
            {
                case 2:
                    SimdUtils.Interleave2(phases[0], phases[1], ref target);
                    break;
                case 4:
                    SimdUtils.Interleave4(phases[0], phases[1], phases[2], phases[3], ref target);
                    break;
                default:
                    SimdUtils.Interleave8(phases[0], phases[1], phases[2], phases[3], phases[4], phases[5], phases[6], phases[7], ref target);
                    break;
            }
        }
    }

    /// <summary>
    /// Interleaves complete 4-column batches into consecutive output phase groups.
    /// </summary>
    /// <param name="operation">The cached source rows, phase kernels and local bounds.</param>
    /// <param name="output">The expanded output row.</param>
    /// <param name="factor">The twofold, fourfold or eightfold factor.</param>
    /// <param name="phaseY">The vertical phase.</param>
    /// <param name="start">The first source column.</param>
    /// <param name="count">The number of complete SIMD batches.</param>
    private static void UpsampleColumns128(in UpsamplingOperator operation, Span<float> output, int factor, int phaseY, int start, int count)
    {
        // Every selected phase is assigned before interleaving. Unused phase slots are never read.
        Unsafe.SkipInit(out InlineArray8<Vector128<float>> storage);
        Span<Vector128<float>> phases = storage;
        ref float destination = ref MemoryMarshal.GetReference(output);
        int x = start;
        for (int block = 0; block < count; block++, x += Vector128<float>.Count)
        {
            for (int phase = 0; phase < factor; phase++)
            {
                operation.Invoke(x, (phaseY * factor) + phase, out phases[phase]);
            }

            // Every register contains the same phase from consecutive source positions. Interleaving completes each expanded pixel group.
            ref float target = ref Unsafe.Add(ref destination, x * factor);
            switch (factor)
            {
                case 2:
                    SimdUtils.Interleave2(phases[0], phases[1], ref target);
                    break;
                case 4:
                    SimdUtils.Interleave4(phases[0], phases[1], phases[2], phases[3], ref target);
                    break;
                default:
                    SimdUtils.Interleave8(phases[0], phases[1], phases[2], phases[3], phases[4], phases[5], phases[6], phases[7], ref target);
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the standard compact JPEG XL phase weights without allocating metadata arrays.
    /// </summary>
    /// <param name="factor">The twofold, fourfold or eightfold factor.</param>
    /// <returns>The original single-precision defaults in triangular order.</returns>
    /// <remarks>
    /// These are the kWeights2, kWeights4 and kWeights8 defaults in CustomTransformData::VisitFields,
    /// lib/jxl/image_metadata.cc, libjxl revision 91837342b24034078780b687f57f670a7def8153.
    /// Defaults retain their specified single-precision values. Explicit custom weights are stored as half-precision values in the codestream.
    /// </remarks>
    private static ReadOnlySpan<float> GetDefaultUpsamplingWeights(int factor) => factor switch
    {
        2 =>
        [
            -0.01716200F, -0.03452303F, -0.04022174F, -0.02921014F, -0.00624645F,
            0.14111091F, 0.28896755F, 0.00278718F, -0.01610267F, 0.56661550F,
            0.03777607F, -0.01986694F, -0.03144731F, -0.01185068F, -0.00213539F,
        ],
        4 =>
        [
            -0.02419067F, -0.03491987F, -0.03693351F, -0.03094285F, -0.00529785F,
            -0.01663432F, -0.03556863F, -0.03888905F, -0.03516850F, -0.00989469F,
            0.23651958F, 0.33392945F, -0.01073543F, -0.01313181F, -0.03556694F,
            0.13048175F, 0.40103025F, 0.03951150F, -0.02077584F, 0.46914198F,
            -0.00209270F, -0.01484589F, -0.04064806F, 0.18942530F, 0.56279892F,
            0.06674400F, -0.02335494F, -0.03551682F, -0.00754830F, -0.02267919F,
            -0.02363578F, 0.00315804F, -0.03399098F, -0.01359519F, -0.00091653F,
            -0.00335467F, -0.01163294F, -0.01610294F, -0.00974088F, -0.00191622F,
            -0.01095446F, -0.03198464F, -0.04455121F, -0.02799790F, -0.00645912F,
            0.06390599F, 0.22963888F, 0.00630981F, -0.01897349F, 0.67537268F,
            0.08483369F, -0.02534994F, -0.02205197F, -0.01667999F, -0.00384443F,
        ],
        8 =>
        [
            -0.02928613F, -0.03706353F, -0.03783812F, -0.03324558F, -0.00447632F,
            -0.02519406F, -0.03752601F, -0.03901508F, -0.03663285F, -0.00646649F,
            -0.02066407F, -0.03838633F, -0.04002101F, -0.03900035F, -0.00901973F,
            -0.01626393F, -0.03954148F, -0.04046620F, -0.03979621F, -0.01224485F,
            0.29895328F, 0.35757708F, -0.02447552F, -0.01081748F, -0.04314594F,
            0.23903219F, 0.41119301F, -0.00573046F, -0.01450239F, -0.04246845F,
            0.17567618F, 0.45220643F, 0.02287757F, -0.01936783F, -0.03583255F,
            0.11572472F, 0.47416733F, 0.06284440F, -0.02685066F, 0.42720050F,
            -0.02248939F, -0.01155273F, -0.04562755F, 0.28689496F, 0.49093869F,
            -0.00007891F, -0.01545926F, -0.04562659F, 0.21238920F, 0.53980934F,
            0.03369474F, -0.02070211F, -0.03866988F, 0.14229550F, 0.56593398F,
            0.08045181F, -0.02888298F, -0.03680918F, -0.00542229F, -0.02920477F,
            -0.02788574F, -0.02118180F, -0.03942402F, -0.00775547F, -0.02433614F,
            -0.03193943F, -0.02030828F, -0.04044014F, -0.01074016F, -0.01930822F,
            -0.03620399F, -0.01974125F, -0.03919545F, -0.01456093F, -0.00045072F,
            -0.00360110F, -0.01020207F, -0.01231907F, -0.00638988F, -0.00071592F,
            -0.00279122F, -0.00957115F, -0.01288327F, -0.00730937F, -0.00107783F,
            -0.00210156F, -0.00890705F, -0.01317668F, -0.00813895F, -0.00153491F,
            -0.02128481F, -0.04173044F, -0.04831487F, -0.03293190F, -0.00525260F,
            -0.01720322F, -0.04052736F, -0.05045706F, -0.03607317F, -0.00738030F,
            -0.01341764F, -0.03965629F, -0.05151616F, -0.03814886F, -0.01005819F,
            0.18968273F, 0.33063684F, -0.01300105F, -0.01372950F, -0.04017465F,
            0.13727832F, 0.36402234F, 0.01027890F, -0.01832107F, -0.03365072F,
            0.08734506F, 0.38194295F, 0.04338228F, -0.02525993F, 0.56408126F,
            0.00458352F, -0.01648227F, -0.04887868F, 0.24585519F, 0.62026135F,
            0.04314807F, -0.02213737F, -0.04158014F, 0.16637289F, 0.65027023F,
            0.09621636F, -0.03101388F, -0.04082742F, -0.00904519F, -0.02790922F,
            -0.02117818F, 0.00798662F, -0.03995711F, -0.01243427F, -0.02231705F,
            -0.02946266F, 0.00992055F, -0.03600283F, -0.01684920F, -0.00111684F,
            -0.00411204F, -0.01297130F, -0.01723725F, -0.01022545F, -0.00165306F,
            -0.00313110F, -0.01218016F, -0.01763266F, -0.01125620F, -0.00231663F,
            -0.01374149F, -0.03797620F, -0.05142937F, -0.03117307F, -0.00581914F,
            -0.01064003F, -0.03608089F, -0.05272168F, -0.03375670F, -0.00795586F,
            0.09628104F, 0.27129991F, -0.00353779F, -0.01734151F, -0.03153981F,
            0.05686230F, 0.28500998F, 0.02230594F, -0.02374955F, 0.68214326F,
            0.05018048F, -0.02320852F, -0.04383616F, 0.18459474F, 0.71517975F,
            0.10805613F, -0.03263677F, -0.03637639F, -0.01394373F, -0.02511203F,
            -0.01728636F, 0.05407331F, -0.02867568F, -0.01893131F, -0.00240854F,
            -0.00446511F, -0.01636187F, -0.02377053F, -0.01522848F, -0.00333334F,
            -0.00819975F, -0.02964169F, -0.04499287F, -0.02745350F, -0.00612408F,
            0.02727416F, 0.19446600F, 0.00159832F, -0.02232473F, 0.74982506F,
            0.11452620F, -0.03348048F, -0.01605681F, -0.02070339F, -0.00458223F,
        ],
        _ => [],
    };

    /// <summary>
    /// Defines the normative phase convolution and its source-neighborhood bounds.
    /// </summary>
    private readonly ref struct UpsamplingOperator
    {
        private readonly ReadOnlySpan<float> window;
        private readonly ReadOnlySpan<int> rows;
        private readonly ReadOnlySpan<float> kernels;
        private readonly ReadOnlySpan<float> minimum;
        private readonly ReadOnlySpan<float> maximum;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpsamplingOperator"/> struct from borrowed row and kernel storage.
        /// </summary>
        /// <param name="window">The original physical row ring.</param>
        /// <param name="rows">The five mirrored row offsets for this source row.</param>
        /// <param name="kernels">The complete phase kernels.</param>
        /// <param name="minimum">The local five-by-five minima.</param>
        /// <param name="maximum">The local five-by-five maxima.</param>
        public UpsamplingOperator(
            ReadOnlySpan<float> window,
            ReadOnlySpan<int> rows,
            ReadOnlySpan<float> kernels,
            ReadOnlySpan<float> minimum,
            ReadOnlySpan<float> maximum)
        {
            this.window = window;
            this.rows = rows;
            this.kernels = kernels;
            this.minimum = minimum;
            this.maximum = maximum;
        }

        /// <summary>
        /// Computes one output phase sample.
        /// </summary>
        /// <param name="column">The first source column.</param>
        /// <param name="phase">The vertical phase times the factor, followed by the horizontal phase.</param>
        /// <param name="value">The convolution result, bounded by its original neighborhood.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int column, int phase, out float value)
        {
            ref float source = ref MemoryMarshal.GetReference(this.window);
            ref float weights = ref Unsafe.Add(ref MemoryMarshal.GetReference(this.kernels), phase * UpsamplingKernelArea);
            ref float row0 = ref Unsafe.Add(ref source, this.rows[0] + column);
            ref float row1 = ref Unsafe.Add(ref source, this.rows[1] + column);
            ref float row2 = ref Unsafe.Add(ref source, this.rows[2] + column);
            ref float row3 = ref Unsafe.Add(ref source, this.rows[3] + column);
            ref float row4 = ref Unsafe.Add(ref source, this.rows[4] + column);

            // Three independent sums match libjxl's tap grouping and reduce the multiply-add dependency chain.
            float acc0 = Unsafe.Add(ref row0, 0) * Unsafe.Add(ref weights, 0);
            float acc1 = Unsafe.Add(ref row0, 1) * Unsafe.Add(ref weights, 1);
            float acc2 = Unsafe.Add(ref row0, 2) * Unsafe.Add(ref weights, 2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row0, 3), Unsafe.Add(ref weights, 3), acc0);
            acc1 = float.MultiplyAddEstimate(Unsafe.Add(ref row0, 4), Unsafe.Add(ref weights, 4), acc1);
            acc2 = float.MultiplyAddEstimate(Unsafe.Add(ref row1, 0), Unsafe.Add(ref weights, 5), acc2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row1, 1), Unsafe.Add(ref weights, 6), acc0);
            acc1 = float.MultiplyAddEstimate(Unsafe.Add(ref row1, 2), Unsafe.Add(ref weights, 7), acc1);
            acc2 = float.MultiplyAddEstimate(Unsafe.Add(ref row1, 3), Unsafe.Add(ref weights, 8), acc2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row1, 4), Unsafe.Add(ref weights, 9), acc0);
            acc1 = float.MultiplyAddEstimate(Unsafe.Add(ref row2, 0), Unsafe.Add(ref weights, 10), acc1);
            acc2 = float.MultiplyAddEstimate(Unsafe.Add(ref row2, 1), Unsafe.Add(ref weights, 11), acc2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row2, 2), Unsafe.Add(ref weights, 12), acc0);
            acc1 = float.MultiplyAddEstimate(Unsafe.Add(ref row2, 3), Unsafe.Add(ref weights, 13), acc1);
            acc2 = float.MultiplyAddEstimate(Unsafe.Add(ref row2, 4), Unsafe.Add(ref weights, 14), acc2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row3, 0), Unsafe.Add(ref weights, 15), acc0);
            acc1 = float.MultiplyAddEstimate(Unsafe.Add(ref row3, 1), Unsafe.Add(ref weights, 16), acc1);
            acc2 = float.MultiplyAddEstimate(Unsafe.Add(ref row3, 2), Unsafe.Add(ref weights, 17), acc2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row3, 3), Unsafe.Add(ref weights, 18), acc0);
            acc1 = float.MultiplyAddEstimate(Unsafe.Add(ref row3, 4), Unsafe.Add(ref weights, 19), acc1);
            acc2 = float.MultiplyAddEstimate(Unsafe.Add(ref row4, 0), Unsafe.Add(ref weights, 20), acc2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row4, 1), Unsafe.Add(ref weights, 21), acc0);
            acc1 = float.MultiplyAddEstimate(Unsafe.Add(ref row4, 2), Unsafe.Add(ref weights, 22), acc1);
            acc2 = float.MultiplyAddEstimate(Unsafe.Add(ref row4, 3), Unsafe.Add(ref weights, 23), acc2);
            acc0 = float.MultiplyAddEstimate(Unsafe.Add(ref row4, 4), Unsafe.Add(ref weights, 24), acc0);

            value = ColorNumerics.ClampToFinite((acc1 + acc2) + acc0, this.minimum[column], this.maximum[column]);
        }

        /// <summary>
        /// Computes 4 independent output phase samples in unchanged source-column order.
        /// </summary>
        /// <param name="column">The first source column.</param>
        /// <param name="phase">The vertical phase times the factor, followed by the horizontal phase.</param>
        /// <param name="value">The convolution result, bounded by its original neighborhood.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int column, int phase, out Vector128<float> value)
        {
            ref float source = ref MemoryMarshal.GetReference(this.window);
            ref float weights = ref Unsafe.Add(ref MemoryMarshal.GetReference(this.kernels), phase * UpsamplingKernelArea);
            ref float row0 = ref Unsafe.Add(ref source, this.rows[0] + column);
            ref float row1 = ref Unsafe.Add(ref source, this.rows[1] + column);
            ref float row2 = ref Unsafe.Add(ref source, this.rows[2] + column);
            ref float row3 = ref Unsafe.Add(ref source, this.rows[3] + column);
            ref float row4 = ref Unsafe.Add(ref source, this.rows[4] + column);

            // Three independent sums match libjxl's tap grouping and reduce the multiply-add dependency chain.
            Vector128<float> acc0 = Vector128.LoadUnsafe(ref row0, 0U) * Vector128.Create(Unsafe.Add(ref weights, 0));
            Vector128<float> acc1 = Vector128.LoadUnsafe(ref row0, 1U) * Vector128.Create(Unsafe.Add(ref weights, 1));
            Vector128<float> acc2 = Vector128.LoadUnsafe(ref row0, 2U) * Vector128.Create(Unsafe.Add(ref weights, 2));
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row0, 3U), Vector128.Create(Unsafe.Add(ref weights, 3)), acc0);
            acc1 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row0, 4U), Vector128.Create(Unsafe.Add(ref weights, 4)), acc1);
            acc2 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row1, 0U), Vector128.Create(Unsafe.Add(ref weights, 5)), acc2);
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row1, 1U), Vector128.Create(Unsafe.Add(ref weights, 6)), acc0);
            acc1 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row1, 2U), Vector128.Create(Unsafe.Add(ref weights, 7)), acc1);
            acc2 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row1, 3U), Vector128.Create(Unsafe.Add(ref weights, 8)), acc2);
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row1, 4U), Vector128.Create(Unsafe.Add(ref weights, 9)), acc0);
            acc1 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row2, 0U), Vector128.Create(Unsafe.Add(ref weights, 10)), acc1);
            acc2 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row2, 1U), Vector128.Create(Unsafe.Add(ref weights, 11)), acc2);
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row2, 2U), Vector128.Create(Unsafe.Add(ref weights, 12)), acc0);
            acc1 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row2, 3U), Vector128.Create(Unsafe.Add(ref weights, 13)), acc1);
            acc2 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row2, 4U), Vector128.Create(Unsafe.Add(ref weights, 14)), acc2);
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row3, 0U), Vector128.Create(Unsafe.Add(ref weights, 15)), acc0);
            acc1 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row3, 1U), Vector128.Create(Unsafe.Add(ref weights, 16)), acc1);
            acc2 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row3, 2U), Vector128.Create(Unsafe.Add(ref weights, 17)), acc2);
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row3, 3U), Vector128.Create(Unsafe.Add(ref weights, 18)), acc0);
            acc1 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row3, 4U), Vector128.Create(Unsafe.Add(ref weights, 19)), acc1);
            acc2 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row4, 0U), Vector128.Create(Unsafe.Add(ref weights, 20)), acc2);
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row4, 1U), Vector128.Create(Unsafe.Add(ref weights, 21)), acc0);
            acc1 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row4, 2U), Vector128.Create(Unsafe.Add(ref weights, 22)), acc1);
            acc2 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row4, 3U), Vector128.Create(Unsafe.Add(ref weights, 23)), acc2);
            acc0 = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref row4, 4U), Vector128.Create(Unsafe.Add(ref weights, 24)), acc0);

            ref float lower = ref MemoryMarshal.GetReference(this.minimum);
            ref float upper = ref MemoryMarshal.GetReference(this.maximum);
            value = ColorNumerics.ClampToFinite(
                (acc1 + acc2) + acc0,
                Vector128.LoadUnsafe(ref lower, (nuint)column),
                Vector128.LoadUnsafe(ref upper, (nuint)column));
        }

        /// <summary>
        /// Computes 8 independent output phase samples in unchanged source-column order.
        /// </summary>
        /// <param name="column">The first source column.</param>
        /// <param name="phase">The vertical phase times the factor, followed by the horizontal phase.</param>
        /// <param name="value">The convolution result, bounded by its original neighborhood.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int column, int phase, out Vector256<float> value)
        {
            ref float source = ref MemoryMarshal.GetReference(this.window);
            ref float weights = ref Unsafe.Add(ref MemoryMarshal.GetReference(this.kernels), phase * UpsamplingKernelArea);
            ref float row0 = ref Unsafe.Add(ref source, this.rows[0] + column);
            ref float row1 = ref Unsafe.Add(ref source, this.rows[1] + column);
            ref float row2 = ref Unsafe.Add(ref source, this.rows[2] + column);
            ref float row3 = ref Unsafe.Add(ref source, this.rows[3] + column);
            ref float row4 = ref Unsafe.Add(ref source, this.rows[4] + column);

            // Three independent sums match libjxl's tap grouping and reduce the multiply-add dependency chain.
            Vector256<float> acc0 = Vector256.LoadUnsafe(ref row0, 0U) * Vector256.Create(Unsafe.Add(ref weights, 0));
            Vector256<float> acc1 = Vector256.LoadUnsafe(ref row0, 1U) * Vector256.Create(Unsafe.Add(ref weights, 1));
            Vector256<float> acc2 = Vector256.LoadUnsafe(ref row0, 2U) * Vector256.Create(Unsafe.Add(ref weights, 2));
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row0, 3U), Vector256.Create(Unsafe.Add(ref weights, 3)), acc0);
            acc1 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row0, 4U), Vector256.Create(Unsafe.Add(ref weights, 4)), acc1);
            acc2 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row1, 0U), Vector256.Create(Unsafe.Add(ref weights, 5)), acc2);
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row1, 1U), Vector256.Create(Unsafe.Add(ref weights, 6)), acc0);
            acc1 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row1, 2U), Vector256.Create(Unsafe.Add(ref weights, 7)), acc1);
            acc2 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row1, 3U), Vector256.Create(Unsafe.Add(ref weights, 8)), acc2);
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row1, 4U), Vector256.Create(Unsafe.Add(ref weights, 9)), acc0);
            acc1 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row2, 0U), Vector256.Create(Unsafe.Add(ref weights, 10)), acc1);
            acc2 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row2, 1U), Vector256.Create(Unsafe.Add(ref weights, 11)), acc2);
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row2, 2U), Vector256.Create(Unsafe.Add(ref weights, 12)), acc0);
            acc1 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row2, 3U), Vector256.Create(Unsafe.Add(ref weights, 13)), acc1);
            acc2 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row2, 4U), Vector256.Create(Unsafe.Add(ref weights, 14)), acc2);
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row3, 0U), Vector256.Create(Unsafe.Add(ref weights, 15)), acc0);
            acc1 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row3, 1U), Vector256.Create(Unsafe.Add(ref weights, 16)), acc1);
            acc2 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row3, 2U), Vector256.Create(Unsafe.Add(ref weights, 17)), acc2);
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row3, 3U), Vector256.Create(Unsafe.Add(ref weights, 18)), acc0);
            acc1 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row3, 4U), Vector256.Create(Unsafe.Add(ref weights, 19)), acc1);
            acc2 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row4, 0U), Vector256.Create(Unsafe.Add(ref weights, 20)), acc2);
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row4, 1U), Vector256.Create(Unsafe.Add(ref weights, 21)), acc0);
            acc1 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row4, 2U), Vector256.Create(Unsafe.Add(ref weights, 22)), acc1);
            acc2 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row4, 3U), Vector256.Create(Unsafe.Add(ref weights, 23)), acc2);
            acc0 = Vector256.MultiplyAddEstimate(Vector256.LoadUnsafe(ref row4, 4U), Vector256.Create(Unsafe.Add(ref weights, 24)), acc0);

            ref float lower = ref MemoryMarshal.GetReference(this.minimum);
            ref float upper = ref MemoryMarshal.GetReference(this.maximum);
            value = ColorNumerics.ClampToFinite(
                (acc1 + acc2) + acc0,
                Vector256.LoadUnsafe(ref lower, (nuint)column),
                Vector256.LoadUnsafe(ref upper, (nuint)column));
        }

        /// <summary>
        /// Computes 16 independent output phase samples in unchanged source-column order.
        /// </summary>
        /// <param name="column">The first source column.</param>
        /// <param name="phase">The vertical phase times the factor, followed by the horizontal phase.</param>
        /// <param name="value">The convolution result, bounded by its original neighborhood.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int column, int phase, out Vector512<float> value)
        {
            ref float source = ref MemoryMarshal.GetReference(this.window);
            ref float weights = ref Unsafe.Add(ref MemoryMarshal.GetReference(this.kernels), phase * UpsamplingKernelArea);
            ref float row0 = ref Unsafe.Add(ref source, this.rows[0] + column);
            ref float row1 = ref Unsafe.Add(ref source, this.rows[1] + column);
            ref float row2 = ref Unsafe.Add(ref source, this.rows[2] + column);
            ref float row3 = ref Unsafe.Add(ref source, this.rows[3] + column);
            ref float row4 = ref Unsafe.Add(ref source, this.rows[4] + column);

            // Three independent sums match libjxl's tap grouping and reduce the multiply-add dependency chain.
            Vector512<float> acc0 = Vector512.LoadUnsafe(ref row0, 0U) * Vector512.Create(Unsafe.Add(ref weights, 0));
            Vector512<float> acc1 = Vector512.LoadUnsafe(ref row0, 1U) * Vector512.Create(Unsafe.Add(ref weights, 1));
            Vector512<float> acc2 = Vector512.LoadUnsafe(ref row0, 2U) * Vector512.Create(Unsafe.Add(ref weights, 2));
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row0, 3U), Vector512.Create(Unsafe.Add(ref weights, 3)), acc0);
            acc1 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row0, 4U), Vector512.Create(Unsafe.Add(ref weights, 4)), acc1);
            acc2 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row1, 0U), Vector512.Create(Unsafe.Add(ref weights, 5)), acc2);
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row1, 1U), Vector512.Create(Unsafe.Add(ref weights, 6)), acc0);
            acc1 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row1, 2U), Vector512.Create(Unsafe.Add(ref weights, 7)), acc1);
            acc2 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row1, 3U), Vector512.Create(Unsafe.Add(ref weights, 8)), acc2);
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row1, 4U), Vector512.Create(Unsafe.Add(ref weights, 9)), acc0);
            acc1 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row2, 0U), Vector512.Create(Unsafe.Add(ref weights, 10)), acc1);
            acc2 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row2, 1U), Vector512.Create(Unsafe.Add(ref weights, 11)), acc2);
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row2, 2U), Vector512.Create(Unsafe.Add(ref weights, 12)), acc0);
            acc1 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row2, 3U), Vector512.Create(Unsafe.Add(ref weights, 13)), acc1);
            acc2 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row2, 4U), Vector512.Create(Unsafe.Add(ref weights, 14)), acc2);
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row3, 0U), Vector512.Create(Unsafe.Add(ref weights, 15)), acc0);
            acc1 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row3, 1U), Vector512.Create(Unsafe.Add(ref weights, 16)), acc1);
            acc2 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row3, 2U), Vector512.Create(Unsafe.Add(ref weights, 17)), acc2);
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row3, 3U), Vector512.Create(Unsafe.Add(ref weights, 18)), acc0);
            acc1 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row3, 4U), Vector512.Create(Unsafe.Add(ref weights, 19)), acc1);
            acc2 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row4, 0U), Vector512.Create(Unsafe.Add(ref weights, 20)), acc2);
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row4, 1U), Vector512.Create(Unsafe.Add(ref weights, 21)), acc0);
            acc1 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row4, 2U), Vector512.Create(Unsafe.Add(ref weights, 22)), acc1);
            acc2 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row4, 3U), Vector512.Create(Unsafe.Add(ref weights, 23)), acc2);
            acc0 = Vector512.MultiplyAddEstimate(Vector512.LoadUnsafe(ref row4, 4U), Vector512.Create(Unsafe.Add(ref weights, 24)), acc0);

            ref float lower = ref MemoryMarshal.GetReference(this.minimum);
            ref float upper = ref MemoryMarshal.GetReference(this.maximum);
            value = ColorNumerics.ClampToFinite(
                (acc1 + acc2) + acc0,
                Vector512.LoadUnsafe(ref lower, (nuint)column),
                Vector512.LoadUnsafe(ref upper, (nuint)column));
        }
    }
}

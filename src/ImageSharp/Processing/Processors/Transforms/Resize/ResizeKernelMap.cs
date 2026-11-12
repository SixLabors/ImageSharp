// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Provides resize kernel values from an optimized contiguous memory region.
/// </summary>
internal partial class ResizeKernelMap : IDisposable
{
    private static readonly TolerantMath TolerantMath = TolerantMath.Default;

    private readonly int sourceLength;

    private readonly double ratio;

    private readonly double scale;

    private readonly double radius;

    /// <summary>
    /// Whether each kernel starts at a multiple of 4 source samples. See <see cref="Calculate{TResampler}"/>.
    /// </summary>
    private readonly bool alignKernelStarts;

    private readonly Buffer2D<float> data;

    private readonly ResizeKernel[] kernels;

    private bool isDisposed;

    // To avoid both GC allocations, and MemoryAllocator ceremony:
    private readonly float[] tempValues;

    private ResizeKernelMap(
        MemoryAllocator memoryAllocator,
        int sourceLength,
        int destinationLength,
        int bufferHeight,
        double ratio,
        double scale,
        double radius,
        int kernelLength,
        int paddedLength,
        bool alignKernelStarts)
    {
        this.ratio = ratio;
        this.scale = scale;
        this.radius = radius;
        this.alignKernelStarts = alignKernelStarts;
        this.sourceLength = sourceLength;
        this.DestinationLength = destinationLength;
        this.MaxDiameter = kernelLength;
        this.PaddedLength = paddedLength;

        // Each row holds the weights of one kernel. Each weight is stored 4 times, once for each channel of a pixel,
        // so the weights of N pixels have the layout of N pixels. The kernels store the offsets of their rows, so the
        // buffer must be contiguous. Each row has PaddedLength weights. The clean buffer gives the weight zero to
        // the taps after the length of each kernel.
        this.data = memoryAllocator.Allocate2D<float>(this.PaddedLength * 4, bufferHeight, true, AllocationOptions.Clean);
        this.kernels = new ResizeKernel[destinationLength];
        this.tempValues = new float[this.PaddedLength];
    }

    /// <summary>
    /// Gets the length of the destination row/column
    /// </summary>
    public int DestinationLength { get; }

    /// <summary>
    /// Gets the number of source samples that each kernel covers before its start is aligned.
    /// Without aligned starts, every kernel of the map has this length.
    /// </summary>
    /// <remarks>
    /// The vertical operators convolve this number of taps. They use maps without aligned starts only.
    /// </remarks>
    public int MaxDiameter { get; }

    /// <summary>
    /// Gets the number of taps that the horizontal operators convolve for each kernel. This is a multiple of 4, and it
    /// is equal to or more than the <see cref="ResizeKernel.Length"/> of every kernel. The weights after the length of
    /// a kernel are zero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The horizontal operators read 4 taps in each step, so they convolve this length. The last kernel of a row can
    /// read up to <c>PaddedLength - MaxDiameter</c> pixels after the end of the row. Thus, a source row must have this
    /// number of pixels with finite values after its end. A weight of zero times a finite pixel is zero, so these
    /// pixels do not change the result.
    /// </para>
    /// <para>
    /// The rule holds with aligned starts too. A kernel starts at most MaxDiameter samples before the end of the
    /// source, and alignment moves the start to the left only. So the read ends at most
    /// <c>PaddedLength - MaxDiameter</c> pixels after the end of the row.
    /// </para>
    /// </remarks>
    public int PaddedLength { get; }

    /// <summary>
    /// Gets a string of information to help debugging
    /// </summary>
    public virtual string Info =>
        $"radius:{this.radius}|sourceSize:{this.sourceLength}|destinationSize:{this.DestinationLength}|ratio:{this.ratio}|scale:{this.scale}";

    /// <summary>
    /// Disposes <see cref="ResizeKernelMap"/> instance releasing it's backing buffer.
    /// </summary>
    public void Dispose()
        => this.Dispose(true);

    /// <summary>
    /// Disposes the object and frees resources for the Garbage Collector.
    /// </summary>
    /// <param name="disposing">Whether to dispose of managed and unmanaged objects.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.isDisposed)
        {
            this.isDisposed = true;

            if (disposing)
            {
                this.data.Dispose();
            }
        }
    }

    /// <summary>
    /// Returns a <see cref="ResizeKernel"/> for an index value between 0 and DestinationSize - 1.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    public ref ResizeKernel GetKernel(nuint destIdx) => ref this.kernels[(int)destIdx];

    /// <summary>
    /// Returns a read-only span of <see cref="ResizeKernel"/> over the underlying kernel data.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    public ReadOnlySpan<ResizeKernel> GetKernelSpan() => this.kernels;

    /// <summary>
    /// Gets a reference to the first float of the weight buffer. <see cref="ResizeKernel.WeightsOffset"/> is relative
    /// to this reference.
    /// </summary>
    /// <returns>The reference to the first float of the weight buffer.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public ref float GetWeightsReference() => ref MemoryMarshal.GetReference(this.data.DangerousGetSingleSpan());

    /// <summary>
    /// Gets the expanded weights of <paramref name="kernel"/>. Each weight is stored 4 times.
    /// </summary>
    /// <param name="kernel">A kernel of this map.</param>
    /// <returns>The <see cref="ResizeKernel.Length"/> * 4 expanded weights.</returns>
    public Span<float> GetValues(in ResizeKernel kernel)
        => this.data.DangerousGetSingleSpan().Slice(kernel.WeightsOffset, kernel.Length * 4);

    /// <summary>
    /// Computes the weights to apply at each pixel when resizing.
    /// </summary>
    /// <typeparam name="TResampler">The type of sampler.</typeparam>
    /// <param name="sampler">The <see cref="IResampler"/></param>
    /// <param name="destinationSize">The destination size</param>
    /// <param name="sourceSize">The source size</param>
    /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations</param>
    /// <param name="alignKernelStarts">
    /// Whether each kernel starts at a multiple of 4 source samples. Use this for maps of the horizontal pass only.
    /// See the remarks.
    /// </param>
    /// <returns>The <see cref="ResizeKernelMap"/></returns>
    /// <remarks>
    /// <para>
    /// A pixel is 16 bytes, so 4 pixels are 64 bytes: one cache line, and one <see cref="Vector512{T}"/>. The horizontal
    /// operators load 4 pixels of a source row in each step, from the start of the kernel. If the kernel starts at a
    /// pixel that is not a multiple of 4, each of these loads crosses a cache line. A load that crosses a cache line
    /// costs 2 cache accesses, and the horizontal loop is limited by its loads.
    /// </para>
    /// <para>
    /// With aligned starts, the map moves the start of each kernel to the left, to the nearest multiple of 4. It puts
    /// 0 to 3 weights of zero in front of the real weights, so the kernel gives the same products. The kernel then
    /// covers up to 3 more samples, and <see cref="PaddedLength"/> can increase by 4. If the source row starts on a
    /// 64-byte boundary, all loads of the kernel start on a cache line.
    /// </para>
    /// <para>
    /// The vertical operators convolve <see cref="MaxDiameter"/> rows from the start of each kernel, so they need
    /// maps without aligned starts.
    /// </para>
    /// </remarks>
    public static ResizeKernelMap Calculate<TResampler>(
        in TResampler sampler,
        int destinationSize,
        int sourceSize,
        MemoryAllocator memoryAllocator,
        bool alignKernelStarts)
        where TResampler : struct, IResampler
    {
        double ratio = (double)sourceSize / destinationSize;
        double scale = ratio;

        if (scale < 1)
        {
            scale = 1;
        }

        // The kernel support is the exact sampler radius in source samples. A sample outside it has the weight zero,
        // so a rounded radius only adds taps with the weight zero to each kernel.
        double radius = scale * sampler.Radius;

        // 'ratio' is a rational number.
        // Multiplying it by destSize/GCD(sourceSize, destinationSize) yields an integer, so every `period` rows
        // the destination-space sampling centers repeat their fractional alignment. `period` is the repeat length
        // in destination rows, while `sourcePeriod` is the corresponding integer offset in source pixels that
        // must be added to the kernel's left index when we reuse the same weights from a previous period.
        int gcd = Numerics.GreatestCommonDivisor(sourceSize, destinationSize);
        int period = destinationSize / gcd;
        int sourcePeriod = sourceSize / gcd;

        // the center position at i == 0:
        double center0 = (ratio - 1) * 0.5;
        double firstNonNegativeLeftVal = (radius - center0 - 1) / ratio;

        // The number of rows building a "stairway" at the top and the bottom of the kernel map
        // corresponding to the corners of the image.
        // If we do not normalize the kernel values, these rows also fit the periodic logic,
        // however, it's just simpler to calculate them separately.
        int cornerInterval = (int)TolerantMath.Ceiling(firstNonNegativeLeftVal);

        // If firstNonNegativeLeftVal was an integral value, we need firstNonNegativeLeftVal+1
        // instead of Ceiling:
        if (TolerantMath.AreEqual(firstNonNegativeLeftVal, cornerInterval))
        {
            cornerInterval++;
        }

        // All kernels of a map have the length of the longest kernel. Then the convolution loops do the same number of
        // steps for each kernel, and the operators can convolve 4 neighboring kernels in one register. The diameter
        // 2 * radius + 1 is an upper bound only. For example, every kernel of a 2x reduction has 8 samples, not 9.
        int kernelLength = 0;
        for (int i = 0; i < destinationSize; i++)
        {
            GetKernelBounds(i, ratio, radius, sourceSize, out _, out int left, out int right);
            kernelLength = Math.Max(kernelLength, right - left + 1);
        }

        // A kernel whose window passes the end of the source moves to the left (see BuildKernel). The periodic copies
        // keep the start of the kernel they copy, so the bottom corner must include each kernel that moves.
        // The left side of the kernels does not decrease, so these kernels are the last kernels of the map.
        int movedKernels = 0;
        for (int i = destinationSize - 1; i >= 0; i--)
        {
            GetKernelBounds(i, ratio, radius, sourceSize, out _, out int left, out _);
            if (left + kernelLength <= sourceSize)
            {
                break;
            }

            movedKernels++;
        }

        cornerInterval = Math.Max(cornerInterval, movedKernels);

        // The weights are stored for kernelLength taps, rounded up to a multiple of 4 for the 4-tap steps of the
        // horizontal operators.
        int paddedLength = (kernelLength + 3) & ~3;

        // The number of weight rows that one kernel of the period needs in a periodic map. Without aligned starts,
        // all copies of a kernel use one row.
        int shiftVariants = 1;

        if (alignKernelStarts)
        {
            // Each kernel covers its shift plus kernelLength samples. Find the largest shift, so that the padded
            // length holds the longest aligned kernel. BuildKernel calculates the start the same way.
            int maxShift = 0;
            for (int i = 0; i < destinationSize && maxShift < 3; i++)
            {
                GetKernelBounds(i, ratio, radius, sourceSize, out _, out int left, out _);
                maxShift = Math.Max(maxShift, Math.Min(left, sourceSize - kernelLength) & 3);
            }

            paddedLength = (kernelLength + maxShift + 3) & ~3;

            // A periodic copy of a kernel has the same weights, and starts sourcePeriod samples after the kernel it
            // copies. Its shift is start % 4. If sourcePeriod is not a multiple of 4, the shift changes from one
            // period to the next, so a copy needs the same weights after a different number of zeros.
            // The shift of period j is (s + j * sourcePeriod) % 4. This repeats after 4 / gcd(sourcePeriod, 4)
            // periods: 1, 2 or 4. So each kernel of the period needs that number of rows, one for each shift.
            // PeriodicKernelMap calculates the weights of each kernel one time, and copies them to its other rows.
            shiftVariants = 4 / Numerics.GreatestCommonDivisor(sourcePeriod, 4);
        }

        // If 'cornerInterval' is too big compared to 'period', we can't apply the periodic optimization.
        // If we don't have at least 2 periods, we go with the basic implementation:
        bool hasAtLeast2Periods = 2 * (cornerInterval + period) < destinationSize;

        // A map that has fewer periods than shift variants uses only one variant for each period. The number of
        // periods between the corners is the number of middle kernels divided by period, rounded up.
        if (hasAtLeast2Periods)
        {
            int periodCount = (destinationSize - (2 * cornerInterval) + period - 1) / period;
            shiftVariants = Math.Min(shiftVariants, periodCount);
        }

        ResizeKernelMap result = hasAtLeast2Periods
        ? new PeriodicKernelMap(
            memoryAllocator,
            sourceSize,
            destinationSize,
            ratio,
            scale,
            radius,
            kernelLength,
            paddedLength,
            alignKernelStarts,
            period,
            cornerInterval,
            sourcePeriod,
            shiftVariants)
        : new ResizeKernelMap(
            memoryAllocator,
            sourceSize,
            destinationSize,
            destinationSize,
            ratio,
            scale,
            radius,
            kernelLength,
            paddedLength,
            alignKernelStarts);

        result.Initialize(in sampler);

        return result;
    }

    /// <summary>
    /// Initializes the kernel map.
    /// </summary>
    protected virtual void Initialize<TResampler>(in TResampler sampler)
        where TResampler : struct, IResampler
    {
        for (int i = 0; i < this.DestinationLength; i++)
        {
            this.kernels[i] = this.BuildKernel(in sampler, i, i);
        }
    }

    /// <summary>
    /// Builds a <see cref="ResizeKernel"/> for the row <paramref name="destRowIndex"/> (in <see cref="kernels"/>)
    /// referencing the data at row <paramref name="dataRowIndex"/> within <see cref="data"/>,
    /// so the data reusable by other data rows.
    /// </summary>
    private ResizeKernel BuildKernel<TResampler>(in TResampler sampler, int destRowIndex, int dataRowIndex)
        where TResampler : struct, IResampler
    {
        GetKernelBounds(destRowIndex, this.ratio, this.radius, this.sourceLength, out double center, out int left, out int right);
        double scale = this.scale;

        // Every kernel covers MaxDiameter samples from start, so the convolution loops do the same number of steps for
        // every kernel. The weights of the samples from left to right go to their own positions in the kernel. The
        // other positions have the weight zero. A kernel near the end of the source starts before left, so that the
        // kernel stays in the source. Then start + MaxDiameter <= sourceLength for every kernel.
        int start = Math.Min(left, this.sourceLength - this.MaxDiameter);

        // With aligned starts, the kernel starts shift samples earlier, at a multiple of 4. The shift samples have the
        // weight zero, so the products do not change. start - shift >= 0, because shift = start % 4 <= start.
        // Calculate uses the same shift to find PaddedLength, so the kernel fits in its weight row.
        int shift = this.alignKernelStarts ? start & 3 : 0;
        int length = this.MaxDiameter + shift;
        ResizeKernel kernel = this.CreateKernel(dataRowIndex, start - shift, length);
        Span<float> kernelValues = this.tempValues.AsSpan(0, length);
        kernelValues.Clear();

        Span<float> weights = kernelValues.Slice(left - start + shift, right - left + 1);
        ref float weight = ref MemoryMarshal.GetReference(weights);
        float sum = 0;

        for (int j = left; j <= right; j++)
        {
            float value = sampler.GetValue((float)((j - center) / scale));
            sum += value;
            weight = value;
            weight = ref Unsafe.Add(ref weight, 1);
        }

        // Normalize, best to do it here rather than in the pixel loop later on.
        if (sum > 0)
        {
            TensorPrimitives.Divide(weights, sum, weights);
        }

        // Store each weight 4 times, once for each channel of a pixel.
        Span<Vector4> expanded = MemoryMarshal.Cast<float, Vector4>(this.data.DangerousGetRowSpan(dataRowIndex));
        for (int j = 0; j < kernelValues.Length; j++)
        {
            expanded[j] = new Vector4(kernelValues[j]);
        }

        return kernel;
    }

    /// <summary>
    /// Gets the center and the first and last source samples of the kernel of one destination sample.
    /// </summary>
    /// <param name="destinationIndex">The index of the destination sample.</param>
    /// <param name="ratio">The ratio of the source length to the destination length.</param>
    /// <param name="radius">The radius of the kernel in source samples.</param>
    /// <param name="sourceLength">The length of the source.</param>
    /// <param name="center">The center of the kernel in source coordinates.</param>
    /// <param name="left">The first source sample of the kernel.</param>
    /// <param name="right">The last source sample of the kernel.</param>
    private static void GetKernelBounds(int destinationIndex, double ratio, double radius, int sourceLength, out double center, out int left, out int right)
    {
        center = ((destinationIndex + .5) * ratio) - .5;

        // Keep inside bounds.
        left = (int)TolerantMath.Ceiling(center - radius);
        if (left < 0)
        {
            left = 0;
        }

        right = (int)TolerantMath.Floor(center + radius);
        if (right > sourceLength - 1)
        {
            right = sourceLength - 1;
        }
    }

    /// <summary>
    /// Returns a <see cref="ResizeKernel"/> referencing values of <see cref="data"/>
    /// at row <paramref name="dataRowIndex"/>.
    /// </summary>
    /// <param name="dataRowIndex">The row of <see cref="data"/> that holds the weights of the kernel.</param>
    /// <param name="start">The first source sample of the kernel.</param>
    /// <param name="length">The number of source samples of the kernel.</param>
    /// <returns>The kernel.</returns>
    private ResizeKernel CreateKernel(int dataRowIndex, int start, int length)
    {
        this.ValidateSizesForCreateKernel(length, dataRowIndex, start, start + length - 1);
        return new ResizeKernel(start, dataRowIndex * this.data.Width, length);
    }

    [Conditional("DEBUG")]
    private void ValidateSizesForCreateKernel(int length, int dataRowIndex, int left, int right)
    {
        if (length * 4 > this.data.Width)
        {
            throw new InvalidOperationException(
                $"Error in KernelMap.CreateKernel({dataRowIndex},{left},{right}): left > this.data.Width");
        }

        if (left < 0 || right > this.sourceLength - 1)
        {
            throw new InvalidOperationException(
                $"Error in KernelMap.CreateKernel({dataRowIndex},{left},{right}): the kernel is outside the source.");
        }
    }
}

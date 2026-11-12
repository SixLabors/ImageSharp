// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

internal static partial class JxlRestoration
{
    /// <summary>
    /// Defines one EPF pass's neighbor accumulation independently of shared row traversal and normalization.
    /// </summary>
    private interface IEpfNeighborhood
    {
        /// <summary>
        /// Gets the combined neighborhood and patch radius in physical pixels.
        /// </summary>
        public static abstract int Radius { get; }

        /// <summary>
        /// Accumulates this pass's neighbor weights and weighted component samples.
        /// </summary>
        /// <param name="operation">The borrowed original rows and channel scales.</param>
        /// <param name="x">The first output column. Each lane carries one consecutive pixel.</param>
        /// <param name="inverseSigma">The lane-specific inverse sigma, including pass and border scales.</param>
        /// <param name="accumulatedX">The running weighted X sum, initialized with the center sample.</param>
        /// <param name="accumulatedY">The running weighted Y sum, initialized with the center sample.</param>
        /// <param name="accumulatedB">The running weighted B sum, initialized with the center sample.</param>
        /// <param name="totalWeight">The running weight sum, initialized with the center weight of one.</param>
        public static abstract void Accumulate(
            in EpfOperator operation,
            int x,
            float inverseSigma,
            ref float accumulatedX,
            ref float accumulatedY,
            ref float accumulatedB,
            ref float totalWeight);

        /// <summary>
        /// Accumulates this pass's neighbor weights and weighted component samples.
        /// </summary>
        /// <param name="operation">The borrowed original rows and channel scales.</param>
        /// <param name="x">The first output column. Each lane carries one consecutive pixel.</param>
        /// <param name="inverseSigma">The lane-specific inverse sigma, including pass and border scales.</param>
        /// <param name="accumulatedX">The running weighted X sum, initialized with the center sample.</param>
        /// <param name="accumulatedY">The running weighted Y sum, initialized with the center sample.</param>
        /// <param name="accumulatedB">The running weighted B sum, initialized with the center sample.</param>
        /// <param name="totalWeight">The running weight sum, initialized with the center weight of one.</param>
        public static abstract void Accumulate(
            in EpfOperator operation,
            int x,
            Vector128<float> inverseSigma,
            ref Vector128<float> accumulatedX,
            ref Vector128<float> accumulatedY,
            ref Vector128<float> accumulatedB,
            ref Vector128<float> totalWeight);

        /// <summary>
        /// Accumulates this pass's neighbor weights and weighted component samples.
        /// </summary>
        /// <param name="operation">The borrowed original rows and channel scales.</param>
        /// <param name="x">The first output column. Each lane carries one consecutive pixel.</param>
        /// <param name="inverseSigma">The lane-specific inverse sigma, including pass and border scales.</param>
        /// <param name="accumulatedX">The running weighted X sum, initialized with the center sample.</param>
        /// <param name="accumulatedY">The running weighted Y sum, initialized with the center sample.</param>
        /// <param name="accumulatedB">The running weighted B sum, initialized with the center sample.</param>
        /// <param name="totalWeight">The running weight sum, initialized with the center weight of one.</param>
        public static abstract void Accumulate(
            in EpfOperator operation,
            int x,
            Vector256<float> inverseSigma,
            ref Vector256<float> accumulatedX,
            ref Vector256<float> accumulatedY,
            ref Vector256<float> accumulatedB,
            ref Vector256<float> totalWeight);

        /// <summary>
        /// Accumulates this pass's neighbor weights and weighted component samples.
        /// </summary>
        /// <param name="operation">The borrowed original rows and channel scales.</param>
        /// <param name="x">The first output column. Each lane carries one consecutive pixel.</param>
        /// <param name="inverseSigma">The lane-specific inverse sigma, including pass and border scales.</param>
        /// <param name="accumulatedX">The running weighted X sum, initialized with the center sample.</param>
        /// <param name="accumulatedY">The running weighted Y sum, initialized with the center sample.</param>
        /// <param name="accumulatedB">The running weighted B sum, initialized with the center sample.</param>
        /// <param name="totalWeight">The running weight sum, initialized with the center weight of one.</param>
        public static abstract void Accumulate(
            in EpfOperator operation,
            int x,
            Vector512<float> inverseSigma,
            ref Vector512<float> accumulatedX,
            ref Vector512<float> accumulatedY,
            ref Vector512<float> accumulatedB,
            ref Vector512<float> totalWeight);
    }

    /// <summary>
    /// Processes one EPF row with a fixed neighborhood, descending SIMD widths and scalar remainder.
    /// </summary>
    /// <typeparam name="TNeighborhood">The fixed neighborhood and difference-patch shape.</typeparam>
    /// <param name="operation">The cached original rows, inverse sigmas and channel scales.</param>
    /// <param name="destinationX">The X component output row.</param>
    /// <param name="destinationY">The Y component output row.</param>
    /// <param name="destinationB">The B component output row.</param>
    private static void FilterEpfRow<TNeighborhood>(EpfOperator operation, Span<float> destinationX, Span<float> destinationY, Span<float> destinationB)
        where TNeighborhood : struct, IEpfNeighborhood
    {
        ref float outputX = ref MemoryMarshal.GetReference(destinationX);
        ref float outputY = ref MemoryMarshal.GetReference(destinationY);
        ref float outputB = ref MemoryMarshal.GetReference(destinationB);
        int x = 0;

        // Lanes carry independent horizontal pixels, including different eight-pixel sigma blocks in a wide vector.
        // Batches start at multiples of their lane count. Up to eight lanes share one sigma block; sixteen lanes span two blocks.
        // Every store remains inside the logical row; padding is input-only.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (destinationX.Length - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                operation.Invoke<TNeighborhood>(x, out Vector512<float> valueX, out Vector512<float> valueY, out Vector512<float> valueB);
                valueX.StoreUnsafe(ref outputX, (nuint)x);
                valueY.StoreUnsafe(ref outputY, (nuint)x);
                valueB.StoreUnsafe(ref outputB, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (destinationX.Length - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                operation.Invoke<TNeighborhood>(x, out Vector256<float> valueX, out Vector256<float> valueY, out Vector256<float> valueB);
                valueX.StoreUnsafe(ref outputX, (nuint)x);
                valueY.StoreUnsafe(ref outputY, (nuint)x);
                valueB.StoreUnsafe(ref outputB, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (destinationX.Length - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                operation.Invoke<TNeighborhood>(x, out Vector128<float> valueX, out Vector128<float> valueY, out Vector128<float> valueB);
                valueX.StoreUnsafe(ref outputX, (nuint)x);
                valueY.StoreUnsafe(ref outputY, (nuint)x);
                valueB.StoreUnsafe(ref outputB, (nuint)x);
            }
        }

        for (; x < destinationX.Length; x++)
        {
            operation.Invoke<TNeighborhood>(x, out float valueX, out float valueY, out float valueB);
            Unsafe.Add(ref outputX, x) = valueX;
            Unsafe.Add(ref outputY, x) = valueY;
            Unsafe.Add(ref outputB, x) = valueB;
        }
    }

    /// <summary>
    /// Defines EPF pass zero: twelve neighbors inside a five-by-five diamond and a five-sample cross difference patch.
    /// </summary>
    private readonly struct Epf0Neighborhood : IEpfNeighborhood
    {
        /// <inheritdoc/>
        public static int Radius => 3;

        /// <summary>
        /// Gets the neighbor offsets in the format's weighted accumulation order.
        /// </summary>
        private static ReadOnlySpan<sbyte> Neighbors => [-2, 0, -1, -1, -1, 0, -1, 1, 0, -2, 0, -1, 0, 1, 0, 2, 1, -1, 1, 0, 1, 1, 2, 0];

        /// <summary>
        /// Gets the offsets added in order for each absolute-difference patch.
        /// </summary>
        private static ReadOnlySpan<sbyte> Patch => [0, 0, -1, 0, 0, -1, 1, 0, 0, 1];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            float inverseSigma,
            ref float accumulatedX,
            ref float accumulatedY,
            ref float accumulatedB,
            ref float totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector128<float> inverseSigma,
            ref Vector128<float> accumulatedX,
            ref Vector128<float> accumulatedY,
            ref Vector128<float> accumulatedB,
            ref Vector128<float> totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector256<float> inverseSigma,
            ref Vector256<float> accumulatedX,
            ref Vector256<float> accumulatedY,
            ref Vector256<float> accumulatedB,
            ref Vector256<float> totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector512<float> inverseSigma,
            ref Vector512<float> accumulatedX,
            ref Vector512<float> accumulatedY,
            ref Vector512<float> accumulatedB,
            ref Vector512<float> totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
    }

    /// <summary>
    /// Defines EPF pass one: four axial neighbors and a five-sample cross difference patch.
    /// </summary>
    private readonly struct Epf1Neighborhood : IEpfNeighborhood
    {
        /// <inheritdoc/>
        public static int Radius => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            float inverseSigma,
            ref float accumulatedX,
            ref float accumulatedY,
            ref float accumulatedB,
            ref float totalWeight)
            => operation.AccumulateCross(
                x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector128<float> inverseSigma,
            ref Vector128<float> accumulatedX,
            ref Vector128<float> accumulatedY,
            ref Vector128<float> accumulatedB,
            ref Vector128<float> totalWeight)
            => operation.AccumulateCross(
                x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector256<float> inverseSigma,
            ref Vector256<float> accumulatedX,
            ref Vector256<float> accumulatedY,
            ref Vector256<float> accumulatedB,
            ref Vector256<float> totalWeight)
            => operation.AccumulateCross(
                x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector512<float> inverseSigma,
            ref Vector512<float> accumulatedX,
            ref Vector512<float> accumulatedY,
            ref Vector512<float> accumulatedB,
            ref Vector512<float> totalWeight)
            => operation.AccumulateCross(
                x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
    }

    /// <summary>
    /// Defines EPF pass two: four axial neighbors compared directly with the center sample.
    /// </summary>
    private readonly struct Epf2Neighborhood : IEpfNeighborhood
    {
        /// <inheritdoc/>
        public static int Radius => 1;

        /// <summary>
        /// Gets the neighbor offsets in the format's weighted accumulation order.
        /// </summary>
        private static ReadOnlySpan<sbyte> Neighbors => [-1, 0, 0, -1, 0, 1, 1, 0];

        /// <summary>
        /// Gets the offsets added in order for each absolute-difference patch.
        /// </summary>
        private static ReadOnlySpan<sbyte> Patch => [0, 0];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            float inverseSigma,
            ref float accumulatedX,
            ref float accumulatedY,
            ref float accumulatedB,
            ref float totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector128<float> inverseSigma,
            ref Vector128<float> accumulatedX,
            ref Vector128<float> accumulatedY,
            ref Vector128<float> accumulatedB,
            ref Vector128<float> totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector256<float> inverseSigma,
            ref Vector256<float> accumulatedX,
            ref Vector256<float> accumulatedY,
            ref Vector256<float> accumulatedB,
            ref Vector256<float> totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            in EpfOperator operation,
            int x,
            Vector512<float> inverseSigma,
            ref Vector512<float> accumulatedX,
            ref Vector512<float> accumulatedY,
            ref Vector512<float> accumulatedB,
            ref Vector512<float> totalWeight)
            => operation.AccumulatePatch(
                x, Neighbors, Patch, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
    }

    /// <summary>
    /// Computes EPF patch distances and weighted outputs from one cached original row window.
    /// </summary>
    private readonly ref struct EpfOperator
    {
        private readonly ReadOnlySpan<float> samples;
        private readonly ReadOnlySpan<int> rows;
        private readonly ReadOnlySpan<float> sigma;
        private readonly ReadOnlySpan<float> multipliers;
        private readonly int componentStride;
        private readonly Vector3 channelScale;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpfOperator"/> struct without allocating or copying row samples.
        /// </summary>
        /// <param name="samples">The three original component row rings.</param>
        /// <param name="rows">Seven vertical offsets resolving mirrored physical rows.</param>
        /// <param name="componentStride">The complete float capacity of one component ring.</param>
        /// <param name="sigma">The current block row's inverse-sigma values.</param>
        /// <param name="multipliers">At least twenty-four repeated border/interior SAD multipliers.</param>
        /// <param name="channelScale">The X, Y and B absolute-difference scales.</param>
        public EpfOperator(
            ReadOnlySpan<float> samples,
            ReadOnlySpan<int> rows,
            int componentStride,
            ReadOnlySpan<float> sigma,
            ReadOnlySpan<float> multipliers,
            Vector3 channelScale)
        {
            this.samples = samples;
            this.rows = rows;
            this.sigma = sigma;
            this.multipliers = multipliers;
            this.componentStride = componentStride;
            this.channelScale = channelScale;
        }

        /// <summary>
        /// Locates one sample in the original padded rows using pass-defined neighborhood coordinates.
        /// </summary>
        /// <param name="component">The X, Y or B component index.</param>
        /// <param name="vertical">The vertical offset, from minus three through three.</param>
        /// <param name="horizontal">The physical horizontal coordinate before the three-sample padding.</param>
        /// <returns>A reference used only by scalar or unaligned SIMD loads.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref float Sample(int component, int vertical, int horizontal)
        {
            // The static neighborhood/patch pair bounds offsets to the three-sample padding. The driver provides
            // only complete vector batches, so the final loaded lane stays inside the same padded physical row.
            int offset = Unsafe.Add(ref MemoryMarshal.GetReference(this.rows), vertical + RestorationRadius);
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(this.samples), (component * this.componentStride) + offset + RestorationRadius + horizontal);
        }

        /// <summary>
        /// Computes one pixel with the selected EPF neighborhood.
        /// </summary>
        /// <typeparam name="TNeighborhood">The fixed neighbor and patch offsets.</typeparam>
        /// <param name="x">The first horizontal output coordinate.</param>
        /// <param name="outputX">The filtered X samples.</param>
        /// <param name="outputY">The filtered Y samples.</param>
        /// <param name="outputB">The filtered B samples.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke<TNeighborhood>(int x, out float outputX, out float outputY, out float outputB)
            where TNeighborhood : struct, IEpfNeighborhood
        {
            float centerX = this.Sample(0, 0, x);
            float centerY = this.Sample(1, 0, x);
            float centerB = this.Sample(2, 0, x);
            float inverseSigma = this.sigma[x / BlockSize];
            if (inverseSigma < MinimumInverseSigma)
            {
                outputX = centerX;
                outputY = centerY;
                outputB = centerB;
                return;
            }

            inverseSigma *= this.multipliers[x & (BlockSize - 1)];
            float accumulatedX = centerX;
            float accumulatedY = centerY;
            float accumulatedB = centerB;
            float totalWeight = 1F;

            TNeighborhood.Accumulate(in this, x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

            // A center weight of one makes the divisor positive. Reuse one exact float reciprocal for all components.
            float reciprocal = 1F / totalWeight;
            outputX = accumulatedX * reciprocal;
            outputY = accumulatedY * reciprocal;
            outputB = accumulatedB * reciprocal;
        }

        /// <summary>
        /// Computes 4 independent pixel lanes with the selected EPF neighborhood.
        /// </summary>
        /// <typeparam name="TNeighborhood">The fixed neighbor and patch offsets.</typeparam>
        /// <param name="x">The first horizontal output coordinate.</param>
        /// <param name="outputX">The filtered X samples.</param>
        /// <param name="outputY">The filtered Y samples.</param>
        /// <param name="outputB">The filtered B samples.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke<TNeighborhood>(int x, out Vector128<float> outputX, out Vector128<float> outputY, out Vector128<float> outputB)
            where TNeighborhood : struct, IEpfNeighborhood
        {
            Vector128<float> centerX = Vector128.LoadUnsafe(ref this.Sample(0, 0, x));
            Vector128<float> centerY = Vector128.LoadUnsafe(ref this.Sample(1, 0, x));
            Vector128<float> centerB = Vector128.LoadUnsafe(ref this.Sample(2, 0, x));

            // A four-lane batch cannot cross an eight-pixel block boundary. All lanes use this block's sigma.
            Vector128<float> inverseSigma = Vector128.Create(this.sigma[x / BlockSize]);
            if (Vector128.LessThanAll(inverseSigma, Vector128.Create(MinimumInverseSigma)))
            {
                outputX = centerX;
                outputY = centerY;
                outputB = centerB;
                return;
            }

            Vector128<float> skip = Vector128.LessThan(inverseSigma, Vector128.Create(MinimumInverseSigma));
            Vector128<float> multiplier = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(this.multipliers), (nuint)(x & (BlockSize - 1)));
            inverseSigma *= multiplier;
            Vector128<float> accumulatedX = centerX;
            Vector128<float> accumulatedY = centerY;
            Vector128<float> accumulatedB = centerB;
            Vector128<float> totalWeight = Vector128<float>.One;

            TNeighborhood.Accumulate(in this, x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

            // A center weight of one makes the divisor positive. Reuse one exact float reciprocal for all components.
            Vector128<float> reciprocal = Vector128<float>.One / totalWeight;
            outputX = Vector128.ConditionalSelect(skip, centerX, accumulatedX * reciprocal);
            outputY = Vector128.ConditionalSelect(skip, centerY, accumulatedY * reciprocal);
            outputB = Vector128.ConditionalSelect(skip, centerB, accumulatedB * reciprocal);
        }

        /// <summary>
        /// Computes 8 independent pixel lanes with the selected EPF neighborhood.
        /// </summary>
        /// <typeparam name="TNeighborhood">The fixed neighbor and patch offsets.</typeparam>
        /// <param name="x">The first horizontal output coordinate.</param>
        /// <param name="outputX">The filtered X samples.</param>
        /// <param name="outputY">The filtered Y samples.</param>
        /// <param name="outputB">The filtered B samples.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke<TNeighborhood>(int x, out Vector256<float> outputX, out Vector256<float> outputY, out Vector256<float> outputB)
            where TNeighborhood : struct, IEpfNeighborhood
        {
            Vector256<float> centerX = Vector256.LoadUnsafe(ref this.Sample(0, 0, x));
            Vector256<float> centerY = Vector256.LoadUnsafe(ref this.Sample(1, 0, x));
            Vector256<float> centerB = Vector256.LoadUnsafe(ref this.Sample(2, 0, x));

            // An eight-lane batch covers one complete sigma block, including after the wider batches.
            Vector256<float> inverseSigma = Vector256.Create(this.sigma[x / BlockSize]);
            if (Vector256.LessThanAll(inverseSigma, Vector256.Create(MinimumInverseSigma)))
            {
                outputX = centerX;
                outputY = centerY;
                outputB = centerB;
                return;
            }

            Vector256<float> skip = Vector256.LessThan(inverseSigma, Vector256.Create(MinimumInverseSigma));
            Vector256<float> multiplier = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(this.multipliers), (nuint)(x & (BlockSize - 1)));
            inverseSigma *= multiplier;
            Vector256<float> accumulatedX = centerX;
            Vector256<float> accumulatedY = centerY;
            Vector256<float> accumulatedB = centerB;
            Vector256<float> totalWeight = Vector256<float>.One;

            TNeighborhood.Accumulate(in this, x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

            // A center weight of one makes the divisor positive. Reuse one exact float reciprocal for all components.
            Vector256<float> reciprocal = Vector256<float>.One / totalWeight;
            outputX = Vector256.ConditionalSelect(skip, centerX, accumulatedX * reciprocal);
            outputY = Vector256.ConditionalSelect(skip, centerY, accumulatedY * reciprocal);
            outputB = Vector256.ConditionalSelect(skip, centerB, accumulatedB * reciprocal);
        }

        /// <summary>
        /// Computes 16 independent pixel lanes with the selected EPF neighborhood.
        /// </summary>
        /// <typeparam name="TNeighborhood">The fixed neighbor and patch offsets.</typeparam>
        /// <param name="x">The first horizontal output coordinate.</param>
        /// <param name="outputX">The filtered X samples.</param>
        /// <param name="outputY">The filtered Y samples.</param>
        /// <param name="outputB">The filtered B samples.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke<TNeighborhood>(int x, out Vector512<float> outputX, out Vector512<float> outputY, out Vector512<float> outputB)
            where TNeighborhood : struct, IEpfNeighborhood
        {
            Vector512<float> centerX = Vector512.LoadUnsafe(ref this.Sample(0, 0, x));
            Vector512<float> centerY = Vector512.LoadUnsafe(ref this.Sample(1, 0, x));
            Vector512<float> centerB = Vector512.LoadUnsafe(ref this.Sample(2, 0, x));

            // Sixteen aligned pixels cover two adjacent eight-pixel blocks. Broadcast once into each half.
            int sigmaIndex = x / BlockSize;
            Vector512<float> inverseSigma = Vector512.Create(Vector256.Create(this.sigma[sigmaIndex]), Vector256.Create(this.sigma[sigmaIndex + 1]));
            if (Vector512.LessThanAll(inverseSigma, Vector512.Create(MinimumInverseSigma)))
            {
                outputX = centerX;
                outputY = centerY;
                outputB = centerB;
                return;
            }

            Vector512<float> skip = Vector512.LessThan(inverseSigma, Vector512.Create(MinimumInverseSigma));
            Vector512<float> multiplier = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(this.multipliers), (nuint)(x & (BlockSize - 1)));
            inverseSigma *= multiplier;
            Vector512<float> accumulatedX = centerX;
            Vector512<float> accumulatedY = centerY;
            Vector512<float> accumulatedB = centerB;
            Vector512<float> totalWeight = Vector512<float>.One;

            TNeighborhood.Accumulate(in this, x, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);

            // A center weight of one makes the divisor positive. Reuse one exact float reciprocal for all components.
            Vector512<float> reciprocal = Vector512<float>.One / totalWeight;
            outputX = Vector512.ConditionalSelect(skip, centerX, accumulatedX * reciprocal);
            outputY = Vector512.ConditionalSelect(skip, centerY, accumulatedY * reciprocal);
            outputB = Vector512.ConditionalSelect(skip, centerB, accumulatedB * reciprocal);
        }

        /// <summary>
        /// Adds a pass's offset-defined difference patches and weighted neighbors in their declared order.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="neighbors">The vertical/horizontal neighbor pairs.</param>
        /// <param name="patch">The vertical/horizontal offsets in each difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulatePatch(
            int x,
            ReadOnlySpan<sbyte> neighbors,
            ReadOnlySpan<sbyte> patch,
            float inverseSigma,
            ref float accumulatedX,
            ref float accumulatedY,
            ref float accumulatedB,
            ref float totalWeight)
        {
            for (int neighbor = 0; neighbor < neighbors.Length; neighbor += 2)
            {
                int dy = neighbors[neighbor];
                int dx = neighbors[neighbor + 1];
                float sad = 0F;
                for (int component = 0; component < 3; component++)
                {
                    float distance = 0F;
                    for (int offset = 0; offset < patch.Length; offset += 2)
                    {
                        int py = patch[offset];
                        int px = patch[offset + 1];
                        float first = this.Sample(component, py, x + px);
                        float second = this.Sample(component, dy + py, x + dx + px);
                        distance += MathF.Abs(first - second);
                    }

                    sad = float.MultiplyAddEstimate(distance, this.channelScale[component], sad);
                }

                this.AccumulateNeighbor(x, dy, dx, sad, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            }
        }

        /// <summary>
        /// Forms EPF1's four cross-patch distances from shared original neighborhood samples, then accumulates their neighbors.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulateCross(
            int x,
            float inverseSigma,
            ref float accumulatedX,
            ref float accumulatedY,
            ref float accumulatedB,
            ref float totalWeight)
        {
            float sadTop = 0F;
            float sadLeft = 0F;
            float sadRight = 0F;
            float sadBottom = 0F;
            for (int component = 0; component < 3; component++)
            {
                // Native stage_epf.cc EPF1Stage compares the center cross with the crosses above, left,
                // right and below. Thirteen positions supply all four five-sample patches for one component.
                // Each SAD keeps its own addition order; shared differences do not change that order.
                float above2 = this.Sample(component, -2, x);
                float above = this.Sample(component, -1, x);
                float aboveLeft = this.Sample(component, -1, x - 1);
                float aboveRight = this.Sample(component, -1, x + 1);
                float left2 = this.Sample(component, 0, x - 2);
                float left = this.Sample(component, 0, x - 1);
                float center = this.Sample(component, 0, x);
                float right = this.Sample(component, 0, x + 1);
                float right2 = this.Sample(component, 0, x + 2);
                float belowLeft = this.Sample(component, 1, x - 1);
                float below = this.Sample(component, 1, x);
                float belowRight = this.Sample(component, 1, x + 1);
                float below2 = this.Sample(component, 2, x);

                float distanceTop = MathF.Abs(above2 - above);
                float distanceLeft = MathF.Abs(aboveLeft - above);
                float distanceRight = MathF.Abs(aboveRight - above);
                distanceLeft += MathF.Abs(left2 - left);
                distanceTop += MathF.Abs(aboveLeft - left);
                float shared = MathF.Abs(left - center);
                distanceLeft += shared;
                distanceRight += shared;
                shared = MathF.Abs(center - above);
                float distanceBottom = shared;
                distanceTop += shared;
                distanceTop += MathF.Abs(aboveRight - right);
                shared = MathF.Abs(center - right);
                distanceLeft += shared;
                distanceRight += shared;
                distanceRight += MathF.Abs(right2 - right);
                distanceBottom += MathF.Abs(belowLeft - left);
                shared = MathF.Abs(center - below);
                distanceTop += shared;
                distanceBottom += shared;
                distanceLeft += MathF.Abs(belowLeft - below);
                distanceRight += MathF.Abs(belowRight - below);
                distanceBottom += MathF.Abs(belowRight - right);
                distanceBottom += MathF.Abs(below2 - below);

                float scale = this.channelScale[component];
                sadTop = float.MultiplyAddEstimate(distanceTop, scale, sadTop);
                sadLeft = float.MultiplyAddEstimate(distanceLeft, scale, sadLeft);
                sadRight = float.MultiplyAddEstimate(distanceRight, scale, sadRight);
                sadBottom = float.MultiplyAddEstimate(distanceBottom, scale, sadBottom);
            }

            // Weight accumulation follows the format's top, left, right, bottom neighbor order.
            this.AccumulateNeighbor(x, -1, 0, sadTop, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, -1, sadLeft, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, 1, sadRight, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 1, 0, sadBottom, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
        }

        /// <summary>
        /// Adds one neighbor using the format's ordered weight bound and multiply-add accumulation.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="dy">The neighbor's vertical displacement.</param>
        /// <param name="dx">The neighbor's horizontal displacement.</param>
        /// <param name="sad">The channel-scaled absolute-difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AccumulateNeighbor(
            int x,
            int dy,
            int dx,
            float sad,
            float inverseSigma,
            ref float accumulatedX,
            ref float accumulatedY,
            ref float accumulatedB,
            ref float totalWeight)
        {
            // The native zero-flush syntax values do not affect Weight. Its ordered negative test retains
            // the native NaN behavior while bounding ordinary weights to max(0, 1 + SAD * inverseSigma).
            float weight = float.MultiplyAddEstimate(sad, inverseSigma, 1F);
            weight = weight < 0F ? 0F : weight;
            totalWeight += weight;
            accumulatedX = float.MultiplyAddEstimate(weight, this.Sample(0, dy, x + dx), accumulatedX);
            accumulatedY = float.MultiplyAddEstimate(weight, this.Sample(1, dy, x + dx), accumulatedY);
            accumulatedB = float.MultiplyAddEstimate(weight, this.Sample(2, dy, x + dx), accumulatedB);
        }

        /// <summary>
        /// Adds a pass's offset-defined difference patches and weighted neighbors in their declared order.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="neighbors">The vertical/horizontal neighbor pairs.</param>
        /// <param name="patch">The vertical/horizontal offsets in each difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulatePatch(
            int x,
            ReadOnlySpan<sbyte> neighbors,
            ReadOnlySpan<sbyte> patch,
            Vector128<float> inverseSigma,
            ref Vector128<float> accumulatedX,
            ref Vector128<float> accumulatedY,
            ref Vector128<float> accumulatedB,
            ref Vector128<float> totalWeight)
        {
            for (int neighbor = 0; neighbor < neighbors.Length; neighbor += 2)
            {
                int dy = neighbors[neighbor];
                int dx = neighbors[neighbor + 1];
                Vector128<float> sad = Vector128<float>.Zero;
                for (int component = 0; component < 3; component++)
                {
                    Vector128<float> distance = Vector128<float>.Zero;
                    for (int offset = 0; offset < patch.Length; offset += 2)
                    {
                        int py = patch[offset];
                        int px = patch[offset + 1];
                        Vector128<float> first = Vector128.LoadUnsafe(ref this.Sample(component, py, x + px));
                        Vector128<float> second = Vector128.LoadUnsafe(ref this.Sample(component, dy + py, x + dx + px));
                        distance += Vector128.Abs(first - second);
                    }

                    sad = Vector128.MultiplyAddEstimate(distance, Vector128.Create(this.channelScale[component]), sad);
                }

                this.AccumulateNeighbor(x, dy, dx, sad, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            }
        }

        /// <summary>
        /// Forms EPF1's four cross-patch distances from shared original neighborhood samples, then accumulates their neighbors.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulateCross(
            int x,
            Vector128<float> inverseSigma,
            ref Vector128<float> accumulatedX,
            ref Vector128<float> accumulatedY,
            ref Vector128<float> accumulatedB,
            ref Vector128<float> totalWeight)
        {
            Vector128<float> sadTop = Vector128<float>.Zero;
            Vector128<float> sadLeft = Vector128<float>.Zero;
            Vector128<float> sadRight = Vector128<float>.Zero;
            Vector128<float> sadBottom = Vector128<float>.Zero;
            for (int component = 0; component < 3; component++)
            {
                // Native stage_epf.cc EPF1Stage compares the center cross with the crosses above, left,
                // right and below. Thirteen positions supply all four five-sample patches for one component.
                // Each SAD keeps its own addition order; shared differences do not change that order.
                Vector128<float> above2 = Vector128.LoadUnsafe(ref this.Sample(component, -2, x));
                Vector128<float> above = Vector128.LoadUnsafe(ref this.Sample(component, -1, x));
                Vector128<float> aboveLeft = Vector128.LoadUnsafe(ref this.Sample(component, -1, x - 1));
                Vector128<float> aboveRight = Vector128.LoadUnsafe(ref this.Sample(component, -1, x + 1));
                Vector128<float> left2 = Vector128.LoadUnsafe(ref this.Sample(component, 0, x - 2));
                Vector128<float> left = Vector128.LoadUnsafe(ref this.Sample(component, 0, x - 1));
                Vector128<float> center = Vector128.LoadUnsafe(ref this.Sample(component, 0, x));
                Vector128<float> right = Vector128.LoadUnsafe(ref this.Sample(component, 0, x + 1));
                Vector128<float> right2 = Vector128.LoadUnsafe(ref this.Sample(component, 0, x + 2));
                Vector128<float> belowLeft = Vector128.LoadUnsafe(ref this.Sample(component, 1, x - 1));
                Vector128<float> below = Vector128.LoadUnsafe(ref this.Sample(component, 1, x));
                Vector128<float> belowRight = Vector128.LoadUnsafe(ref this.Sample(component, 1, x + 1));
                Vector128<float> below2 = Vector128.LoadUnsafe(ref this.Sample(component, 2, x));

                Vector128<float> distanceTop = Vector128.Abs(above2 - above);
                Vector128<float> distanceLeft = Vector128.Abs(aboveLeft - above);
                Vector128<float> distanceRight = Vector128.Abs(aboveRight - above);
                distanceLeft += Vector128.Abs(left2 - left);
                distanceTop += Vector128.Abs(aboveLeft - left);
                Vector128<float> shared = Vector128.Abs(left - center);
                distanceLeft += shared;
                distanceRight += shared;
                shared = Vector128.Abs(center - above);
                Vector128<float> distanceBottom = shared;
                distanceTop += shared;
                distanceTop += Vector128.Abs(aboveRight - right);
                shared = Vector128.Abs(center - right);
                distanceLeft += shared;
                distanceRight += shared;
                distanceRight += Vector128.Abs(right2 - right);
                distanceBottom += Vector128.Abs(belowLeft - left);
                shared = Vector128.Abs(center - below);
                distanceTop += shared;
                distanceBottom += shared;
                distanceLeft += Vector128.Abs(belowLeft - below);
                distanceRight += Vector128.Abs(belowRight - below);
                distanceBottom += Vector128.Abs(belowRight - right);
                distanceBottom += Vector128.Abs(below2 - below);

                Vector128<float> scale = Vector128.Create(this.channelScale[component]);
                sadTop = Vector128.MultiplyAddEstimate(distanceTop, scale, sadTop);
                sadLeft = Vector128.MultiplyAddEstimate(distanceLeft, scale, sadLeft);
                sadRight = Vector128.MultiplyAddEstimate(distanceRight, scale, sadRight);
                sadBottom = Vector128.MultiplyAddEstimate(distanceBottom, scale, sadBottom);
            }

            // Weight accumulation follows the format's top, left, right, bottom neighbor order.
            this.AccumulateNeighbor(x, -1, 0, sadTop, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, -1, sadLeft, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, 1, sadRight, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 1, 0, sadBottom, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
        }

        /// <summary>
        /// Adds one neighbor using the format's ordered weight bound and multiply-add accumulation.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="dy">The neighbor's vertical displacement.</param>
        /// <param name="dx">The neighbor's horizontal displacement.</param>
        /// <param name="sad">The channel-scaled absolute-difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AccumulateNeighbor(
            int x,
            int dy,
            int dx,
            Vector128<float> sad,
            Vector128<float> inverseSigma,
            ref Vector128<float> accumulatedX,
            ref Vector128<float> accumulatedY,
            ref Vector128<float> accumulatedB,
            ref Vector128<float> totalWeight)
        {
            // The native zero-flush syntax values do not affect Weight. Its ordered negative test retains
            // the native NaN behavior while bounding ordinary weights to max(0, 1 + SAD * inverseSigma).
            Vector128<float> weight = Vector128.MultiplyAddEstimate(sad, inverseSigma, Vector128<float>.One);
            weight = Vector128.ConditionalSelect(Vector128.LessThan(weight, Vector128<float>.Zero), Vector128<float>.Zero, weight);
            totalWeight += weight;
            accumulatedX = Vector128.MultiplyAddEstimate(weight, Vector128.LoadUnsafe(ref this.Sample(0, dy, x + dx)), accumulatedX);
            accumulatedY = Vector128.MultiplyAddEstimate(weight, Vector128.LoadUnsafe(ref this.Sample(1, dy, x + dx)), accumulatedY);
            accumulatedB = Vector128.MultiplyAddEstimate(weight, Vector128.LoadUnsafe(ref this.Sample(2, dy, x + dx)), accumulatedB);
        }

        /// <summary>
        /// Adds a pass's offset-defined difference patches and weighted neighbors in their declared order.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="neighbors">The vertical/horizontal neighbor pairs.</param>
        /// <param name="patch">The vertical/horizontal offsets in each difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulatePatch(
            int x,
            ReadOnlySpan<sbyte> neighbors,
            ReadOnlySpan<sbyte> patch,
            Vector256<float> inverseSigma,
            ref Vector256<float> accumulatedX,
            ref Vector256<float> accumulatedY,
            ref Vector256<float> accumulatedB,
            ref Vector256<float> totalWeight)
        {
            for (int neighbor = 0; neighbor < neighbors.Length; neighbor += 2)
            {
                int dy = neighbors[neighbor];
                int dx = neighbors[neighbor + 1];
                Vector256<float> sad = Vector256<float>.Zero;
                for (int component = 0; component < 3; component++)
                {
                    Vector256<float> distance = Vector256<float>.Zero;
                    for (int offset = 0; offset < patch.Length; offset += 2)
                    {
                        int py = patch[offset];
                        int px = patch[offset + 1];
                        Vector256<float> first = Vector256.LoadUnsafe(ref this.Sample(component, py, x + px));
                        Vector256<float> second = Vector256.LoadUnsafe(ref this.Sample(component, dy + py, x + dx + px));
                        distance += Vector256.Abs(first - second);
                    }

                    sad = Vector256.MultiplyAddEstimate(distance, Vector256.Create(this.channelScale[component]), sad);
                }

                this.AccumulateNeighbor(x, dy, dx, sad, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            }
        }

        /// <summary>
        /// Forms EPF1's four cross-patch distances from shared original neighborhood samples, then accumulates their neighbors.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulateCross(
            int x,
            Vector256<float> inverseSigma,
            ref Vector256<float> accumulatedX,
            ref Vector256<float> accumulatedY,
            ref Vector256<float> accumulatedB,
            ref Vector256<float> totalWeight)
        {
            Vector256<float> sadTop = Vector256<float>.Zero;
            Vector256<float> sadLeft = Vector256<float>.Zero;
            Vector256<float> sadRight = Vector256<float>.Zero;
            Vector256<float> sadBottom = Vector256<float>.Zero;
            for (int component = 0; component < 3; component++)
            {
                // Native stage_epf.cc EPF1Stage compares the center cross with the crosses above, left,
                // right and below. Thirteen positions supply all four five-sample patches for one component.
                // Each SAD keeps its own addition order; shared differences do not change that order.
                Vector256<float> above2 = Vector256.LoadUnsafe(ref this.Sample(component, -2, x));
                Vector256<float> above = Vector256.LoadUnsafe(ref this.Sample(component, -1, x));
                Vector256<float> aboveLeft = Vector256.LoadUnsafe(ref this.Sample(component, -1, x - 1));
                Vector256<float> aboveRight = Vector256.LoadUnsafe(ref this.Sample(component, -1, x + 1));
                Vector256<float> left2 = Vector256.LoadUnsafe(ref this.Sample(component, 0, x - 2));
                Vector256<float> left = Vector256.LoadUnsafe(ref this.Sample(component, 0, x - 1));
                Vector256<float> center = Vector256.LoadUnsafe(ref this.Sample(component, 0, x));
                Vector256<float> right = Vector256.LoadUnsafe(ref this.Sample(component, 0, x + 1));
                Vector256<float> right2 = Vector256.LoadUnsafe(ref this.Sample(component, 0, x + 2));
                Vector256<float> belowLeft = Vector256.LoadUnsafe(ref this.Sample(component, 1, x - 1));
                Vector256<float> below = Vector256.LoadUnsafe(ref this.Sample(component, 1, x));
                Vector256<float> belowRight = Vector256.LoadUnsafe(ref this.Sample(component, 1, x + 1));
                Vector256<float> below2 = Vector256.LoadUnsafe(ref this.Sample(component, 2, x));

                Vector256<float> distanceTop = Vector256.Abs(above2 - above);
                Vector256<float> distanceLeft = Vector256.Abs(aboveLeft - above);
                Vector256<float> distanceRight = Vector256.Abs(aboveRight - above);
                distanceLeft += Vector256.Abs(left2 - left);
                distanceTop += Vector256.Abs(aboveLeft - left);
                Vector256<float> shared = Vector256.Abs(left - center);
                distanceLeft += shared;
                distanceRight += shared;
                shared = Vector256.Abs(center - above);
                Vector256<float> distanceBottom = shared;
                distanceTop += shared;
                distanceTop += Vector256.Abs(aboveRight - right);
                shared = Vector256.Abs(center - right);
                distanceLeft += shared;
                distanceRight += shared;
                distanceRight += Vector256.Abs(right2 - right);
                distanceBottom += Vector256.Abs(belowLeft - left);
                shared = Vector256.Abs(center - below);
                distanceTop += shared;
                distanceBottom += shared;
                distanceLeft += Vector256.Abs(belowLeft - below);
                distanceRight += Vector256.Abs(belowRight - below);
                distanceBottom += Vector256.Abs(belowRight - right);
                distanceBottom += Vector256.Abs(below2 - below);

                Vector256<float> scale = Vector256.Create(this.channelScale[component]);
                sadTop = Vector256.MultiplyAddEstimate(distanceTop, scale, sadTop);
                sadLeft = Vector256.MultiplyAddEstimate(distanceLeft, scale, sadLeft);
                sadRight = Vector256.MultiplyAddEstimate(distanceRight, scale, sadRight);
                sadBottom = Vector256.MultiplyAddEstimate(distanceBottom, scale, sadBottom);
            }

            // Weight accumulation follows the format's top, left, right, bottom neighbor order.
            this.AccumulateNeighbor(x, -1, 0, sadTop, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, -1, sadLeft, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, 1, sadRight, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 1, 0, sadBottom, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
        }

        /// <summary>
        /// Adds one neighbor using the format's ordered weight bound and multiply-add accumulation.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="dy">The neighbor's vertical displacement.</param>
        /// <param name="dx">The neighbor's horizontal displacement.</param>
        /// <param name="sad">The channel-scaled absolute-difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AccumulateNeighbor(
            int x,
            int dy,
            int dx,
            Vector256<float> sad,
            Vector256<float> inverseSigma,
            ref Vector256<float> accumulatedX,
            ref Vector256<float> accumulatedY,
            ref Vector256<float> accumulatedB,
            ref Vector256<float> totalWeight)
        {
            // The native zero-flush syntax values do not affect Weight. Its ordered negative test retains
            // the native NaN behavior while bounding ordinary weights to max(0, 1 + SAD * inverseSigma).
            Vector256<float> weight = Vector256.MultiplyAddEstimate(sad, inverseSigma, Vector256<float>.One);
            weight = Vector256.ConditionalSelect(Vector256.LessThan(weight, Vector256<float>.Zero), Vector256<float>.Zero, weight);
            totalWeight += weight;
            accumulatedX = Vector256.MultiplyAddEstimate(weight, Vector256.LoadUnsafe(ref this.Sample(0, dy, x + dx)), accumulatedX);
            accumulatedY = Vector256.MultiplyAddEstimate(weight, Vector256.LoadUnsafe(ref this.Sample(1, dy, x + dx)), accumulatedY);
            accumulatedB = Vector256.MultiplyAddEstimate(weight, Vector256.LoadUnsafe(ref this.Sample(2, dy, x + dx)), accumulatedB);
        }

        /// <summary>
        /// Adds a pass's offset-defined difference patches and weighted neighbors in their declared order.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="neighbors">The vertical/horizontal neighbor pairs.</param>
        /// <param name="patch">The vertical/horizontal offsets in each difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulatePatch(
            int x,
            ReadOnlySpan<sbyte> neighbors,
            ReadOnlySpan<sbyte> patch,
            Vector512<float> inverseSigma,
            ref Vector512<float> accumulatedX,
            ref Vector512<float> accumulatedY,
            ref Vector512<float> accumulatedB,
            ref Vector512<float> totalWeight)
        {
            for (int neighbor = 0; neighbor < neighbors.Length; neighbor += 2)
            {
                int dy = neighbors[neighbor];
                int dx = neighbors[neighbor + 1];
                Vector512<float> sad = Vector512<float>.Zero;
                for (int component = 0; component < 3; component++)
                {
                    Vector512<float> distance = Vector512<float>.Zero;
                    for (int offset = 0; offset < patch.Length; offset += 2)
                    {
                        int py = patch[offset];
                        int px = patch[offset + 1];
                        Vector512<float> first = Vector512.LoadUnsafe(ref this.Sample(component, py, x + px));
                        Vector512<float> second = Vector512.LoadUnsafe(ref this.Sample(component, dy + py, x + dx + px));
                        distance += Vector512.Abs(first - second);
                    }

                    sad = Vector512.MultiplyAddEstimate(distance, Vector512.Create(this.channelScale[component]), sad);
                }

                this.AccumulateNeighbor(x, dy, dx, sad, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            }
        }

        /// <summary>
        /// Forms EPF1's four cross-patch distances from shared original neighborhood samples, then accumulates their neighbors.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AccumulateCross(
            int x,
            Vector512<float> inverseSigma,
            ref Vector512<float> accumulatedX,
            ref Vector512<float> accumulatedY,
            ref Vector512<float> accumulatedB,
            ref Vector512<float> totalWeight)
        {
            Vector512<float> sadTop = Vector512<float>.Zero;
            Vector512<float> sadLeft = Vector512<float>.Zero;
            Vector512<float> sadRight = Vector512<float>.Zero;
            Vector512<float> sadBottom = Vector512<float>.Zero;
            for (int component = 0; component < 3; component++)
            {
                // Native stage_epf.cc EPF1Stage compares the center cross with the crosses above, left,
                // right and below. Thirteen positions supply all four five-sample patches for one component.
                // Each SAD keeps its own addition order; shared differences do not change that order.
                Vector512<float> above2 = Vector512.LoadUnsafe(ref this.Sample(component, -2, x));
                Vector512<float> above = Vector512.LoadUnsafe(ref this.Sample(component, -1, x));
                Vector512<float> aboveLeft = Vector512.LoadUnsafe(ref this.Sample(component, -1, x - 1));
                Vector512<float> aboveRight = Vector512.LoadUnsafe(ref this.Sample(component, -1, x + 1));
                Vector512<float> left2 = Vector512.LoadUnsafe(ref this.Sample(component, 0, x - 2));
                Vector512<float> left = Vector512.LoadUnsafe(ref this.Sample(component, 0, x - 1));
                Vector512<float> center = Vector512.LoadUnsafe(ref this.Sample(component, 0, x));
                Vector512<float> right = Vector512.LoadUnsafe(ref this.Sample(component, 0, x + 1));
                Vector512<float> right2 = Vector512.LoadUnsafe(ref this.Sample(component, 0, x + 2));
                Vector512<float> belowLeft = Vector512.LoadUnsafe(ref this.Sample(component, 1, x - 1));
                Vector512<float> below = Vector512.LoadUnsafe(ref this.Sample(component, 1, x));
                Vector512<float> belowRight = Vector512.LoadUnsafe(ref this.Sample(component, 1, x + 1));
                Vector512<float> below2 = Vector512.LoadUnsafe(ref this.Sample(component, 2, x));

                Vector512<float> distanceTop = Vector512.Abs(above2 - above);
                Vector512<float> distanceLeft = Vector512.Abs(aboveLeft - above);
                Vector512<float> distanceRight = Vector512.Abs(aboveRight - above);
                distanceLeft += Vector512.Abs(left2 - left);
                distanceTop += Vector512.Abs(aboveLeft - left);
                Vector512<float> shared = Vector512.Abs(left - center);
                distanceLeft += shared;
                distanceRight += shared;
                shared = Vector512.Abs(center - above);
                Vector512<float> distanceBottom = shared;
                distanceTop += shared;
                distanceTop += Vector512.Abs(aboveRight - right);
                shared = Vector512.Abs(center - right);
                distanceLeft += shared;
                distanceRight += shared;
                distanceRight += Vector512.Abs(right2 - right);
                distanceBottom += Vector512.Abs(belowLeft - left);
                shared = Vector512.Abs(center - below);
                distanceTop += shared;
                distanceBottom += shared;
                distanceLeft += Vector512.Abs(belowLeft - below);
                distanceRight += Vector512.Abs(belowRight - below);
                distanceBottom += Vector512.Abs(belowRight - right);
                distanceBottom += Vector512.Abs(below2 - below);

                Vector512<float> scale = Vector512.Create(this.channelScale[component]);
                sadTop = Vector512.MultiplyAddEstimate(distanceTop, scale, sadTop);
                sadLeft = Vector512.MultiplyAddEstimate(distanceLeft, scale, sadLeft);
                sadRight = Vector512.MultiplyAddEstimate(distanceRight, scale, sadRight);
                sadBottom = Vector512.MultiplyAddEstimate(distanceBottom, scale, sadBottom);
            }

            // Weight accumulation follows the format's top, left, right, bottom neighbor order.
            this.AccumulateNeighbor(x, -1, 0, sadTop, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, -1, sadLeft, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 0, 1, sadRight, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
            this.AccumulateNeighbor(x, 1, 0, sadBottom, inverseSigma, ref accumulatedX, ref accumulatedY, ref accumulatedB, ref totalWeight);
        }

        /// <summary>
        /// Adds one neighbor using the format's ordered weight bound and multiply-add accumulation.
        /// </summary>
        /// <param name="x">The first output column.</param>
        /// <param name="dy">The neighbor's vertical displacement.</param>
        /// <param name="dx">The neighbor's horizontal displacement.</param>
        /// <param name="sad">The channel-scaled absolute-difference sum.</param>
        /// <param name="inverseSigma">The prepared inverse sigma for each lane.</param>
        /// <param name="accumulatedX">The running weighted X sum.</param>
        /// <param name="accumulatedY">The running weighted Y sum.</param>
        /// <param name="accumulatedB">The running weighted B sum.</param>
        /// <param name="totalWeight">The running weight sum.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AccumulateNeighbor(
            int x,
            int dy,
            int dx,
            Vector512<float> sad,
            Vector512<float> inverseSigma,
            ref Vector512<float> accumulatedX,
            ref Vector512<float> accumulatedY,
            ref Vector512<float> accumulatedB,
            ref Vector512<float> totalWeight)
        {
            // The native zero-flush syntax values do not affect Weight. Its ordered negative test retains
            // the native NaN behavior while bounding ordinary weights to max(0, 1 + SAD * inverseSigma).
            Vector512<float> weight = Vector512.MultiplyAddEstimate(sad, inverseSigma, Vector512<float>.One);
            weight = Vector512.ConditionalSelect(Vector512.LessThan(weight, Vector512<float>.Zero), Vector512<float>.Zero, weight);
            totalWeight += weight;
            accumulatedX = Vector512.MultiplyAddEstimate(weight, Vector512.LoadUnsafe(ref this.Sample(0, dy, x + dx)), accumulatedX);
            accumulatedY = Vector512.MultiplyAddEstimate(weight, Vector512.LoadUnsafe(ref this.Sample(1, dy, x + dx)), accumulatedY);
            accumulatedB = Vector512.MultiplyAddEstimate(weight, Vector512.LoadUnsafe(ref this.Sample(2, dy, x + dx)), accumulatedB);
        }
    }
}

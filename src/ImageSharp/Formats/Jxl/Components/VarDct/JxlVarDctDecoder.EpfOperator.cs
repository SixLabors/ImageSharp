// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Defines one EPF pass's fixed neighborhood and difference patch independently of row traversal and SIMD width.
    /// </summary>
    private interface IEpfNeighborhood
    {
        /// <summary>
        /// Gets the combined neighborhood and patch radius in physical pixels.
        /// </summary>
        public static abstract int Radius { get; }

        /// <summary>
        /// Gets vertical/horizontal offset pairs in native accumulation order for the weighted neighbors.
        /// </summary>
        public static abstract ReadOnlySpan<sbyte> Neighbors { get; }

        /// <summary>
        /// Gets vertical/horizontal offset pairs for one neighbor's absolute-difference patch.
        /// </summary>
        public static abstract ReadOnlySpan<sbyte> Patch { get; }
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
        // The shared gather selects each lane's sigma. Every store remains inside the logical row; padding is input-only.
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

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Neighbors => [-2, 0, -1, -1, -1, 0, -1, 1, 0, -2, 0, -1, 0, 1, 0, 2, 1, -1, 1, 0, 1, 1, 2, 0];

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Patch => [0, 0, -1, 0, 0, -1, 1, 0, 0, 1];
    }

    /// <summary>
    /// Defines EPF pass one: four axial neighbors and a five-sample cross difference patch.
    /// </summary>
    private readonly struct Epf1Neighborhood : IEpfNeighborhood
    {
        /// <inheritdoc/>
        public static int Radius => 2;

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Neighbors => [-1, 0, 0, -1, 0, 1, 1, 0];

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Patch => [-1, 0, 0, -1, 0, 0, 0, 1, 1, 0];
    }

    /// <summary>
    /// Defines EPF pass two: four axial neighbors compared directly with the center sample.
    /// </summary>
    private readonly struct Epf2Neighborhood : IEpfNeighborhood
    {
        /// <inheritdoc/>
        public static int Radius => 1;

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Neighbors => [-1, 0, 0, -1, 0, 1, 1, 0];

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Patch => [0, 0];
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
            ReadOnlySpan<sbyte> neighbors = TNeighborhood.Neighbors;
            ReadOnlySpan<sbyte> patch = TNeighborhood.Patch;
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

                // stage_epf.cc retains zero-flush parameters in syntax but its Weight threshold argument is unused.
                // Weight uses only max(0, 1 + SAD * inverseSigma). An ordered negative test preserves native NaN behavior.
                float weight = float.MultiplyAddEstimate(sad, inverseSigma, 1F);
                weight = weight < 0F ? 0F : weight;
                totalWeight += weight;
                accumulatedX = float.MultiplyAddEstimate(weight, this.Sample(0, dy, x + dx), accumulatedX);
                accumulatedY = float.MultiplyAddEstimate(weight, this.Sample(1, dy, x + dx), accumulatedY);
                accumulatedB = float.MultiplyAddEstimate(weight, this.Sample(2, dy, x + dx), accumulatedB);
            }

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
            Vector128<int> indices = (Vector128<int>.Indices + Vector128.Create(x)) >> 3;
            ref int sigmaBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.sigma));
            Vector128<float> inverseSigma = Vector128_.Gather(ref sigmaBits, indices).AsSingle();
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
            ReadOnlySpan<sbyte> neighbors = TNeighborhood.Neighbors;
            ReadOnlySpan<sbyte> patch = TNeighborhood.Patch;
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

                // stage_epf.cc retains zero-flush parameters in syntax but its Weight threshold argument is unused.
                // Weight uses only max(0, 1 + SAD * inverseSigma). An ordered negative test preserves native NaN behavior.
                Vector128<float> weight = Vector128.MultiplyAddEstimate(sad, inverseSigma, Vector128<float>.One);
                weight = Vector128.ConditionalSelect(Vector128.LessThan(weight, Vector128<float>.Zero), Vector128<float>.Zero, weight);
                totalWeight += weight;
                accumulatedX = Vector128.MultiplyAddEstimate(weight, Vector128.LoadUnsafe(ref this.Sample(0, dy, x + dx)), accumulatedX);
                accumulatedY = Vector128.MultiplyAddEstimate(weight, Vector128.LoadUnsafe(ref this.Sample(1, dy, x + dx)), accumulatedY);
                accumulatedB = Vector128.MultiplyAddEstimate(weight, Vector128.LoadUnsafe(ref this.Sample(2, dy, x + dx)), accumulatedB);
            }

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
            Vector256<int> indices = (Vector256<int>.Indices + Vector256.Create(x)) >> 3;
            ref int sigmaBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.sigma));
            Vector256<float> inverseSigma = Vector256_.Gather(ref sigmaBits, indices).AsSingle();
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
            ReadOnlySpan<sbyte> neighbors = TNeighborhood.Neighbors;
            ReadOnlySpan<sbyte> patch = TNeighborhood.Patch;
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

                // stage_epf.cc retains zero-flush parameters in syntax but its Weight threshold argument is unused.
                // Weight uses only max(0, 1 + SAD * inverseSigma). An ordered negative test preserves native NaN behavior.
                Vector256<float> weight = Vector256.MultiplyAddEstimate(sad, inverseSigma, Vector256<float>.One);
                weight = Vector256.ConditionalSelect(Vector256.LessThan(weight, Vector256<float>.Zero), Vector256<float>.Zero, weight);
                totalWeight += weight;
                accumulatedX = Vector256.MultiplyAddEstimate(weight, Vector256.LoadUnsafe(ref this.Sample(0, dy, x + dx)), accumulatedX);
                accumulatedY = Vector256.MultiplyAddEstimate(weight, Vector256.LoadUnsafe(ref this.Sample(1, dy, x + dx)), accumulatedY);
                accumulatedB = Vector256.MultiplyAddEstimate(weight, Vector256.LoadUnsafe(ref this.Sample(2, dy, x + dx)), accumulatedB);
            }

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
            Vector512<int> indices = (Vector512<int>.Indices + Vector512.Create(x)) >> 3;
            ref int sigmaBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.sigma));
            Vector512<float> inverseSigma = Vector512_.Gather(ref sigmaBits, indices).AsSingle();
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
            ReadOnlySpan<sbyte> neighbors = TNeighborhood.Neighbors;
            ReadOnlySpan<sbyte> patch = TNeighborhood.Patch;
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

                // stage_epf.cc retains zero-flush parameters in syntax but its Weight threshold argument is unused.
                // Weight uses only max(0, 1 + SAD * inverseSigma). An ordered negative test preserves native NaN behavior.
                Vector512<float> weight = Vector512.MultiplyAddEstimate(sad, inverseSigma, Vector512<float>.One);
                weight = Vector512.ConditionalSelect(Vector512.LessThan(weight, Vector512<float>.Zero), Vector512<float>.Zero, weight);
                totalWeight += weight;
                accumulatedX = Vector512.MultiplyAddEstimate(weight, Vector512.LoadUnsafe(ref this.Sample(0, dy, x + dx)), accumulatedX);
                accumulatedY = Vector512.MultiplyAddEstimate(weight, Vector512.LoadUnsafe(ref this.Sample(1, dy, x + dx)), accumulatedY);
                accumulatedB = Vector512.MultiplyAddEstimate(weight, Vector512.LoadUnsafe(ref this.Sample(2, dy, x + dx)), accumulatedB);
            }

            // A center weight of one makes the divisor positive. Reuse one exact float reciprocal for all components.
            Vector512<float> reciprocal = Vector512<float>.One / totalWeight;
            outputX = Vector512.ConditionalSelect(skip, centerX, accumulatedX * reciprocal);
            outputY = Vector512.ConditionalSelect(skip, centerY, accumulatedY * reciprocal);
            outputB = Vector512.ConditionalSelect(skip, centerB, accumulatedB * reciprocal);
        }
    }
}

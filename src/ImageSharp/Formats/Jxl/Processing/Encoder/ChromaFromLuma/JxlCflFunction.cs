// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;

/// <summary>
/// Evaluates the regularized correlation loss and its neighboring derivatives from borrowed coefficient pairs.
/// </summary>
internal readonly ref struct JxlCflFunction
{
    /// <summary>The derivative scale for the one-third residual loss.</summary>
    private const float DerivativeScale = 2F / 3F;

    /// <summary>The residual magnitude at which an observation stops contributing to the loss.</summary>
    private const float ResidualThreshold = 100F;

    /// <summary>Converts the encoded correlation adjustment to a multiplier.</summary>
    private const float InverseColorFactor = 1F / JxlChromaFromLuma.DefaultColorFactor;

    private readonly ReadOnlySpan<float> luma;
    private readonly ReadOnlySpan<float> chroma;
    private readonly float baseMultiplier;
    private readonly float distanceMultiplier;

    /// <summary>
    /// Borrows the logical observations used by one correlation search. Padding does not belong to the loss.
    /// </summary>
    /// <param name="valuesM">The luma coefficients.</param>
    /// <param name="valuesS">The matching chroma coefficients.</param>
    /// <param name="num">The number of logical coefficient pairs.</param>
    /// <param name="baseValue">The base correlation multiplier before the encoded adjustment.</param>
    /// <param name="distanceMultiplier">The regularization strength for the encoded adjustment.</param>
    public JxlCflFunction(ReadOnlySpan<float> valuesM, ReadOnlySpan<float> valuesS, int num, float baseValue, float distanceMultiplier)
    {
        this.luma = valuesM[..num];
        this.chroma = valuesS[..num];
        this.baseMultiplier = baseValue;
        this.distanceMultiplier = distanceMultiplier;
    }

    /// <summary>
    /// Computes the derivative at an adjustment and at its two finite-difference neighbors.
    /// </summary>
    /// <param name="x">The current encoded-adjustment estimate.</param>
    /// <param name="eps">The distance from the estimate to each neighboring derivative.</param>
    /// <param name="fpeps">The derivative at x + eps.</param>
    /// <param name="fmeps">The derivative at x - eps.</param>
    /// <returns>The derivative at x, including the quadratic regularization.</returns>
    public float Compute(float x, float eps, ref float fpeps, ref float fmeps)
    {
        // Each coefficient pair contributes independently to three sums. Keep all three in registers in a single pass;
        // separate tensor reductions would reload the coefficients or require materialized residual buffers.
        DerivativeOperator operation = new(this.baseMultiplier, x, eps);
        float derivative = 2F * this.distanceMultiplier * this.luma.Length * x;
        fpeps = 2F * this.distanceMultiplier * this.luma.Length * (x + eps);
        fmeps = 2F * this.distanceMultiplier * this.luma.Length * (x - eps);
        ref float luma = ref MemoryMarshal.GetReference(this.luma);
        ref float chroma = ref MemoryMarshal.GetReference(this.chroma);
        nuint length = (nuint)this.luma.Length;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> sum = Vector512<float>.Zero;
            Vector512<float> sumPlus = Vector512<float>.Zero;
            Vector512<float> sumMinus = Vector512<float>.Zero;
            nuint end = this.luma.Vector512Count<float>() * (nuint)Vector512<float>.Count;

            for (; i < end; i += (nuint)Vector512<float>.Count)
            {
                operation.Invoke(
                    Vector512.LoadUnsafe(ref luma, i),
                    Vector512.LoadUnsafe(ref chroma, i),
                    out Vector512<float> value,
                    out Vector512<float> plus,
                    out Vector512<float> minus);

                sum += value;
                sumPlus += plus;
                sumMinus += minus;
            }

            derivative += Vector512.Sum(sum);
            fpeps += Vector512.Sum(sumPlus);
            fmeps += Vector512.Sum(sumMinus);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> sum = Vector256<float>.Zero;
            Vector256<float> sumPlus = Vector256<float>.Zero;
            Vector256<float> sumMinus = Vector256<float>.Zero;
            nuint end = this.luma.Vector256Count<float>() * (nuint)Vector256<float>.Count;

            for (; i < end; i += (nuint)Vector256<float>.Count)
            {
                operation.Invoke(
                    Vector256.LoadUnsafe(ref luma, i),
                    Vector256.LoadUnsafe(ref chroma, i),
                    out Vector256<float> value,
                    out Vector256<float> plus,
                    out Vector256<float> minus);

                sum += value;
                sumPlus += plus;
                sumMinus += minus;
            }

            derivative += Vector256.Sum(sum);
            fpeps += Vector256.Sum(sumPlus);
            fmeps += Vector256.Sum(sumMinus);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> sum = Vector128<float>.Zero;
            Vector128<float> sumPlus = Vector128<float>.Zero;
            Vector128<float> sumMinus = Vector128<float>.Zero;
            nuint end = this.luma.Vector128Count<float>() * (nuint)Vector128<float>.Count;

            for (; i < end; i += (nuint)Vector128<float>.Count)
            {
                operation.Invoke(
                    Vector128.LoadUnsafe(ref luma, i),
                    Vector128.LoadUnsafe(ref chroma, i),
                    out Vector128<float> value,
                    out Vector128<float> plus,
                    out Vector128<float> minus);

                sum += value;
                sumPlus += plus;
                sumMinus += minus;
            }

            derivative += Vector128.Sum(sum);
            fpeps += Vector128.Sum(sumPlus);
            fmeps += Vector128.Sum(sumMinus);
        }

        // Smaller hardware widths consume the remainder after wider blocks. The scalar path uses the same three residuals
        // and the same central-residual cutoff; it also handles every observation when hardware intrinsics are disabled.
        for (; i < length; i++)
        {
            operation.Invoke(Unsafe.Add(ref luma, i), Unsafe.Add(ref chroma, i), out float value, out float plus, out float minus);
            derivative += value;
            fpeps += plus;
            fmeps += minus;
        }

        return derivative;
    }

    /// <summary>
    /// Evaluates the three derivatives for corresponding coefficient lanes without storage or traversal.
    /// </summary>
    private readonly struct DerivativeOperator
    {
        private readonly float baseMultiplier;
        private readonly float x;
        private readonly float xPlus;
        private readonly float xMinus;

        /// <summary>
        /// Prepares the adjustment and its two neighbors once for a coefficient pass.
        /// </summary>
        /// <param name="baseMultiplier">The base correlation multiplier.</param>
        /// <param name="x">The encoded-adjustment estimate.</param>
        /// <param name="eps">The finite-difference distance.</param>
        public DerivativeOperator(float baseMultiplier, float x, float eps)
        {
            this.baseMultiplier = baseMultiplier;
            this.x = x;
            this.xPlus = x + eps;
            this.xMinus = x - eps;
        }

        /// <summary>
        /// Evaluates one coefficient pair using the same fused residual arithmetic as the SIMD paths.
        /// </summary>
        /// <param name="luma">The luma coefficient.</param>
        /// <param name="chroma">The chroma coefficient.</param>
        /// <param name="derivative">The derivative at x.</param>
        /// <param name="plus">The derivative at x + eps.</param>
        /// <param name="minus">The derivative at x - eps.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(float luma, float chroma, out float derivative, out float plus, out float minus)
        {
            float a = InverseColorFactor * luma;
            float b = (this.baseMultiplier * luma) - chroma;
            float residual = MathF.FusedMultiplyAdd(a, this.x, b);
            float residualPlus = MathF.FusedMultiplyAdd(a, this.xPlus, b);
            float residualMinus = MathF.FusedMultiplyAdd(a, this.xMinus, b);
            float magnitude = MathF.Abs(residual);
            float scale = DerivativeScale * a;
            derivative = scale * (magnitude + 1F);
            plus = scale * (MathF.Abs(residualPlus) + 1F);
            minus = scale * (MathF.Abs(residualMinus) + 1F);
            derivative = residual < 0F ? 0F - derivative : derivative;
            plus = residualPlus < 0F ? 0F - plus : plus;
            minus = residualMinus < 0F ? 0F - minus : minus;

            // The native loss excludes all three derivatives according to the central residual, not each displaced residual.
            if (magnitude >= ResidualThreshold)
            {
                derivative = 0F;
                plus = 0F;
                minus = 0F;
            }
        }

        /// <summary>
        /// Evaluates independent coefficient pairs in 4 lanes, with no horizontal arithmetic until the caller reduces each sum.
        /// </summary>
        /// <param name="luma">The luma coefficients in their original order.</param>
        /// <param name="chroma">The matching chroma coefficients.</param>
        /// <param name="derivative">The derivatives at x.</param>
        /// <param name="plus">The derivatives at x + eps.</param>
        /// <param name="minus">The derivatives at x - eps.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector128<float> luma,
            Vector128<float> chroma,
            out Vector128<float> derivative,
            out Vector128<float> plus,
            out Vector128<float> minus)
        {
            Vector128<float> a = Vector128.Create(InverseColorFactor) * luma;
            Vector128<float> b = (Vector128.Create(this.baseMultiplier) * luma) - chroma;
            Vector128<float> residual = Vector128.FusedMultiplyAdd(a, Vector128.Create(this.x), b);
            Vector128<float> residualPlus = Vector128.FusedMultiplyAdd(a, Vector128.Create(this.xPlus), b);
            Vector128<float> residualMinus = Vector128.FusedMultiplyAdd(a, Vector128.Create(this.xMinus), b);
            Vector128<float> magnitude = Vector128.Abs(residual);
            Vector128<float> scale = Vector128.Create(DerivativeScale) * a;
            derivative = scale * (magnitude + Vector128<float>.One);
            plus = scale * (Vector128.Abs(residualPlus) + Vector128<float>.One);
            minus = scale * (Vector128.Abs(residualMinus) + Vector128<float>.One);
            derivative = Vector128.ConditionalSelect(Vector128.LessThan(residual, Vector128<float>.Zero), Vector128<float>.Zero - derivative, derivative);
            plus = Vector128.ConditionalSelect(Vector128.LessThan(residualPlus, Vector128<float>.Zero), Vector128<float>.Zero - plus, plus);
            minus = Vector128.ConditionalSelect(Vector128.LessThan(residualMinus, Vector128<float>.Zero), Vector128<float>.Zero - minus, minus);

            // Share the central-residual exclusion across the three estimates, exactly as in the scalar operation.
            Vector128<float> excluded = Vector128.GreaterThanOrEqual(magnitude, Vector128.Create(ResidualThreshold));
            derivative = Vector128.ConditionalSelect(excluded, Vector128<float>.Zero, derivative);
            plus = Vector128.ConditionalSelect(excluded, Vector128<float>.Zero, plus);
            minus = Vector128.ConditionalSelect(excluded, Vector128<float>.Zero, minus);
        }

        /// <summary>
        /// Evaluates independent coefficient pairs in 8 lanes, with no horizontal arithmetic until the caller reduces each sum.
        /// </summary>
        /// <param name="luma">The luma coefficients in their original order.</param>
        /// <param name="chroma">The matching chroma coefficients.</param>
        /// <param name="derivative">The derivatives at x.</param>
        /// <param name="plus">The derivatives at x + eps.</param>
        /// <param name="minus">The derivatives at x - eps.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector256<float> luma,
            Vector256<float> chroma,
            out Vector256<float> derivative,
            out Vector256<float> plus,
            out Vector256<float> minus)
        {
            Vector256<float> a = Vector256.Create(InverseColorFactor) * luma;
            Vector256<float> b = (Vector256.Create(this.baseMultiplier) * luma) - chroma;
            Vector256<float> residual = Vector256.FusedMultiplyAdd(a, Vector256.Create(this.x), b);
            Vector256<float> residualPlus = Vector256.FusedMultiplyAdd(a, Vector256.Create(this.xPlus), b);
            Vector256<float> residualMinus = Vector256.FusedMultiplyAdd(a, Vector256.Create(this.xMinus), b);
            Vector256<float> magnitude = Vector256.Abs(residual);
            Vector256<float> scale = Vector256.Create(DerivativeScale) * a;
            derivative = scale * (magnitude + Vector256<float>.One);
            plus = scale * (Vector256.Abs(residualPlus) + Vector256<float>.One);
            minus = scale * (Vector256.Abs(residualMinus) + Vector256<float>.One);
            derivative = Vector256.ConditionalSelect(Vector256.LessThan(residual, Vector256<float>.Zero), Vector256<float>.Zero - derivative, derivative);
            plus = Vector256.ConditionalSelect(Vector256.LessThan(residualPlus, Vector256<float>.Zero), Vector256<float>.Zero - plus, plus);
            minus = Vector256.ConditionalSelect(Vector256.LessThan(residualMinus, Vector256<float>.Zero), Vector256<float>.Zero - minus, minus);

            // Share the central-residual exclusion across the three estimates, exactly as in the scalar operation.
            Vector256<float> excluded = Vector256.GreaterThanOrEqual(magnitude, Vector256.Create(ResidualThreshold));
            derivative = Vector256.ConditionalSelect(excluded, Vector256<float>.Zero, derivative);
            plus = Vector256.ConditionalSelect(excluded, Vector256<float>.Zero, plus);
            minus = Vector256.ConditionalSelect(excluded, Vector256<float>.Zero, minus);
        }

        /// <summary>
        /// Evaluates independent coefficient pairs in 16 lanes, with no horizontal arithmetic until the caller reduces each sum.
        /// </summary>
        /// <param name="luma">The luma coefficients in their original order.</param>
        /// <param name="chroma">The matching chroma coefficients.</param>
        /// <param name="derivative">The derivatives at x.</param>
        /// <param name="plus">The derivatives at x + eps.</param>
        /// <param name="minus">The derivatives at x - eps.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector512<float> luma,
            Vector512<float> chroma,
            out Vector512<float> derivative,
            out Vector512<float> plus,
            out Vector512<float> minus)
        {
            Vector512<float> a = Vector512.Create(InverseColorFactor) * luma;
            Vector512<float> b = (Vector512.Create(this.baseMultiplier) * luma) - chroma;
            Vector512<float> residual = Vector512.FusedMultiplyAdd(a, Vector512.Create(this.x), b);
            Vector512<float> residualPlus = Vector512.FusedMultiplyAdd(a, Vector512.Create(this.xPlus), b);
            Vector512<float> residualMinus = Vector512.FusedMultiplyAdd(a, Vector512.Create(this.xMinus), b);
            Vector512<float> magnitude = Vector512.Abs(residual);
            Vector512<float> scale = Vector512.Create(DerivativeScale) * a;
            derivative = scale * (magnitude + Vector512<float>.One);
            plus = scale * (Vector512.Abs(residualPlus) + Vector512<float>.One);
            minus = scale * (Vector512.Abs(residualMinus) + Vector512<float>.One);
            derivative = Vector512.ConditionalSelect(Vector512.LessThan(residual, Vector512<float>.Zero), Vector512<float>.Zero - derivative, derivative);
            plus = Vector512.ConditionalSelect(Vector512.LessThan(residualPlus, Vector512<float>.Zero), Vector512<float>.Zero - plus, plus);
            minus = Vector512.ConditionalSelect(Vector512.LessThan(residualMinus, Vector512<float>.Zero), Vector512<float>.Zero - minus, minus);

            // Share the central-residual exclusion across the three estimates, exactly as in the scalar operation.
            Vector512<float> excluded = Vector512.GreaterThanOrEqual(magnitude, Vector512.Create(ResidualThreshold));
            derivative = Vector512.ConditionalSelect(excluded, Vector512<float>.Zero, derivative);
            plus = Vector512.ConditionalSelect(excluded, Vector512<float>.Zero, plus);
            minus = Vector512.ConditionalSelect(excluded, Vector512<float>.Zero, minus);
        }
    }
}

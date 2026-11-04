// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Applies one JPEG XL blend equation to native color or extra-channel samples.
/// </summary>
/// <remarks>
/// Patch reconstruction and original-device frame composition share the equations from libjxl alpha.cc and blending.cc.
/// The caller owns traversal and distinct input, output and scratch spans. This value owns no sample storage.
/// Codec weights retain their encoded meaning. Pixel alpha bounds and destination packing apply separately at the pixel boundary.
/// </remarks>
internal readonly struct JxlBlending
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBlending"/> struct.
    /// </summary>
    /// <param name="mode">The validated blend equation selected by the patch or frame header.</param>
    /// <param name="alphaChannel">The weight index, bounded by the header's maximum of 4096 extra channels.</param>
    /// <param name="clamp">Whether the selected equation bounds its weight or multiplicand.</param>
    public JxlBlending(BlendMode mode, ushort alphaChannel, bool clamp)
    {
        this.Mode = mode;
        this.Clamp = clamp;
        this.AlphaChannel = alphaChannel;
    }

    /// <summary>
    /// Defines the eight JPEG XL patch and frame composition equations.
    /// </summary>
    public enum BlendMode : byte
    {
        /// <summary>
        /// Retains the background samples.
        /// </summary>
        None,

        /// <summary>
        /// Replaces the background samples with foreground samples.
        /// </summary>
        Replace,

        /// <summary>
        /// Adds the foreground samples to the background samples.
        /// </summary>
        Add,

        /// <summary>
        /// Multiplies the background samples by the foreground samples.
        /// </summary>
        Multiply,

        /// <summary>
        /// Composes the foreground above the background using the selected weight channel.
        /// </summary>
        BlendAbove,

        /// <summary>
        /// Composes the background above the foreground using the selected weight channel.
        /// </summary>
        BlendBelow,

        /// <summary>
        /// Adds the weighted foreground samples to the background.
        /// </summary>
        AlphaWeightedAddAbove,

        /// <summary>
        /// Adds the weighted background samples to the foreground.
        /// </summary>
        AlphaWeightedAddBelow
    }

    /// <summary>
    /// Gets the selected blend equation.
    /// </summary>
    public BlendMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether the equation bounds its weight or multiplicand.
    /// </summary>
    public bool Clamp { get; }

    /// <summary>
    /// Gets the encoded extra-channel index supplying the equation's weight.
    /// </summary>
    public ushort AlphaChannel { get; }

    /// <summary>
    /// Prepares the selected weights for one weighted blend record without changing either input.
    /// </summary>
    /// <param name="associated">Whether the selected weight has associated-alpha semantics.</param>
    /// <param name="selectedWeight">Whether the component itself supplies the selected weight.</param>
    /// <param name="backgroundWeight">The unchanged background weights.</param>
    /// <param name="foregroundWeight">The unchanged foreground weights.</param>
    /// <param name="bounded">The bounded top weights when clamping is enabled.</param>
    /// <param name="complement">The output for one minus each top weight in an alpha blend.</param>
    /// <param name="alpha">The combined weights for an alpha blend.</param>
    /// <param name="reciprocal">The positive combined-weight reciprocals for unassociated non-weight components.</param>
    /// <remarks>
    /// Call only for weighted equations. All spans cover the same component block.
    /// Scratch spans must be distinct from one another and from both input weights.
    /// Keep these results until every component that uses these weights has reached <see cref="Apply"/>.
    /// </remarks>
    public void PrepareWeights(
        bool associated,
        bool selectedWeight,
        ReadOnlySpan<float> backgroundWeight,
        ReadOnlySpan<float> foregroundWeight,
        Span<float> bounded,
        Span<float> complement,
        Span<float> alpha,
        Span<float> reciprocal)
    {
        bool weightedAdd = this.Mode is BlendMode.AlphaWeightedAddAbove or BlendMode.AlphaWeightedAddBelow;

        if (weightedAdd && selectedWeight)
        {
            // Weighted addition copies the selected weight instead of multiplying it by itself.
            return;
        }

        bool above = this.Mode is BlendMode.BlendAbove or BlendMode.AlphaWeightedAddAbove;
        ReadOnlySpan<float> topWeight = above ? foregroundWeight : backgroundWeight;
        ReadOnlySpan<float> bottomWeight = above ? backgroundWeight : foregroundWeight;

        if (this.Clamp)
        {
            // Only the top weight is bounded. Keep both original planes unchanged for other channels.
            topWeight.CopyTo(bounded);
            ColorNumerics.ClampToFinite(bounded, 0F, 1F);
            topWeight = bounded;
        }

        if (weightedAdd)
        {
            return;
        }

        // libjxl alpha.cc combines weights as 1 - (1 - top) * (1 - bottom).
        // Keep these separate operations so every component uses the same rounding order.
        TensorPrimitives.Subtract(1F, topWeight, complement);
        TensorPrimitives.Subtract(1F, bottomWeight, alpha);
        TensorPrimitives.Multiply(complement, alpha, alpha);
        TensorPrimitives.Subtract(1F, alpha, alpha);

        if (!associated && !selectedWeight)
        {
            TensorPrimitives_.Invoke(alpha, reciprocal, default(PositiveReciprocalOperator));
        }
    }

    /// <summary>
    /// Combines one component block using its prepared weights without overwriting either input.
    /// </summary>
    /// <param name="associated">Whether the selected weight has associated-alpha semantics.</param>
    /// <param name="selectedWeight">Whether this component itself supplies the selected weight.</param>
    /// <param name="background">The unchanged background samples.</param>
    /// <param name="foreground">The unchanged foreground samples.</param>
    /// <param name="backgroundWeight">The selected unchanged background weights, when needed.</param>
    /// <param name="foregroundWeight">The selected unchanged foreground weights, when needed.</param>
    /// <param name="destination">The separate output block.</param>
    /// <param name="bounded">The prepared bounded weights, or scratch for bounded foreground samples in a multiplicative blend.</param>
    /// <param name="complement">The prepared values of one minus each top weight.</param>
    /// <param name="alpha">The prepared combined weights, also used by color blends.</param>
    /// <param name="contribution">Scratch for the unassociated bottom contribution.</param>
    /// <param name="reciprocal">The prepared positive combined-weight reciprocals for unassociated non-weight components.</param>
    /// <remarks>
    /// Weighted equations consume the results of <see cref="PrepareWeights"/> for the same block and association mode.
    /// All spans cover that block. The output and scratch spans must be distinct from both inputs.
    /// Keep input weights unchanged until all components that use those weights have been calculated.
    /// </remarks>
    public void Apply(
        bool associated,
        bool selectedWeight,
        ReadOnlySpan<float> background,
        ReadOnlySpan<float> foreground,
        ReadOnlySpan<float> backgroundWeight,
        ReadOnlySpan<float> foregroundWeight,
        Span<float> destination,
        Span<float> bounded,
        ReadOnlySpan<float> complement,
        ReadOnlySpan<float> alpha,
        Span<float> contribution,
        ReadOnlySpan<float> reciprocal)
    {
        switch (this.Mode)
        {
            case BlendMode.None:
                background.CopyTo(destination);
                return;
            case BlendMode.Replace:
                foreground.CopyTo(destination);
                return;
            case BlendMode.Add:
                TensorPrimitives.Add(background, foreground, destination);
                return;
            case BlendMode.Multiply:
                if (this.Clamp)
                {
                    foreground.CopyTo(bounded);
                    ColorNumerics.ClampToFinite(bounded, 0F, 1F);
                    foreground = bounded;
                }

                TensorPrimitives.Multiply(background, foreground, destination);
                return;
            case BlendMode.AlphaWeightedAddAbove:
            case BlendMode.AlphaWeightedAddBelow:
                if (selectedWeight)
                {
                    // The weight is not weighted a second time. Above keeps the destination
                    // weight; below takes the foreground weight, matching alpha.cc's alias rule.
                    (this.Mode == BlendMode.AlphaWeightedAddAbove ? background : foreground).CopyTo(destination);
                    return;
                }

                ReadOnlySpan<float> weight = this.Mode == BlendMode.AlphaWeightedAddAbove ? foregroundWeight : backgroundWeight;
                if (this.Clamp)
                {
                    weight = bounded;
                }

                if (this.Mode == BlendMode.AlphaWeightedAddAbove)
                {
                    TensorPrimitives.MultiplyAddEstimate(foreground, weight, background, destination);
                }
                else
                {
                    TensorPrimitives.MultiplyAddEstimate(background, weight, foreground, destination);
                }

                return;
        }

        // Only the two validated alpha-blend modes reach this point. Below reverses top and bottom;
        // PrepareWeights has already bounded the selected top weight when the record requests it.
        bool above = this.Mode == BlendMode.BlendAbove;
        ReadOnlySpan<float> top = above ? foreground : background;
        ReadOnlySpan<float> bottom = above ? background : foreground;
        ReadOnlySpan<float> topWeight = this.Clamp ? bounded : above ? foregroundWeight : backgroundWeight;
        ReadOnlySpan<float> bottomWeight = above ? backgroundWeight : foregroundWeight;

        if (selectedWeight)
        {
            alpha.CopyTo(destination);
        }
        else if (associated)
        {
            TensorPrimitives.MultiplyAddEstimate(bottom, complement, top, destination);
        }
        else
        {
            // Retain the reference's product order before division. A nonpositive combined weight
            // gives a zero reciprocal; this is codec reconstruction, not pixel unassociation.
            TensorPrimitives.Multiply(bottomWeight, bottom, contribution);
            TensorPrimitives.Multiply(contribution, complement, contribution);
            TensorPrimitives.Multiply(topWeight, top, destination);
            TensorPrimitives.Add(destination, contribution, destination);
            TensorPrimitives.Multiply(destination, reciprocal, destination);
        }
    }

    /// <summary>
    /// Computes reciprocals only for positive combined reconstruction weights.
    /// </summary>
    /// <remarks>
    /// Each SIMD lane is an independent weight. Shared tensor traversal selects the width and
    /// applies the same scalar equation to remainders; the operation does not interpret pixel layout.
    /// </remarks>
    private readonly struct PositiveReciprocalOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value) => value > 0F ? 1F / value : 0F;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => Vector128.ConditionalSelect(Vector128.GreaterThan(value, Vector128<float>.Zero), Vector128<float>.One / value, Vector128<float>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => Vector256.ConditionalSelect(Vector256.GreaterThan(value, Vector256<float>.Zero), Vector256<float>.One / value, Vector256<float>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => Vector512.ConditionalSelect(Vector512.GreaterThan(value, Vector512<float>.Zero), Vector512<float>.One / value, Vector512<float>.Zero);
    }
}

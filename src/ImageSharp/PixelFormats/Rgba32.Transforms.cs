// <copyright file="Rgba32.Transforms.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <content>
    /// Provides operators and composition algorithms.
    /// </content>
    public partial struct Rgba32
    {
        /// <summary>
        /// Adds the second color to the first.
        /// </summary>
        /// <param name="left">The first source color.</param>
        /// <param name="right">The second source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 operator +(Rgba32 left, Rgba32 right)
        {
            Vector4 add = left.ToVector4() + right.ToVector4();
            return PackNew(ref add);
        }

        /// <summary>
        /// Subtracts the second color from the first.
        /// </summary>
        /// <param name="left">The first source color.</param>
        /// <param name="right">The second source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 operator -(Rgba32 left, Rgba32 right)
        {
            Vector4 sub = left.ToVector4() - right.ToVector4();
            return PackNew(ref sub);
        }

        /// <summary>
        /// The blending formula simply selects the source color.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Normal(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 normal = Vector4BlendTransforms.Normal(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref normal);
        }

        /// <summary>
        /// Blends two colors by multiplication.
        /// <remarks>
        /// The source color is multiplied by the destination color and replaces the destination.
        /// The resultant color is always at least as dark as either the source or destination color.
        /// Multiplying any color with black results in black. Multiplying any color with white preserves the
        /// original color.
        /// </remarks>
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Multiply(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 multiply = Vector4BlendTransforms.Multiply(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref multiply);
        }

        /// <summary>
        /// Multiplies the complements of the backdrop and source color values, then complements the result.
        /// <remarks>
        /// The result color is always at least as light as either of the two constituent colors. Screening any
        /// color with white produces white; screening with black leaves the original color unchanged.
        /// The effect is similar to projecting multiple photographic slides simultaneously onto a single screen.
        /// </remarks>
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Screen(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 subtract = Vector4BlendTransforms.Screen(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref subtract);
        }

        /// <summary>
        /// Multiplies or screens the colors, depending on the source color value. The effect is similar to
        /// shining a harsh spotlight on the backdrop.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 HardLight(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 hardlight = Vector4BlendTransforms.HardLight(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref hardlight);
        }

        /// <summary>
        /// Multiplies or screens the colors, depending on the backdrop color value.
        /// <remarks>
        /// Source colors overlay the backdrop while preserving its highlights and shadows.
        /// The backdrop color is not replaced but is mixed with the source color to reflect the lightness or darkness
        /// of the backdrop.
        /// </remarks>
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Overlay(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 overlay = Vector4BlendTransforms.Overlay(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref overlay);
        }

        /// <summary>
        /// Selects the darker of the backdrop and source colors.
        /// The backdrop is replaced with the source where the source is darker; otherwise, it is left unchanged.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Darken(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 darken = Vector4BlendTransforms.Darken(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref darken);
        }

        /// <summary>
        /// Selects the lighter of the backdrop and source colors.
        /// The backdrop is replaced with the source where the source is lighter; otherwise, it is left unchanged.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Lighten(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 lighten = Vector4BlendTransforms.Lighten(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref lighten);
        }

        /// <summary>
        /// Darkens or lightens the colors, depending on the source color value. The effect is similar to shining
        /// a diffused spotlight on the backdrop.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 SoftLight(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 softlight = Vector4BlendTransforms.SoftLight(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref softlight);
        }

        /// <summary>
        /// Brightens the backdrop color to reflect the source color. Painting with black produces no changes.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 ColorDodge(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 dodge = Vector4BlendTransforms.Dodge(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref dodge);
        }

        /// <summary>
        /// Darkens the backdrop color to reflect the source color. Painting with white produces no change.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 ColorBurn(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 burn = Vector4BlendTransforms.Burn(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref burn);
        }

        /// <summary>
        /// Subtracts the darker of the two constituent colors from the lighter color.
        /// Painting with white inverts the backdrop color; painting with black produces no change.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Difference(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 difference = Vector4BlendTransforms.Difference(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref difference);
        }

        /// <summary>
        /// Produces an effect similar to that of the <see cref="Difference"/> mode but lower in contrast. Painting with white
        /// inverts the backdrop color; painting with black produces no change
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 Exclusion(Rgba32 backdrop, Rgba32 source)
        {
            Vector4 exclusion = Vector4BlendTransforms.Exclusion(backdrop.ToVector4(), source.ToVector4());
            return PackNew(ref exclusion);
        }

        /// <summary>
        /// Linearly interpolates from one color to another based on the given weighting.
        /// </summary>
        /// <param name="from">The first color value.</param>
        /// <param name="to">The second color value.</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        /// <returns>
        /// The <see cref="Rgba32"/>
        /// </returns>
        public static Rgba32 Lerp(Rgba32 from, Rgba32 to, float amount)
        {
            Vector4 lerp = Vector4.Lerp(from.ToVector4(), to.ToVector4(), amount);
            return PackNew(ref lerp);
        }
    }
}
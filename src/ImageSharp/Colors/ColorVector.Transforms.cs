// <copyright file="ColorVector.Transforms.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Unpacked pixel type containing four 16-bit floating-point values typically ranging from 0 to 1.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct ColorVector
    {
        /// <summary>
        /// Adds the second color to the first.
        /// </summary>
        /// <param name="left">The first source color.</param>
        /// <param name="right">The second source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ColorVector operator +(ColorVector left, ColorVector right)
        {
            return new ColorVector(left.backingVector + right.backingVector);
        }

        /// <summary>
        /// Subtracts the second color from the first.
        /// </summary>
        /// <param name="left">The first source color.</param>
        /// <param name="right">The second source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ColorVector operator -(ColorVector left, ColorVector right)
        {
            return new ColorVector(left.backingVector - right.backingVector);
        }

        /// <summary>
        /// The blending formula simply selects the source color.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Normal(ColorVector backdrop, ColorVector source)
        {
            Vector4 normal = Vector4BlendTransforms.Normal(backdrop.backingVector, source.backingVector);
            return new ColorVector(normal);
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
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Multiply(ColorVector backdrop, ColorVector source)
        {
            Vector4 multiply = Vector4BlendTransforms.Multiply(backdrop.backingVector, source.backingVector);
            return new ColorVector(multiply);
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
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Screen(ColorVector backdrop, ColorVector source)
        {
            Vector4 subtract = Vector4BlendTransforms.Screen(backdrop.backingVector, source.backingVector);
            return new ColorVector(subtract);
        }

        /// <summary>
        /// Multiplies or screens the colors, depending on the source color value. The effect is similar to
        /// shining a harsh spotlight on the backdrop.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector HardLight(ColorVector backdrop, ColorVector source)
        {
            Vector4 hardlight = Vector4BlendTransforms.HardLight(backdrop.backingVector, source.backingVector);
            return new ColorVector(hardlight);
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
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Overlay(ColorVector backdrop, ColorVector source)
        {
            Vector4 overlay = Vector4BlendTransforms.Overlay(backdrop.backingVector, source.backingVector);
            return new ColorVector(overlay);
        }

        /// <summary>
        /// Selects the darker of the backdrop and source colors.
        /// The backdrop is replaced with the source where the source is darker; otherwise, it is left unchanged.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Darken(ColorVector backdrop, ColorVector source)
        {
            Vector4 darken = Vector4BlendTransforms.Darken(backdrop.backingVector, source.backingVector);
            return new ColorVector(darken);
        }

        /// <summary>
        /// Selects the lighter of the backdrop and source colors.
        /// The backdrop is replaced with the source where the source is lighter; otherwise, it is left unchanged.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Lighten(ColorVector backdrop, ColorVector source)
        {
            Vector4 lighten = Vector4BlendTransforms.Lighten(backdrop.backingVector, source.backingVector);
            return new ColorVector(lighten);
        }

        /// <summary>
        /// Darkens or lightens the colors, depending on the source color value. The effect is similar to shining
        /// a diffused spotlight on the backdrop.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector SoftLight(ColorVector backdrop, ColorVector source)
        {
            Vector4 softlight = Vector4BlendTransforms.SoftLight(backdrop.backingVector, source.backingVector);
            return new ColorVector(softlight);
        }

        /// <summary>
        /// Brightens the backdrop color to reflect the source color. Painting with black produces no changes.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector ColorDodge(ColorVector backdrop, ColorVector source)
        {
            Vector4 dodge = Vector4BlendTransforms.Dodge(backdrop.backingVector, source.backingVector);
            return new ColorVector(dodge);
        }

        /// <summary>
        /// Darkens the backdrop color to reflect the source color. Painting with white produces no change.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector ColorBurn(ColorVector backdrop, ColorVector source)
        {
            Vector4 burn = Vector4BlendTransforms.Burn(backdrop.backingVector, source.backingVector);
            return new ColorVector(burn);
        }

        /// <summary>
        /// Subtracts the darker of the two constituent colors from the lighter color.
        /// Painting with white inverts the backdrop color; painting with black produces no change.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Difference(ColorVector backdrop, ColorVector source)
        {
            Vector4 difference = Vector4BlendTransforms.Difference(backdrop.backingVector, source.backingVector);
            return new ColorVector(difference);
        }

        /// <summary>
        /// Produces an effect similar to that of the <see cref="Difference"/> mode but lower in contrast. Painting with white
        /// inverts the backdrop color; painting with black produces no change
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="ColorVector"/>.
        /// </returns>
        public static ColorVector Exclusion(ColorVector backdrop, ColorVector source)
        {
            Vector4 exclusion = Vector4BlendTransforms.Exclusion(backdrop.backingVector, source.backingVector);
            return new ColorVector(exclusion);
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
        /// The <see cref="ColorVector"/>
        /// </returns>
        public static ColorVector Lerp(ColorVector from, ColorVector to, float amount)
        {
            return new ColorVector(Vector4.Lerp(from.backingVector, to.backingVector, amount));
        }
    }
}
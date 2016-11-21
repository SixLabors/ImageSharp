// <copyright file="ColorTransforms.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp
{
    using System.Numerics;

    /// <summary>
    /// Packed vector type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct Color
    {
        /// <summary>
        /// Adds the second color to the first.
        /// </summary>
        /// <param name="left">The first source color.</param>
        /// <param name="right">The second source color.</param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color operator +(Color left, Color right)
        {
            Vector4 add = left.ToVector4() + right.ToVector4();
            return new Color(Pack(ref add));
        }

        /// <summary>
        /// Subtracts the second color from the first.
        /// </summary>
        /// <param name="left">The first source color.</param>
        /// <param name="right">The second source color.</param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color operator -(Color left, Color right)
        {
            Vector4 sub = left.ToVector4() - right.ToVector4();
            return new Color(Pack(ref sub));
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
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color Multiply(Color backdrop, Color source)
        {
            if (source == Black)
            {
                return Black;
            }

            if (source == White)
            {
                return backdrop;
            }

            Vector4 vb = backdrop.ToVector4();
            Vector4 vs = source.ToVector4();

            Vector4 multiply = vb * vs;
            multiply.W = vb.W;
            return new Color(Pack(ref multiply));
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
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color Screen(Color backdrop, Color source)
        {
            if (source == Black)
            {
                return backdrop;
            }

            if (source == White)
            {
                return White;
            }

            Vector4 vb = backdrop.ToVector4();
            Vector4 vs = source.ToVector4();

            Vector4 subtract = Vector4.Clamp(vb + vs - (vb * vs), Vector4.Zero, Vector4.One);
            subtract.W = vb.W;
            return new Color(Pack(ref subtract));
        }

        /// <summary>
        /// Multiplies or screens the colors, depending on the source color value. The effect is similar to 
        /// shining a harsh spotlight on the backdrop.
        /// </summary>
        /// <param name="backdrop">The backdrop color.</param>
        /// <param name="source">The source color.</param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color HardLight(Color backdrop, Color source)
        {
            // TODO: Why is this giving me nonsense?
            // https://www.w3.org/TR/compositing-1/#blendinghardlight
            // if(Cs <= 0.5)
            //    B(Cb, Cs) = Multiply(Cb, 2 x Cs)
            // else
            //    B(Cb, Cs) = Screen(Cb, 2 x Cs -1)  
            Vector4 vs = source.ToVector4();
            Vector4 blend = 2F * vs;
            if (vs.X <= 0.5F && vs.Y <= 0.5F && vs.Z <= 0.5F)
            {
                return Multiply(backdrop, new Color(Pack(ref blend)));
            }

            blend = (2F * vs) - Vector4.One;
            return Screen(backdrop, new Color(Pack(ref blend)));
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
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color Overlay(Color backdrop, Color source)
        {
            return HardLight(source, backdrop);
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
        /// The <see cref="Color"/>
        /// </returns>
        public static Color Lerp(Color from, Color to, float amount)
        {
            return new Color(Vector4.Lerp(from.ToVector4(), to.ToVector4(), amount));
        }
    }
}

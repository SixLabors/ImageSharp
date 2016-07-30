// <copyright file="ColorTransforms.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
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
        /// Blends two colors by multiplication.
        /// <remarks>
        /// The source color is multiplied by the destination color and replaces the destination.
        /// The resultant color is always at least as dark as either the source or destination color.
        /// Multiplying any color with black results in black. Multiplying any color with white preserves the 
        /// original color.
        /// </remarks>
        /// </summary>
        /// <param name="source">The source color.</param>
        /// <param name="destination">The destination color.</param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color Multiply(Color source, Color destination)
        {
            if (destination == Color.Black)
            {
                return Color.Black;
            }

            if (destination == Color.White)
            {
                return source;
            }

            // TODO: This will use less memory than using Vector4
            // but we should test speed vs memory to see which is best balance.
            byte r = (byte)(source.R * destination.R).Clamp(0, 255);
            byte g = (byte)(source.G * destination.G).Clamp(0, 255);
            byte b = (byte)(source.B * destination.B).Clamp(0, 255);
            byte a = (byte)(source.A * destination.A).Clamp(0, 255);

            return new Color(r, g, b, a);
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

// <copyright file="ColorTransforms.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// Represents a four-component color using red, green, blue, and alpha data. 
    /// Each component is stored in premultiplied format multiplied by the alpha component.
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

            return new Color(source.backingVector * destination.backingVector);
        }

        /// <summary>
        /// Linearly interpolates from one color to another based on the given amount.
        /// </summary>
        /// <param name="source">The first color value.</param>
        /// <param name="destination">The second color value.</param>
        /// <param name="amount">
        /// The weight value. At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color Lerp(Color source, Color destination, float amount)
        {
            amount = amount.Clamp(0f, 1f);

            if (Math.Abs(source.A - 1) < Epsilon && Math.Abs(destination.A - 1) < Epsilon)
            {
                return source + ((destination - source) * amount);
            }

            // Premultiplied.
            return (source * (1 - amount)) + destination;
        }
    }
}

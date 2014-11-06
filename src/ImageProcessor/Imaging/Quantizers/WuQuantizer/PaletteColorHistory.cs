// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PaletteColorHistory.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The palette color history.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System.Drawing;

    /// <summary>
    /// The palette color history.
    /// Adapted from <see href="https://github.com/drewnoakes" />
    /// </summary>
    internal struct PaletteColorHistory
    {
        /// <summary>
        /// The alpha component.
        /// </summary>
        public int Alpha;

        /// <summary>
        /// The red component.
        /// </summary>
        public int Red;

        /// <summary>
        /// The green component.
        /// </summary>
        public int Green;

        /// <summary>
        /// The blue component.
        /// </summary>
        public int Blue;

        /// <summary>
        /// The sum of the color components.
        /// </summary>
        public int Sum;

        /// <summary>
        /// Normalizes the color.
        /// </summary>
        /// <returns>
        /// The normalized <see cref="Color"/>.
        /// </returns>
        public Color ToNormalizedColor()
        {
            return (this.Sum != 0) ? Color.FromArgb(this.Alpha /= this.Sum, this.Red /= this.Sum, this.Green /= this.Sum, this.Blue /= this.Sum) : Color.Empty;
        }

        /// <summary>
        /// Adds a pixel to the color history.
        /// </summary>
        /// <param name="pixel">
        /// The pixel.
        /// </param>
        public void AddPixel(Pixel pixel)
        {
            this.Alpha += pixel.Alpha;
            this.Red += pixel.Red;
            this.Green += pixel.Green;
            this.Blue += pixel.Blue;
            this.Sum++;
        }
    }
}

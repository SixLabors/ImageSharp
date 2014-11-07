// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PaletteColorHistory.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The palette color history containing the sum of all pixel data.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System.Drawing;

    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// The palette color history containing the sum of all pixel data.
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
        /// The pixel to add.
        /// </param>
        public void AddPixel(Color32 pixel)
        {
            this.Alpha += pixel.A;
            this.Red += pixel.R;
            this.Green += pixel.G;
            this.Blue += pixel.B;
            this.Sum++;
        }
    }
}

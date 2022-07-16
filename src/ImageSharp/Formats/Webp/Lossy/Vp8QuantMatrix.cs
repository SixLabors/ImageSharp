// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8QuantMatrix
    {
        private int dither;

        public int[] Y1Mat { get; } = new int[2];

        public int[] Y2Mat { get; } = new int[2];

        public int[] UvMat { get; } = new int[2];

        /// <summary>
        /// Gets or sets the U/V quantizer value.
        /// </summary>
        public int UvQuant { get; set; }

        /// <summary>
        /// Gets or sets the dithering amplitude (0 = off, max=255).
        /// </summary>
        public int Dither
        {
            get => this.dither;
            set
            {
                Guard.MustBeBetweenOrEqualTo(value, 0, 255, nameof(this.Dither));
                this.dither = value;
            }
        }
    }
}

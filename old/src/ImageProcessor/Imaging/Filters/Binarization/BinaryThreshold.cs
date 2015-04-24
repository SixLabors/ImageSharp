// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BinaryThreshold.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Performs binary threshold filtering against a given greyscale image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Binarization
{
    using System.Drawing;
    using System.Threading.Tasks;

    /// <summary>
    /// Performs binary threshold filtering against a given greyscale image.
    /// </summary>
    public class BinaryThreshold
    {
        /// <summary>
        /// The threshold value.
        /// </summary>
        private byte threshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThreshold"/> class.
        /// </summary>
        /// <param name="threshold">
        /// The threshold.
        /// </param>
        public BinaryThreshold(byte threshold = 10)
        {
            this.threshold = threshold;
        }

        /// <summary>
        /// Gets or sets the threshold.
        /// </summary>
        public byte Threshold
        {
            get
            {
                return this.threshold;
            }

            set
            {
                this.threshold = value;
            }
        }

        /// <summary>
        /// Processes the given bitmap to apply the threshold.
        /// </summary>
        /// <param name="source">
        /// The image to process.
        /// </param>
        /// <returns>
        /// A processed bitmap.
        /// </returns>
        public Bitmap ProcessFilter(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;

            using (FastBitmap sourceBitmap = new FastBitmap(source))
            {
                Parallel.For(
                    0, 
                    height, 
                    y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            // ReSharper disable AccessToDisposedClosure
                            Color color = sourceBitmap.GetPixel(x, y);
                            sourceBitmap.SetPixel(x, y, color.B >= this.threshold ? Color.White : Color.Black);

                            // ReSharper restore AccessToDisposedClosure
                        }
                    });
            }

            return source;
        }
    }
}

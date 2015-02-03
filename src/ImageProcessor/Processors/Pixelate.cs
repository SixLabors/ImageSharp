// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Pixelate.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to pixelate an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Threading.Tasks;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Encapsulates methods to pixelate an image.
    /// </summary>
    public class Pixelate : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Pixelate"/> class.
        /// </summary>
        public Pixelate()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the dynamic parameter.
        /// </summary>
        public dynamic DynamicParameter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings
        {
            get;
            set;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                Tuple<int, Rectangle?> parameters = this.DynamicParameter;
                int size = parameters.Item1;
                Rectangle rectangle = parameters.Item2 ?? new Rectangle(0, 0, image.Width, image.Height);
                int x = rectangle.X;
                int y = rectangle.Y;
                int offset = size / 2;
                int width = rectangle.Width;
                int height = rectangle.Height;
                int maxWidth = image.Width;
                int maxHeight = image.Height;

                newImage = new Bitmap(image);

                using (FastBitmap fastBitmap = new FastBitmap(newImage))
                {
                    // Get the range of on the y-plane to choose from.
                    IEnumerable<int> range = EnumerableExtensions.SteppedRange(y, i => i < y + height && i < maxHeight, size);

                    Parallel.ForEach(
                        range,
                        j =>
                        {
                            for (int i = x; i < x + width && i < maxWidth; i += size)
                            {
                                int offsetX = offset;
                                int offsetY = offset;

                                // Make sure that the offset is within the boundary of the image.
                                while (j + offsetY >= maxHeight)
                                {
                                    offsetY--;
                                }

                                while (i + offsetX >= maxWidth)
                                {
                                    offsetX--;
                                }

                                // Get the pixel color in the centre of the soon to be pixelated area.
                                // ReSharper disable AccessToDisposedClosure
                                Color pixel = fastBitmap.GetPixel(i + offsetX, j + offsetY);

                                // For each pixel in the pixelate size, set it to the centre color.
                                for (int l = j; l < j + size && l < maxHeight; l++)
                                {
                                    for (int k = i; k < i + size && k < maxWidth; k++)
                                    {
                                        fastBitmap.SetPixel(k, l, pixel);
                                    }
                                }
                                // ReSharper restore AccessToDisposedClosure
                            }
                        });
                }

                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                throw new ImageProcessingException("Error processing image with " + this.GetType().Name, ex);
            }

            return image;
        }
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ReplaceColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods allowing the replacement of a color within an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Encapsulates methods allowing the replacement of a color within an image.
    /// </summary>
    public class ReplaceColor : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaceColor"/> class.
        /// </summary>
        public ReplaceColor()
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
                Tuple<Color, Color, int> parameters = this.DynamicParameter;
                Color original = parameters.Item1;
                byte originalR = original.R;
                byte originalG = original.G;
                byte originalB = original.B;

                Color replacement = parameters.Item2;
                byte replacementR = original.R;
                byte replacementG = replacement.G;
                byte replacementB = replacement.B;
                byte replacementA = replacement.A;

                int fuzziness = parameters.Item3;

                newImage = new Bitmap(image);
                int width = image.Width;
                int height = image.Height;

                using (FastBitmap fastBitmap = new FastBitmap(newImage))
                {
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            // Get the pixel color.
                            Color currentColor = fastBitmap.GetPixel(x, y);
                            byte currentR = currentColor.R;
                            byte currentG = currentColor.B;
                            byte currentB = currentColor.B;
                            byte currentA = currentColor.A;

                            // Test whether it is in the expected range.
                            if (currentR <= originalR + fuzziness && currentR >= originalR - fuzziness)
                            {
                                if (currentG <= originalG + fuzziness && currentG >= originalG - fuzziness)
                                {
                                    if (currentB <= originalB + fuzziness && currentB >= originalB - fuzziness)
                                    {
                                        // Ensure the values are withing an acceptable byte range
                                        // and set the new value.
                                        byte r = (originalR - currentR + replacementR).ToByte();
                                        byte g = (originalG - currentG + replacementG).ToByte();
                                        byte b = (originalB - currentB + replacementB).ToByte();

                                        // Allow replacement with transparent color.
                                        byte a = currentA != replacementA ? replacementA : currentA;

                                        fastBitmap.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                                    }
                                }
                            }
                        }
                    }
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

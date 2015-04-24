// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Mask.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Applies a mask to the given image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Colors;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Applies a mask to the given image. If the mask is not the same size as the image 
    /// it will be centered against the image.
    /// </summary>
    public class Mask : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Mask"/> class.
        /// </summary>
        public Mask()
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
            Bitmap mask = null;
            Bitmap maskCropped = null;
            Bitmap maskPositioned = null;
            Image image = factory.Image;

            try
            {
                int width = image.Width;
                int height = image.Height;
                Tuple<Image, Point?> parameters = this.DynamicParameter;
                mask = new Bitmap(parameters.Item1);
                Point? position = parameters.Item2;

                if (mask.Size != image.Size)
                {
                    Rectangle parent = new Rectangle(0, 0, width, height);
                    Rectangle child = ImageMaths.GetFilteredBoundingRectangle(mask, 0, RgbaComponent.A);
                    maskCropped = new Bitmap(child.Width, child.Height);
                    maskCropped.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                    // First crop any bounding transparency.
                    using (Graphics graphics = Graphics.FromImage(maskCropped))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.DrawImage(
                                         mask,
                                         new Rectangle(0, 0, child.Width, child.Height),
                                         child.X,
                                         child.Y,
                                         child.Width,
                                         child.Height,
                                         GraphicsUnit.Pixel);
                    }

                    // Now position the mask in an image of the same dimensions as the original.
                    maskPositioned = new Bitmap(width, height);
                    maskPositioned.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                    using (Graphics graphics = Graphics.FromImage(maskPositioned))
                    {
                        graphics.Clear(Color.Transparent);

                        if (position != null)
                        {
                            // Apply the mask at the given position.
                            graphics.DrawImage(maskCropped, position.Value);
                        }
                        else
                        {
                            // Center it instead
                            RectangleF centered = ImageMaths.CenteredRectangle(parent, child);
                            graphics.DrawImage(maskCropped, new PointF(centered.X, centered.Y));
                        }
                    }

                    newImage = Effects.ApplyMask(image, maskPositioned);
                    maskCropped.Dispose();
                    maskPositioned.Dispose();
                }
                else
                {
                    newImage = Effects.ApplyMask(image, mask);
                    mask.Dispose();
                }

                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (mask != null)
                {
                    mask.Dispose();
                }

                if (maskCropped != null)
                {
                    maskCropped.Dispose();
                }

                if (maskPositioned != null)
                {
                    maskPositioned.Dispose();
                }

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

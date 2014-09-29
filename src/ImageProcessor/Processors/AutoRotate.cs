// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AutoRotate.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Performs auto-rotation to ensure that EXIF defined rotation is reflected in
//   the final image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.MetaData;

    /// <summary>
    /// Performs auto-rotation to ensure that EXIF defined rotation is reflected in 
    /// the final image.
    /// </summary>
    public class AutoRotate : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutoRotate"/> class.
        /// </summary>
        public AutoRotate()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets DynamicParameter.
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
        /// <param name="factory">The current instance of the 
        /// <see cref="T:ImageProcessor.ImageFactory" /> class containing
        /// the image to process.</param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory" /> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                const int Orientation = (int)ExifPropertyTag.Orientation;
                if (!factory.PreserveExifData && factory.ExifPropertyItems.ContainsKey(Orientation))
                {
                    newImage = new Bitmap(image);

                    int rotationValue = factory.ExifPropertyItems[Orientation].Value[0];
                    switch (rotationValue)
                    {
                        case 1: // Landscape, do nothing
                            break;

                        case 8: // Rotated 90 right
                            // De-rotate:
                            newImage.RotateFlip(RotateFlipType.Rotate270FlipNone);
                            break;

                        case 3: // Bottoms up
                            newImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
                            break;

                        case 6: // Rotated 90 left
                            newImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            break;
                    }

                    // Reassign the image.
                    image.Dispose();
                    image = newImage;
                }
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
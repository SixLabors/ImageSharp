// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DetectEdges.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Produces an image with the detected edges highlighted.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Filters.EdgeDetection;

    /// <summary>
    /// Produces an image with the detected edges highlighted.
    /// </summary>
    public class DetectEdges : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DetectEdges"/> class.
        /// </summary>
        public DetectEdges()
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
            Tuple<IEdgeFilter, bool> parameters = this.DynamicParameter;
            IEdgeFilter filter = parameters.Item1;
            bool greyscale = parameters.Item2;

            try
            {
                ConvolutionFilter convolutionFilter = new ConvolutionFilter(filter, greyscale);

                // Check and assign the correct method. Don't use reflection for speed.
                newImage = filter is I2DEdgeFilter
                    ? convolutionFilter.Process2DFilter((Bitmap)image)
                    : convolutionFilter.ProcessFilter((Bitmap)image);

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

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DetectObjects.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to change the DetectObjects component of the image to effect its transparency.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Filters.ObjectDetection;
    using ImageProcessor.Imaging.Filters.Photo;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Encapsulates methods to change the DetectObjects component of the image to effect its transparency.
    /// </summary>
    public class DetectObjects : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DetectObjects"/> class.
        /// </summary>
        public DetectObjects()
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
            Bitmap grey = null;
            Image image = factory.Image;

            try
            {
                HaarCascade cascade = this.DynamicParameter;
                grey = new Bitmap(image.Width, image.Height);
                grey.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                grey = MatrixFilters.GreyScale.TransformImage(image, grey);

                HaarObjectDetector detector = new HaarObjectDetector(cascade)
                {
                    SearchMode = ObjectDetectorSearchMode.NoOverlap,
                    ScalingMode = ObjectDetectorScalingMode.GreaterToSmaller,
                    ScalingFactor = 1.5f
                };

                // Process frame to detect objects
                Rectangle[] rectangles = detector.ProcessFrame(grey);
                grey.Dispose();

                newImage = new Bitmap(image);
                newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (Pen blackPen = new Pen(Color.White))
                    {
                        blackPen.Width = 4;
                        graphics.DrawRectangles(blackPen, rectangles);
                    }
                }

                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (grey != null)
                {
                    grey.Dispose();
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

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageFactoryExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Extends the ImageFactory class to provide a fluent API.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.Cair
{
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Plugins.Cair.Imaging;
    using ImageProcessor.Plugins.Cair.Processors;

    /// <summary>
    /// Extends the ImageFactory class to provide a fluent API.
    /// </summary>
    public static class ImageFactoryExtensions
    {
        /// <summary>
        /// Resizes the current image to the given dimensions using Content Aware Resizing.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class
        /// that this method extends.
        /// </param>
        /// <param name="size">
        /// The <see cref="T:System.Drawing.Size"/> containing the width and height to set the image to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public static ImageFactory ContentAwareResize(this ImageFactory factory, Size size)
        {
            if (factory.ShouldProcess)
            {
                int width = size.Width;
                int height = size.Height;

                ContentAwareResizeLayer resizeLayer = new ContentAwareResizeLayer(new Size(width, height));
                return factory.ContentAwareResize(resizeLayer);
            }

            return factory;
        }

        /// <summary>
        /// Resizes the current image to the given dimensions using Content Aware Resizing.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class
        /// that this method extends.
        /// </param>
        /// <param name="layer">
        /// The <see cref="ContentAwareResizeLayer"/> containing the properties required to resize the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public static ImageFactory ContentAwareResize(this ImageFactory factory, ContentAwareResizeLayer layer)
        {
            if (factory.ShouldProcess)
            {
                Dictionary<string, string> resizeSettings = new Dictionary<string, string> { { "MaxWidth", layer.Size.Width.ToString("G") }, { "MaxHeight", layer.Size.Height.ToString("G") } };

                ContentAwareResize resize = new ContentAwareResize { DynamicParameter = layer, Settings = resizeSettings };
                factory.CurrentImageFormat.ApplyProcessor(resize.ProcessImage, factory);
            }

            return factory;
        }
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageFactoryExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Extends the ImageFactory class to provide a fluent API.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using ImageProcessor.Extensions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Config;
    #endregion

    /// <summary>
    /// Extends the ImageFactory class to provide a fluent API.
    /// </summary>
    public static class ImageFactoryExtensions
    {
        /// <summary>
        /// The object to lock against.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Auto processes image files based on any query string parameters added to the image path.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class
        /// that this method extends.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public static ImageFactory AutoProcess(this ImageFactory factory)
        {
            if (factory.ShouldProcess)
            {
                // It's faster to lock and run through our activated list than to create new instances.
                lock (SyncRoot)
                {
                    // Get a list of all graphics processors that have parsed and matched the query string.
                    List<IGraphicsProcessor> graphicsProcessors =
                        ImageProcessorConfig.Instance.GraphicsProcessors
                        .Where(x => x.MatchRegexIndex(factory.QueryString) != int.MaxValue)
                        .OrderBy(y => y.SortOrder)
                        .ToList();

                    // Loop through and process the image.
                    foreach (IGraphicsProcessor graphicsProcessor in graphicsProcessors)
                    {
                        ApplyProcessor(graphicsProcessor.ProcessImage, factory);
                    }
                }
            }

            return factory;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="processor">
        /// The processor.
        /// </param>
        /// <param name="factory">
        /// The factory.
        /// </param>
        private static void ApplyProcessor(Func<ImageFactory, Image> processor, ImageFactory factory)
        {
            ImageInfo imageInfo = factory.Image.GetImageInfo(factory.ImageFormat);

            if (imageInfo.IsAnimated)
            {
                OctreeQuantizer quantizer = new OctreeQuantizer(255, 8);

                // We don't dispose of the memory stream as that is disposed when a new image is created and doing so 
                // beforehand will cause an exception.
                MemoryStream stream = new MemoryStream();
                using (GifEncoder encoder = new GifEncoder(stream, null, null, imageInfo.LoopCount))
                {
                    foreach (GifFrame frame in imageInfo.GifFrames)
                    {
                        factory.Update(frame.Image);
                        frame.Image = quantizer.Quantize(processor.Invoke(factory));
                        encoder.AddFrame(frame);
                    }
                }

                stream.Position = 0;
                factory.Update(Image.FromStream(stream));
            }
            else
            {
                factory.Update(processor.Invoke(factory));
            }
        }
    }
}

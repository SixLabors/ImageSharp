// -----------------------------------------------------------------------
// <copyright file="ImageFactoryExtensions.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web
{
    #region Using
    using System.Collections.Generic;
    using System.Linq;
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
                    // Get a list of all graphics processors that have parsed and matched the querystring.
                    List<IGraphicsProcessor> graphicsProcessors =
                        ImageProcessorConfig.Instance.GraphicsProcessors
                        .Where(x => x.MatchRegexIndex(factory.QueryString) != int.MaxValue)
                        .OrderBy(y => y.SortOrder)
                        .ToList();

                    // Loop through and process the image.
                    foreach (IGraphicsProcessor graphicsProcessor in graphicsProcessors)
                    {
                        factory.Image = graphicsProcessor.ProcessImage(factory);
                    }
                }
            }

            return factory;
        }
    }
}

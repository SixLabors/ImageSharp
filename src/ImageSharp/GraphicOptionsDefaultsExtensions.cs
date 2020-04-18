// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Adds extensions that allow the processing of images to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class GraphicOptionsDefaultsExtensions
    {
        /// <summary>
        /// Sets the default options against the image processing context.
        /// </summary>
        /// <param name="context">The image processing context to store default against.</param>
        /// <param name="options">The default options to use.</param>
        public static void SetDefaultOptions(this IImageProcessingContext context, GraphicsOptions options)
        {
            context.Properties[typeof(GraphicsOptions)] = options;
        }

        /// <summary>
        /// Sets the default options against the configuration.
        /// </summary>
        /// <param name="context">The image processing context to store default against.</param>
        /// <param name="options">The default options to use.</param>
        public static void SetDefaultOptions(this Configuration context, GraphicsOptions options)
        {
            context.Properties[typeof(GraphicsOptions)] = options;
        }

        /// <summary>
        /// Gets the default options against the image processing context.
        /// </summary>
        /// <param name="context">The image processing context to retrieve defaults from.</param>
        /// <returns>The globaly configued default options.</returns>
        public static GraphicsOptions GetDefaultGraphicsOptions(this IImageProcessingContext context)
        {
            if (context.Properties.TryGetValue(typeof(GraphicsOptions), out var options) && options is GraphicsOptions go)
            {
                return go;
            }

            var configOptions = context.Configuration.GetDefaultGraphicsOptions();
            context.Properties[typeof(GraphicsOptions)] = configOptions;
            return configOptions;
        }

        /// <summary>
        /// Gets the default options against the image processing context.
        /// </summary>
        /// <param name="context">The image processing context to retrieve defaults from.</param>
        /// <returns>The globaly configued default options.</returns>
        public static GraphicsOptions GetDefaultGraphicsOptions(this Configuration context)
        {
            if (context.Properties.TryGetValue(typeof(GraphicsOptions), out var options) && options is GraphicsOptions go)
            {
                return go;
            }

            var configOptions = new GraphicsOptions();
            context.Properties[typeof(GraphicsOptions)] = configOptions;
            return configOptions;
        }
    }
}

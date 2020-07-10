// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
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
        /// <param name="optionsBuilder">The action to update instance of the default options used.</param>
        /// <returns>The passed in <paramref name="context"/> to allow chaining.</returns>
        public static IImageProcessingContext SetGraphicsOptions(this IImageProcessingContext context, Action<GraphicsOptions> optionsBuilder)
        {
            var cloned = context.GetGraphicsOptions().DeepClone();
            optionsBuilder(cloned);
            context.Properties[typeof(GraphicsOptions)] = cloned;
            return context;
        }

        /// <summary>
        /// Sets the default options against the configuration.
        /// </summary>
        /// <param name="configuration">The configuration to store default against.</param>
        /// <param name="optionsBuilder">The default options to use.</param>
        public static void SetGraphicsOptions(this Configuration configuration, Action<GraphicsOptions> optionsBuilder)
        {
            var cloned = configuration.GetGraphicsOptions().DeepClone();
            optionsBuilder(cloned);
            configuration.Properties[typeof(GraphicsOptions)] = cloned;
        }

        /// <summary>
        /// Sets the default options against the image processing context.
        /// </summary>
        /// <param name="context">The image processing context to store default against.</param>
        /// <param name="options">The default options to use.</param>
        /// <returns>The passed in <paramref name="context"/> to allow chaining.</returns>
        public static IImageProcessingContext SetGraphicsOptions(this IImageProcessingContext context, GraphicsOptions options)
        {
            context.Properties[typeof(GraphicsOptions)] = options;
            return context;
        }

        /// <summary>
        /// Sets the default options against the configuration.
        /// </summary>
        /// <param name="configuration">The configuration to store default against.</param>
        /// <param name="options">The default options to use.</param>
        public static void SetGraphicsOptions(this Configuration configuration, GraphicsOptions options)
        {
            configuration.Properties[typeof(GraphicsOptions)] = options;
        }

        /// <summary>
        /// Gets the default options against the image processing context.
        /// </summary>
        /// <param name="context">The image processing context to retrieve defaults from.</param>
        /// <returns>The globaly configued default options.</returns>
        public static GraphicsOptions GetGraphicsOptions(this IImageProcessingContext context)
        {
            if (context.Properties.TryGetValue(typeof(GraphicsOptions), out var options) && options is GraphicsOptions go)
            {
                return go;
            }

            // do not cache the fall back to config into the the processing context
            // in case someone want to change the value on the config and expects it re trflow thru
            return context.Configuration.GetGraphicsOptions();
        }

        /// <summary>
        /// Gets the default options against the image processing context.
        /// </summary>
        /// <param name="configuration">The configuration to retrieve defaults from.</param>
        /// <returns>The globaly configued default options.</returns>
        public static GraphicsOptions GetGraphicsOptions(this Configuration configuration)
        {
            if (configuration.Properties.TryGetValue(typeof(GraphicsOptions), out var options) && options is GraphicsOptions go)
            {
                return go;
            }

            var configOptions = new GraphicsOptions();

            // capture the fallback so the same instance will always be returned in case its mutated
            configuration.Properties[typeof(GraphicsOptions)] = configOptions;
            return configOptions;
        }
    }
}

// <copyright file="ImageSharpMiddlewareOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System;

    using ImageSharp.Web.Commands;
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Resolvers;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Configuration options for the ImageSharp middleware.
    /// </summary>
    public class ImageSharpMiddlewareOptions
    {
        /// <summary>
        /// Gets or sets the base library configuration
        /// </summary>
        public Configuration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the number of days to store images in the browser cache.
        /// </summary>
        public int MaxBrowserCacheDays { get; set; }

        /// <summary>
        /// Gets or sets the number of days to store images in the image cache.
        /// </summary>
        public int MaxCacheDays { get; set; }

        /// <summary>
        /// Gets or sets the additional validation method.
        /// This is called once the commands have been gathered and before an <see cref="IImageResolver"/> has been assigned.
        /// This can be used to used to augment commands and further validate the request.
        /// Emptying the dictionary will ensure that the middleware will ignore the request.
        /// </summary>
        public Action<ImageValidationContext> OnValidate { get; set; } = v =>
            {
                // It's a good idea to have this to provide very basic security. We can safely use the static
                // resize processor properties and the validation method will pass even if not installed.
                uint width = v.Parser.ParseValue<uint>(v.Commands.GetValueOrDefault(ResizeWebProcessor.Width));
                uint height = v.Parser.ParseValue<uint>(v.Commands.GetValueOrDefault(ResizeWebProcessor.Height));

                if (width > 4000 && height > 4000)
                {
                    v.Commands.Remove(ResizeWebProcessor.Width);
                    v.Commands.Remove(ResizeWebProcessor.Height);
                }
            };

        /// <summary>
        /// Gets or sets the additional processing method.
        /// This is called after image has been processed, but before the result has been cached.
        /// This can be used to further optimize the resultant image.
        /// </summary>
        public Action<ImageProcessingContext> OnProcessed { get; set; } = _ => { };

        /// <summary>
        /// Gets or sets the additional response method.
        /// This is called after the status code and headers have been set, but before the body has been written.
        /// This can be used to add or change the response headers.
        /// </summary>
        public Action<HttpContext> OnPrepareResponse { get; set; } = _ => { };
    }
}
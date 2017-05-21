// <copyright file="ImageSharpMiddlewareOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System;
    using System.Collections.Generic;

    using ImageSharp.Web.Caching;
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
        /// Gets or sets the uri parser
        /// </summary>
        public IUriParser UriParser { get; set; }

        /// <summary>
        /// Gets or sets the collecion of image resolvers.
        /// </summary>
        public IList<IImageResolver> Resolvers { get; set; } = new List<IImageResolver>();

        /// <summary>
        /// Gets or sets the collecion of image processors.
        /// </summary>
        public IList<IImageWebProcessor> Processors { get; set; } = new List<IImageWebProcessor>();

        /// <summary>
        /// Gets or sets the cache.
        /// </summary>
        public IImageCache Cache { get; set; }

        /// <summary>
        /// Gets or sets the number of days to store images in the cache.
        /// </summary>
        public int MaxCacheDays { get; set; }

        /// <summary>
        /// Gets or sets the additional validation method used to augment commands.
        /// This is called once the commands have been gathered and before an <see cref="IImageResolver"/> has been assigned.
        /// Emptying the dictionary will ensure that the middleware will ignore the request.
        /// </summary>
        public Action<HttpContext, IDictionary<string, string>> OnValidate { get; set; } = (context, commands) =>
         {
             CommandParser parser = CommandParser.Instance;
             uint width = parser.ParseValue<uint>(commands.GetValueOrDefault(ResizeWebProcessor.Width));
             uint height = parser.ParseValue<uint>(commands.GetValueOrDefault(ResizeWebProcessor.Height));

             if (width > 4000 && height > 4000)
             {
                 commands.Remove(ResizeWebProcessor.Width);
                 commands.Remove(ResizeWebProcessor.Height);
             }
         };
    }
}
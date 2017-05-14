// <copyright file="ImageSharpMiddleware.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Middleware for handling the processing of images via a url api.
    /// </summary>
    public class ImageSharpMiddleware
    {
        /// <summary>
        /// The function processing the Http request.
        /// </summary>
        private readonly RequestDelegate next;

        private readonly ImageSharpMiddlewareOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharpMiddleware"/> class.
        /// </summary>
        /// <param name="next">The function processing the HTTP request</param>
        /// <param name="options">Configuration options</param>
        public ImageSharpMiddleware(RequestDelegate next, IOptions<ImageSharpMiddlewareOptions> options)
        {
            Guard.NotNull(next, nameof(next));
            Guard.NotNull(options, nameof(options));
            this.next = next;
            this.options = options.Value;
        }

        /// <summary>
        /// Performs operations upon the current request.
        /// </summary>
        /// <param name="context">The current cHTTP request context</param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task Invoke(HttpContext context)
        {
            // Call the next delegate/middleware in the pipeline
            await this.next(context);
        }
    }
}

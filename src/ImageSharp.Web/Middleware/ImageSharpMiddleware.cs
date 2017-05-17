// <copyright file="ImageSharpMiddleware.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using ImageSharp.Memory;
    using ImageSharp.Web.Services;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Middleware for handling the processing of images via a URI querystring API.
    /// </summary>
    public class ImageSharpMiddleware
    {
        /// <summary>
        /// The function processing the Http request.
        /// </summary>
        private readonly RequestDelegate next;

        /// <summary>
        /// The configuration options
        /// </summary>
        private readonly ImageSharpMiddlewareOptions options;

        /// <summary>
        /// The hosting environment the application is running in.
        /// </summary>
        private readonly IHostingEnvironment environment;

        /// <summary>
        /// The logger for logging errors.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharpMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="environment">The <see cref="IHostingEnvironment"/> used by this middleware.</param>
        /// <param name="options">The configuration options.</param>
        /// <param name="loggerFactory">An <see cref="ILoggerFactory"/> instance used to create loggers.</param>
        public ImageSharpMiddleware(RequestDelegate next, IHostingEnvironment environment, IOptions<ImageSharpMiddlewareOptions> options, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(next, nameof(next));
            Guard.NotNull(environment, nameof(environment));
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.next = next;
            this.environment = environment;
            this.options = options.Value;
            this.logger = loggerFactory.CreateLogger<ImageSharpMiddleware>();
        }

        /// <summary>
        /// Performs operations upon the current request.
        /// </summary>
        /// <param name="context">The current cHTTP request context</param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task Invoke(HttpContext context)
        {
            // TODO: Parse the request path and application path.
            string path = string.Empty;
            string applicationPath = string.Empty;

            // Get the correct service for the request.
            IImageService service = this.AssignService(path, applicationPath);

            if (service == null || !await service.IsValidRequestAsync(context, this.environment, this.logger, path))
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next(context);
                return;
            }

            // TODO: Check querystring against list of known parameters. Only continue if valid.
            // TODO: Check cache and return correct response if already exists.
            byte[] buffer = await service.ResolveImageAsync(context, this.environment, this.logger, path);
            MemoryStream stream = null;
            try
            {
                // No allocations here since we are passing the buffer.
                stream = new MemoryStream(buffer);
                using (var image = Image.Load(stream))
                {
                    // TODO: Process and save.
                }
            }
            catch (Exception ex)
            {
                this.logger.LogCritical($"{ex.Message}{Environment.NewLine}StackTrace:{ex.StackTrace}");
            }
            finally
            {
                stream?.Dispose();
                BufferDataPool.Return(buffer);
            }

            // TODO: Caching and Response.

            // Call the next delegate/middleware in the pipeline
            await this.next(context);
        }

        private IImageService AssignService(string uri, string applicationPath)
        {
            throw new NotImplementedException();
        }
    }
}
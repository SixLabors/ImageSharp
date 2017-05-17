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
    using ImageSharp.Web.Caching;
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

            // TODO: Is this correct?
            PathString applicationPath = context.Request.PathBase;
            string query = string.Empty;

            // Get the correct service for the request.
            IImageService service = this.AssignService(path, applicationPath);

            if (service == null || !await service.IsValidRequestAsync(context, this.environment, this.logger, path))
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next.Invoke(context);
                return;
            }

            // TODO: Check querystring against list of known parameters. Only continue if valid.
            // TODO: Check cache and return correct response if already exists.
            IImageCache cache = this.options.Cache;

            // TODO: Add event hendler to allow augmenting the querystring value.
            string key = CacheHash.Create(path + query);

            if (!await cache.IsExpiredAsync(this.environment, key, DateTime.UtcNow.AddDays(-this.options.MaxCacheDays)))
            {
                // TODO: Do something with this value. We should be able to copy something from the StaticFileMiddleware to
                // check for a 304 response and do the right thing.
                // https://github.com/aspnet/StaticFiles/blob/dev/src/Microsoft.AspNetCore.StaticFiles/StaticFileMiddleware.cs#L95
                byte[] cachedBuffer = await cache.GetAsync(this.environment, key);
            }

            // Not cached? Let's get it from the image service.
            byte[] buffer = await service.ResolveImageAsync(context, this.environment, this.logger, path);
            MemoryStream inStream = null;
            MemoryStream outStream = null;
            try
            {
                // No allocations here for inStream since we are passing the buffer.
                inStream = new MemoryStream(buffer);
                outStream = new MemoryStream();
                using (var image = Image.Load(inStream))
                {
                    // TODO: Process.
                    image.Save(outStream);
                }

                // TODO: Add an event for post processing the image.
                byte[] outBuffer = outStream.ToArray();
                await cache.SetAsync(this.environment, key, outBuffer, DateTime.UtcNow);

                // TODO: Response headers.
            }
            catch (Exception ex)
            {
                // TODO: Create an extension that does this interpolation globally.
                this.logger.LogCritical($"{ex.Message}{Environment.NewLine}StackTrace:{ex.StackTrace}");
            }
            finally
            {
                inStream?.Dispose();
                outStream?.Dispose();
                BufferDataPool.Return(buffer);
            }
        }

        private IImageService AssignService(string uri, PathString applicationPath)
        {
            throw new NotImplementedException();
        }
    }
}
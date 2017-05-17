// <copyright file="ImageSharpMiddleware.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using ImageSharp.Memory;
    using ImageSharp.Web.Caching;
    using ImageSharp.Web.Helpers;
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
        /// The type used for performing logging.
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
        /// <param name="context">The current HTTP request context</param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task Invoke(HttpContext context)
        {
            // TODO: Parse the request path and application path.
            PathString path = context.Request.Path;
            PathString applicationPath = context.Request.PathBase;
            QueryString query = context.Request.QueryString;

            if (!query.HasValue)
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next(context);
                return;
            }

            // Get the correct service for the request.
            IImageService service = await this.AssignServiceAsync(context, path, applicationPath);

            if (service == null)
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next(context);
                return;
            }

            // TODO: Check querystring against list of known parameters. Only continue if valid.
            // TODO: Check cache and return correct response if already exists.
            IImageCache cache = this.options.Cache;

            // TODO: Add event handler to allow augmenting the querystring value.
            string uri = path + query;
            string key = $"{CacheHash.Create(uri)}.{FormatHelpers.GetExtension(this.options.Configuration, uri)}";

            ImageCacheInfo info = await cache.IsExpiredAsync(this.environment, key, DateTime.UtcNow.AddDays(-this.options.MaxCacheDays));

            var imageContext = new ImageContext(context);

            if (!info.Expired)
            {
                await this.SendResponse(imageContext, info, cache, key, info.LastModified, null);
                return;
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
                using (var image = Image.Load(this.options.Configuration, inStream))
                {
                    // TODO: Process.
                    image.Save(outStream);
                }

                // TODO: Add an event for post processing the image.
                byte[] outBuffer = outStream.ToArray();

                DateTime cachedDate = DateTime.UtcNow;
                await cache.SetAsync(this.environment, key, outBuffer, cachedDate);
                await this.SendResponse(imageContext, info, cache, key, cachedDate, outBuffer);
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

        private async Task SendResponse(ImageContext imageContext, ImageCacheInfo info, IImageCache cache, string key, DateTimeOffset lastModified, byte[] buffer)
        {
            // If info length is zero it means that the image has only just been cached so the buffer has value.
            long len = info.Length > 0 ? info.Length : buffer.Length;
            DateTimeOffset lm = info.LastModified > DateTimeOffset.MinValue ? info.LastModified : lastModified;

            imageContext.ComprehendRequestHeaders(lm, len);

            string contentType = FormatHelpers.GetContentType(this.options.Configuration, key);

            switch (imageContext.GetPreconditionState())
            {
                case ImageContext.PreconditionState.Unspecified:
                case ImageContext.PreconditionState.ShouldProcess:
                    if (imageContext.IsHeadRequest())
                    {
                        await imageContext.SendStatusAsync(ResponseConstants.Status200Ok, contentType);
                    }

                    // logger.LogFileServed(fileContext.SubPath, fileContext.PhysicalPath);
                    if (buffer == null)
                    {
                        buffer = await cache.GetAsync(this.environment, key);
                    }

                    await imageContext.SendAsync(contentType, buffer);
                    break;

                case ImageContext.PreconditionState.NotModified:
                    // _logger.LogPathNotModified(fileContext.SubPath);
                    await imageContext.SendStatusAsync(ResponseConstants.Status304NotModified, contentType);
                    break;
                case ImageContext.PreconditionState.PreconditionFailed:
                    // _logger.LogPreconditionFailed(fileContext.SubPath);
                    await imageContext.SendStatusAsync(ResponseConstants.Status412PreconditionFailed, contentType);
                    break;
                default:
                    var exception = new NotImplementedException(imageContext.GetPreconditionState().ToString());
                    Debug.Fail(exception.ToString());
                    throw exception;
            }
        }

        private async Task<IImageService> AssignServiceAsync(HttpContext context, string uri, string applicationPath)
        {
            IList<IImageService> services = this.options.Services;

            // Remove the Application Path from the Request.Path.
            // This allows applications running on localhost as sub applications to work.
            string path = uri.TrimStart(applicationPath.ToCharArray());
            foreach (IImageService service in services)
            {
                string key = service.Key;
                if (string.IsNullOrWhiteSpace(key) || !path.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (await service.IsValidRequestAsync(context, this.environment, this.logger, path))
                {
                    return service;
                }
            }

            // Return the file based service.
            Type physicalType = typeof(PhysicalFileImageService);

            IImageService physicalService = services.FirstOrDefault(s => s.GetType() == physicalType);
            if (physicalService != null)
            {
                if (await physicalService.IsValidRequestAsync(context, this.environment, this.logger, path))
                {
                    return physicalService;
                }
            }

            // Return the next unprefixed service.
            foreach (IImageService service in services.Where(s => string.IsNullOrEmpty(s.Key) && s.GetType() != physicalType))
            {
                if (await service.IsValidRequestAsync(context, this.environment, this.logger, path))
                {
                    return service;
                }
            }

            return null;
        }
    }
}
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
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Resolvers;
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
        /// The collection of known commands gathered from the processors.
        /// </summary>
        private readonly IEnumerable<string> knownCommands;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharpMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="environment">The <see cref="IHostingEnvironment"/> used by this middleware</param>
        /// <param name="options">The configuration options</param>
        /// <param name="loggerFactory">An <see cref="ILoggerFactory"/> instance used to create loggers</param>
        public ImageSharpMiddleware(RequestDelegate next, IHostingEnvironment environment, IOptions<ImageSharpMiddlewareOptions> options, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(next, nameof(next));
            Guard.NotNull(environment, nameof(environment));
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.next = next;
            this.environment = environment;
            this.options = options.Value;

            var commands = new List<string>();
            foreach (IImageWebProcessor processor in this.options.Processors)
            {
                commands.AddRange(processor.Commands);
            }

            this.knownCommands = commands;

            this.logger = loggerFactory.CreateLogger<ImageSharpMiddleware>();
        }

        /// <summary>
        /// Performs operations upon the current request.
        /// </summary>
        /// <param name="context">The current HTTP request context</param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task Invoke(HttpContext context)
        {
            IDictionary<string, string> commands = this.options.UriParser.ParseUriCommands(context);

            this.options.OnValidate(commands);

            if (!commands.Any() || !commands.Keys.Intersect(this.knownCommands).Any())
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next(context);
                return;
            }

            // TODO: Check querystring against list of known parameters. Only continue if valid.
            // Get the correct service for the request.
            IImageResolver resolver = this.options.Resolvers.FirstOrDefault(r => r.Key(context));

            if (resolver == null || !await resolver.IsValidRequestAsync(context, this.logger))
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next(context);
                return;
            }

            string uri = context.Request.Path + QueryString.Create(commands);
            IImageCache cache = this.options.Cache;
            string key = CacheHash.Create(uri, this.options.Configuration);

            CachedInfo info = await cache.IsExpiredAsync(key, DateTime.UtcNow.AddDays(-this.options.MaxCacheDays));

            var imageContext = new ImageContext(context, this.options);

            if (!info.Expired)
            {
                // Image is a cached image. Return the correct response now.
                await this.SendResponse(imageContext, cache, key, info.LastModifiedUtc, null, (int)info.Length);
                return;
            }

            // Not cached? Let's get it from the image service.
            byte[] inBuffer = await resolver.ResolveImageAsync(context, this.logger);

            // TODO: Empty buffer = 404?
            byte[] outBuffer = null;
            MemoryStream inStream = null;
            MemoryStream outStream = null;
            try
            {
                // No allocations here for inStream since we are passing the buffer.
                // TODO: How to prevent the allocation in outStream? Passing a pooled buffer won't let stream grow if needed.
                inStream = new MemoryStream(inBuffer);
                outStream = new MemoryStream();
                using (var image = Image.Load(this.options.Configuration, inStream))
                {
                    image.Process(this.logger, this.options.Processors, commands)
                         .Save(outStream);
                }

                // TODO: Add an event for post processing the image.
                // Copy the outstream to the pooled buffer.
                int outLength = (int)outStream.Position + 1;
                outStream.Position = 0;
                outBuffer = BufferDataPool.Rent(outLength);
                await outStream.ReadAsync(outBuffer, 0, outLength);

                DateTimeOffset cachedDate = await cache.SetAsync(key, outBuffer, outLength);
                await this.SendResponse(imageContext, cache, key, cachedDate, outBuffer, outLength);
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

                // Buffer should have been rented in IImageService
                BufferDataPool.Return(inBuffer);
                BufferDataPool.Return(outBuffer);
            }
        }

        private async Task SendResponse(ImageContext imageContext, IImageCache cache, string key, DateTimeOffset lastModified, byte[] buffer, int length)
        {
            imageContext.ComprehendRequestHeaders(lastModified, length);

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
                        // We're pulling the buffer from the cache. This should be pooled.
                        CachedBuffer cachedBuffer = await cache.GetAsync(key);
                        await imageContext.SendAsync(contentType, cachedBuffer.Buffer, cachedBuffer.Length);
                        BufferDataPool.Return(cachedBuffer.Buffer);
                    }
                    else
                    {
                        await imageContext.SendAsync(contentType, buffer, length);
                    }

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
    }
}
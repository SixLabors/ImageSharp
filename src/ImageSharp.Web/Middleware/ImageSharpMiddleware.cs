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
    using ImageSharp.Web.Commands;
    using ImageSharp.Web.Helpers;
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Resolvers;

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
        /// The type used for performing logging.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// The URI parser for parsing commands from the current request.
        /// </summary>
        private readonly IUriParser uriParser;

        /// <summary>
        /// The collection of image resolvers.
        /// </summary>
        private readonly IEnumerable<IImageResolver> resolvers;

        /// <summary>
        /// The collection of image processors.
        /// </summary>
        private readonly IEnumerable<IImageWebProcessor> processors;

        /// <summary>
        /// The image cache.
        /// </summary>
        private readonly IImageCache cache;

        /// <summary>
        /// The collection of known commands gathered from the processors.
        /// </summary>
        private readonly IEnumerable<string> knownCommands;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharpMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="options">The configuration options</param>
        /// <param name="loggerFactory">An <see cref="ILoggerFactory"/> instance used to create loggers</param>
        /// <param name="uriParser">An <see cref="IUriParser"/> instance used to parse URI's for commands</param>
        /// <param name="resolvers">A collection of <see cref="IImageResolver"/> instances used to resolve images</param>
        /// <param name="processors">A collection of <see cref="IImageWebProcessor"/> instances used to process images</param>
        /// <param name="cache">An <see cref="IImageCache"/> instance used for caching images</param>
        public ImageSharpMiddleware(
            RequestDelegate next,
            IOptions<ImageSharpMiddlewareOptions> options,
            ILoggerFactory loggerFactory,
            IUriParser uriParser,
            IEnumerable<IImageResolver> resolvers,
            IEnumerable<IImageWebProcessor> processors,
            IImageCache cache)
        {
            Guard.NotNull(next, nameof(next));
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(uriParser, nameof(uriParser));
            Guard.NotNull(resolvers, nameof(resolvers));
            Guard.NotNull(processors, nameof(processors));
            Guard.NotNull(cache, nameof(cache));

            this.next = next;
            this.options = options.Value;
            this.uriParser = uriParser;
            this.resolvers = resolvers;
            this.processors = processors;
            this.cache = cache;

            var commands = new List<string>();
            foreach (IImageWebProcessor processor in this.processors)
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
            IDictionary<string, string> commands = this.uriParser.ParseUriCommands(context);
            this.options.OnValidate(new ImageValidationContext(context, commands, CommandParser.Instance));

            if (!commands.Any() || !commands.Keys.Intersect(this.knownCommands).Any())
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next(context);
                return;
            }

            // Get the correct service for the request.
            IImageResolver resolver = this.resolvers.FirstOrDefault(r => r.Key(context));

            if (resolver == null || !await resolver.IsValidRequestAsync(context, this.logger))
            {
                // Nothing to do. call the next delegate/middleware in the pipeline
                await this.next(context);
                return;
            }

            string uri = context.Request.Path + QueryString.Create(commands);
            string key = CacheHash.Create(uri, this.options.Configuration);

            CachedInfo info = await this.cache.IsExpiredAsync(key, DateTime.UtcNow.AddDays(-this.options.MaxCacheDays));

            var imageContext = new ImageContext(context, this.options);

            if (!info.Expired)
            {
                // Image is a cached image. Return the correct response now.
                await this.SendResponse(imageContext, key, info.LastModifiedUtc, null, (int)info.Length);
                return;
            }

            // Not cached? Let's get it from the image resolver.
            byte[] inBuffer = null;
            byte[] outBuffer = null;
            MemoryStream inStream = null;
            MemoryStream outStream = null;
            try
            {
                inBuffer = await resolver.ResolveImageAsync(context, this.logger);
                if (inBuffer == null || inBuffer.Length == 0)
                {
                    // Log the error but let the pipeline handle the 404
                    this.logger.LogImageResolveFailed(imageContext.GetDisplayUrl());
                    await this.next(context);
                    return;
                }

                // No allocations here for inStream since we are passing the buffer.
                // TODO: How to prevent the allocation in outStream? Passing a pooled buffer won't let stream grow if needed.
                inStream = new MemoryStream(inBuffer);
                outStream = new MemoryStream();
                using (var image = Image.Load(this.options.Configuration, inStream))
                {
                    image.Process(this.logger, this.processors, commands)
                         .Save(outStream);
                }

                // Allow for any further optimization of the image. Always reset the position just in case.
                outStream.Position = 0;
                this.options.OnProcessed(new ImageProcessingContext(context, outStream, Path.GetExtension(key)));
                outStream.Position = 0;
                int outLength = (int)outStream.Length;

                // Copy the outstream to the pooled buffer.
                outBuffer = BufferDataPool.Rent(outLength);
                await outStream.ReadAsync(outBuffer, 0, outLength);

                DateTimeOffset cachedDate = await this.cache.SetAsync(key, outBuffer, outLength);
                await this.SendResponse(imageContext, key, cachedDate, outBuffer, outLength);
            }
            catch (Exception ex)
            {
                this.logger.LogImageProcessingFailed(imageContext.GetDisplayUrl(), ex);
            }
            finally
            {
                inStream?.Dispose();
                outStream?.Dispose();

                // Buffer should have been rented in IImageResolver
                BufferDataPool.Return(inBuffer);
                BufferDataPool.Return(outBuffer);
            }
        }

        private async Task SendResponse(ImageContext imageContext, string key, DateTimeOffset lastModified, byte[] buffer, int length)
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

                    this.logger.LogImageServed(imageContext.GetDisplayUrl(), key);
                    if (buffer == null)
                    {
                        // We're pulling the buffer from the cache. This should be pooled.
                        CachedBuffer cachedBuffer = await this.cache.GetAsync(key);
                        await imageContext.SendAsync(contentType, cachedBuffer.Buffer, cachedBuffer.Length);
                        BufferDataPool.Return(cachedBuffer.Buffer);
                    }
                    else
                    {
                        await imageContext.SendAsync(contentType, buffer, length);
                    }

                    break;

                case ImageContext.PreconditionState.NotModified:
                    this.logger.LogImageNotModified(imageContext.GetDisplayUrl());
                    await imageContext.SendStatusAsync(ResponseConstants.Status304NotModified, contentType);
                    break;
                case ImageContext.PreconditionState.PreconditionFailed:
                    this.logger.LogImagePreconditionFailed(imageContext.GetDisplayUrl());
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
// <copyright file="ServiceCollectionExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using System;

    using ImageSharp.Web.Caching;
    using ImageSharp.Web.Commands;
    using ImageSharp.Web.Middleware;
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Resolvers;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to simplify middleware service registration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ImageSharp services to the specified <see cref="IServiceCollection" /> with the default options.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>An <see cref="IImageSharpBuilder"/> that can be used to further configure the ImageSharp services.</returns>
        public static IImageSharpBuilder AddImageSharp(this IServiceCollection services)
        {
            Guard.NotNull(services, nameof(services));

            services.AddSingleton<IConfigureOptions<ImageSharpMiddlewareOptions>, ImageSharpConfiguration>();

            IImageSharpCoreBuilder builder = AddImageSharpCore(services);

            builder.SetUriParser<QueryCollectionUriParser>();

            builder.SetCache<PhysicalFileSystemCache>();

            builder.AddResolver<PhysicalFileSystemResolver>();

            builder.AddProcessor<ResizeWebProcessor>();

            return new ImageSharpBuilder(builder.Services);
        }

        /// <summary>
        /// Adds ImageSharp services to the specified <see cref="IServiceCollection" /> with the given options.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="setupAction">An <see cref="Action{ImageSharpMiddlewareOptions}"/> to configure the provided <see cref="ImageSharpMiddlewareOptions"/>.</param>
        /// <returns>An <see cref="IImageSharpBuilder"/> that can be used to further configure the ImageSharp services.</returns>
        public static IImageSharpBuilder AddImageSharp(this IServiceCollection services, Action<ImageSharpMiddlewareOptions> setupAction)
        {
            Guard.NotNull(services, nameof(services));

            IImageSharpCoreBuilder builder = AddImageSharpCore(services);

            builder.Services.Configure(setupAction);

            builder.SetUriParser<QueryCollectionUriParser>();

            builder.SetCache<PhysicalFileSystemCache>();

            builder.AddResolver<PhysicalFileSystemResolver>();

            builder.AddProcessor<ResizeWebProcessor>();

            return new ImageSharpBuilder(builder.Services);
        }

        /// <summary>
        /// Provides the means to add essential ImageSharp services to the specified <see cref="IServiceCollection" /> with the default options.
        /// All additional services are required to be configured.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>An <see cref="IImageSharpCoreBuilder"/> that can be used to further configure the ImageSharp services.</returns>
        public static IImageSharpCoreBuilder AddImageSharpCore(this IServiceCollection services)
        {
            Guard.NotNull(services, nameof(services));

            services.AddSingleton<IConfigureOptions<ImageSharpMiddlewareOptions>, ImageSharpConfiguration>();

            return new ImageSharpCoreBuilder(services);
        }

        /// <summary>
        /// Provides the means to add essential ImageSharp services to the specified <see cref="IServiceCollection" /> with the given options.
        /// All additional services are required to be configured.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="setupAction">An <see cref="Action{ImageSharpMiddlewareOptions}"/> to configure the provided <see cref="ImageSharpMiddlewareOptions"/>.</param>
        /// <returns>An <see cref="IImageSharpCoreBuilder"/> that can be used to further configure the ImageSharp services.</returns>
        public static IImageSharpCoreBuilder AddImageSharpCore(this IServiceCollection services, Action<ImageSharpMiddlewareOptions> setupAction)
        {
            Guard.NotNull(services, nameof(services));

            services.Configure(setupAction);

            return new ImageSharpCoreBuilder(services);
        }
    }
}
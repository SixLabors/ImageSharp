// <copyright file="ServiceCollectionExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using ImageSharp.Web.Middleware;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to simplify middleware service registration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers ImageSharp with the default services
        /// </summary>
        /// <param name="services">The contract for the collection of service descriptors</param>
        /// <returns><see cref="IServiceCollection"/></returns>
        public static IServiceCollection AddImageSharp(this IServiceCollection services)
        {
            Guard.NotNull(services, nameof(services));

            return services.AddSingleton<IConfigureOptions<ImageSharpMiddlewareOptions>, ImageSharpConfiguration>();
        }

        /// <summary>
        /// Registers ImageSharp with the configured services
        /// </summary>
        /// <param name="services">The contract for the collection of service descriptors</param>
        /// <typeparam name="TOptions">The configuration options.</typeparam>
        /// <returns><see cref="IServiceCollection"/></returns>
        public static IServiceCollection AddImageSharp<TOptions>(this IServiceCollection services)
            where TOptions : class, IConfigureOptions<ImageSharpMiddlewareOptions>
        {
            Guard.NotNull(services, nameof(services));

            return services.AddSingleton<IConfigureOptions<ImageSharpMiddlewareOptions>, TOptions>();
        }
    }
}
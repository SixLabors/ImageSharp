// <copyright file="ImageSharpCoreBuilderExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using System;

    using ImageSharp.Web.Caching;
    using ImageSharp.Web.Commands;
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Resolvers;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Extension methods for <see cref="IImageSharpCoreBuilder"/> that allow configuration of services.
    /// </summary>
    public static class ImageSharpCoreBuilderExtensions
    {
        /// <summary>
        /// Sets the given <see cref="IUriParser"/> adding it to the service collection
        /// </summary>
        /// <typeparam name="TParser">The type of class implementing <see cref="IUriParser"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder SetUriParser<TParser>(this IImageSharpCoreBuilder builder)
            where TParser : class, IUriParser
        {
            builder.Services.TryAddSingleton<IUriParser, TParser>();
            return builder;
        }

        /// <summary>
        /// Sets the given <see cref="IUriParser"/> adding it to the service collection
        /// </summary>
        /// <typeparam name="TParser">The type of class implementing <see cref="IUriParser"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <param name="implementationFactory">The factory method for returning a <see cref="IUriParser"/>"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder SetUriParser<TParser>(this IImageSharpCoreBuilder builder, Func<IServiceProvider, TParser> implementationFactory)
            where TParser : class, IUriParser
        {
            builder.Services.TryAddSingleton(implementationFactory);
            return builder;
        }

        /// <summary>
        /// Sets the given <see cref="IImageCache"/> adding it to the service collection
        /// </summary>
        /// <typeparam name="TCache">The type of class implementing <see cref="IImageCache"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder SetCache<TCache>(this IImageSharpCoreBuilder builder)
            where TCache : class, IImageCache
        {
            builder.Services.TryAddSingleton<IImageCache, TCache>();
            return builder;
        }

        /// <summary>
        /// Sets the given <see cref="IImageCache"/> adding it to the service collection
        /// </summary>
        /// <typeparam name="TCache">The type of class implementing <see cref="IImageCache"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <param name="implementationFactory">The factory method for returning a <see cref="IImageCache"/>"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder SetCache<TCache>(this IImageSharpCoreBuilder builder, Func<IServiceProvider, TCache> implementationFactory)
            where TCache : class, IImageCache
        {
            builder.Services.TryAddSingleton(implementationFactory);
            return builder;
        }

        /// <summary>
        /// Adds the given <see cref="IImageResolver"/> to the resolver collection within the service collection
        /// </summary>
        /// <typeparam name="TResolver">The type of class implementing <see cref="IImageResolver"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder AddResolver<TResolver>(this IImageSharpCoreBuilder builder)
            where TResolver : class, IImageResolver
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IImageResolver, TResolver>());
            return builder;
        }

        /// <summary>
        /// Adds the given <see cref="IImageResolver"/> to the resolver collection within the service collection
        /// </summary>
        /// <typeparam name="TResolver">The type of class implementing <see cref="IImageResolver"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <param name="implementationFactory">The factory method for returning a <see cref="IImageResolver"/>"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder AddResolver<TResolver>(this IImageSharpCoreBuilder builder, Func<IServiceProvider, TResolver> implementationFactory)
            where TResolver : class, IImageResolver
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(implementationFactory));
            return builder;
        }

        /// <summary>
        /// Adds the given <see cref="IImageWebProcessor"/> to the processor collection within the service collection
        /// </summary>
        /// <typeparam name="TProcessor">The type of class implementing <see cref="IImageWebProcessor"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder AddProcessor<TProcessor>(this IImageSharpCoreBuilder builder)
            where TProcessor : class, IImageWebProcessor
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IImageWebProcessor, TProcessor>());
            return builder;
        }

        /// <summary>
        /// Adds the given <see cref="IImageWebProcessor"/> to the processor collection within the service collection
        /// </summary>
        /// <typeparam name="TProcessor">The type of class implementing <see cref="IImageWebProcessor"/>to add.</typeparam>
        /// <param name="builder">The <see cref="IImageSharpCoreBuilder"/></param>
        /// <param name="implementationFactory">The factory method for returning a <see cref="IImageResolver"/>"/></param>
        /// <returns>The <see cref="IImageSharpCoreBuilder"/>.</returns>
        public static IImageSharpCoreBuilder AddProcessor<TProcessor>(this IImageSharpCoreBuilder builder, Func<IServiceProvider, TProcessor> implementationFactory)
            where TProcessor : class, IImageWebProcessor
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(implementationFactory));
            return builder;
        }
    }
}
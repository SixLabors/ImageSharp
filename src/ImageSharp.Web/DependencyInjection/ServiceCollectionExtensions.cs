// <copyright file="ServiceCollectionExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using System.Collections.Generic;
    using ImageSharp.Web.Commands;
    using ImageSharp.Web.Processors;
    using Microsoft.Extensions.DependencyInjection;

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
        public static IServiceCollection UseImageSharp(this IServiceCollection services)
        {
            Guard.NotNull(services, nameof(services));

            services.AddSingleton<IUriParser>(new QueryCollectionUriParser());

            var processors = new List<IImageWebProcessor>
            {
                new ResizeWebProcessor()
            };

            services.AddSingleton<IEnumerable<IImageWebProcessor>>(processors);

            return services;
        }
    }
}
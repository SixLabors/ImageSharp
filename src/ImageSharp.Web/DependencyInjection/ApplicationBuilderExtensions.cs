// <copyright file="ApplicationBuilderExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using ImageSharp.Web.Middleware;
    using Microsoft.AspNetCore.Builder;

    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/> to simplify middleware registration.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Registers the ImageSharp middleware.
        /// </summary>
        /// <param name="app">The application with the mechanism to configure a request pipeline</param>
        /// <returns><see cref="IApplicationBuilder"/></returns>
        public static IApplicationBuilder UseImageSharp(this IApplicationBuilder app)
        {
            Guard.NotNull(app, nameof(app));

            return app.UseMiddleware<ImageSharpMiddleware>();
        }
    }
}
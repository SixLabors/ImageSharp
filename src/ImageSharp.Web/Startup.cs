// <copyright file="Startup.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web
{
    using ImageSharp.Web.Middleware;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Performs initialization logic.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Configures services for the middleware.
        /// </summary>
        /// <param name="services">The service contract.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ImageSharpMiddlewareOptions>(options => { });
        }
    }
}
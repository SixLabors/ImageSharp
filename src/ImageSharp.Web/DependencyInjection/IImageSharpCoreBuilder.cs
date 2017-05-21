// <copyright file="IImageSharpCoreBuilder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Defines a contract for adding core ImsgeSharp services
    /// </summary>
    public interface IImageSharpCoreBuilder
    {
        /// <summary>
        /// Gets the <see cref="IServiceCollection"/> where ImageSharp services are configured.
        /// </summary>
        IServiceCollection Services { get; }
    }
}

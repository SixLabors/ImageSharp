// <copyright file="IImageSharpBuilder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Defines a contract for adding ImsgeSharp services
    /// </summary>
    public interface IImageSharpBuilder
    {
        /// <summary>
        /// Gets the <see cref="IServiceCollection"/> where ImageSharp services are configured.
        /// </summary>
        IServiceCollection Services { get; }
    }
}

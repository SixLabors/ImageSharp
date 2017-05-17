// <copyright file="Startup.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Specifies the contract for returning images from different locations.
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Gets or sets the service key.
        /// </summary>
        string Key { get; set; }

        /// <summary>
        /// Gets or sets any additional settings.
        /// </summary>
        IDictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current request passes sanitizing rules.
        /// </summary>
        /// <param name="context">The current HTTP request context</param>
        /// <param name="path">The to the image.</param>
        /// <returns>
        /// <returns>The <see cref="Task{Boolean}"/></returns>
        /// </returns>
        bool IsValidRequestAsync(HttpContext context, string path);

        /// <summary>
        /// Resolves the image in an asynchronous manner.
        /// </summary>
        /// <param name="context">The current HTTP request context</param>
        /// <param name="path">The to the image.</param>
        /// <returns>The <see cref="Task{Byte}"/></returns>
        Task<Stream> ResolveImageAsync(HttpContext context, string path);
    }
}
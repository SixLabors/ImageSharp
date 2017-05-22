// <copyright file="ImageProcessingContext.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System.IO;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Contains information about the current image request and processed image.
    /// </summary>
    public class ImageProcessingContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessingContext"/> class.
        /// </summary>
        /// <param name="context">The current HTTP request context</param>
        /// <param name="stream">The stream containing the processed image bytes</param>
        /// <param name="extension">The file extension for the processed image</param>
        public ImageProcessingContext(HttpContext context, Stream stream, string extension)
        {
            this.Context = context;
            this.Stream = stream;
            this.Extension = extension;
        }

        /// <summary>
        /// Gets the current HTTP request context.
        /// </summary>
        public HttpContext Context { get; }

        /// <summary>
        /// Gets the stream containing the processed image bytes.
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// Gets the file extension for the processed image.
        /// </summary>
        public string Extension { get; }
    }
}
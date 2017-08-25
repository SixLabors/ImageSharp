// <copyright file="IImageWebProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Processors
{
    using System.Collections.Generic;

    using ImageSharp.Formats;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Specifies the contract for processing images using a querystring URI API.
    /// </summary>
    public interface IImageWebProcessor
    {
        /// <summary>
        /// Gets the collection of recognized querystring commands.
        /// </summary>
        IEnumerable<string> Commands { get; }

        /// <summary>
        /// Processes the image based on the querystring commands.
        /// </summary>
        /// <param name="image">The image to process</param>
        /// <param name="format">The current image format</param>
        /// <param name="logger">The type used for performing logging</param>
        /// <param name="commands">The querystring collection containing the processing commands</param>
        /// <returns>The <see cref="Image{Rgba32}"/></returns>
        Image<Rgba32> Process(Image<Rgba32> image, ref IImageFormat format, ILogger logger, IDictionary<string, string> commands);
    }
}

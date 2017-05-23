// <copyright file="WebProcessingExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Processors
{
    using System.Collections.Generic;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Extends the image API to allow processing via a URI API
    /// </summary>
    internal static class WebProcessingExtensions
    {
        /// <summary>
        /// Loops through the available processors and updates the image if any match
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="logger">The type used for performing logging</param>
        /// <param name="processors">The collection of available processors</param>
        /// <param name="commands">The querystring collection containing the processing commands</param>
        /// <returns>The <see cref="Image{Rgba32}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<Rgba32> Process(this Image<Rgba32> source, ILogger logger, IEnumerable<IImageWebProcessor> processors, IDictionary<string, string> commands)
        {
            foreach (IImageWebProcessor processor in processors)
            {
                source = processor.Process(source, logger, commands);
            }

            return source;
        }
    }
}
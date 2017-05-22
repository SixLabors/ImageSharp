// <copyright file="FormatWebProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Processors
{
    using System.Collections.Generic;
    using System.Linq;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;
    using ImageSharp.Web.Commands;
    using ImageSharp.Web.Middleware;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Allows the changing of image formats.
    /// </summary>
    public class FormatWebProcessor : IImageWebProcessor
    {
        /// <summary>
        /// The command constant for format
        /// </summary>
        public const string Format = "format";

        /// <summary>
        /// The middleware configuration options
        /// </summary>
        private readonly ImageSharpMiddlewareOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormatWebProcessor"/> class.
        /// </summary>
        /// <param name="options">The middleware configuration options</param>
        public FormatWebProcessor(IOptions<ImageSharpMiddlewareOptions> options)
        {
            this.options = options.Value;
        }

        /// <inheritdoc/>
        public IEnumerable<string> Commands => new[] { Format };

        /// <inheritdoc/>
        public Image<TPixel> Process<TPixel>(Image<TPixel> image, ILogger logger, IDictionary<string, string> commands)
            where TPixel : struct, IPixel<TPixel>
        {
            string extension = commands.GetValueOrDefault(Format);

            if (!string.IsNullOrWhiteSpace(extension))
            {
                IImageFormat format = this.options.Configuration.ImageFormats
                                                  .FirstOrDefault(f => f.SupportedExtensions.Contains(extension));

                if (format != null)
                {
                    image.CurrentImageFormat = format;
                }
            }

            return image;
        }
    }
}
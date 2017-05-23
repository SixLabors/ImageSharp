// <copyright file="BackgroundColorWebProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Processors
{
    using System.Collections.Generic;

    using ImageSharp.Web.Commands;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Allows setting of the background color
    /// </summary>
    public class BackgroundColorWebProcessor : IImageWebProcessor
    {
        /// <summary>
        /// The command for changing the background color
        /// </summary>
        public const string Color = "bgcolor";

        /// <summary>
        /// The reusable collection of commands
        /// </summary>
        private static readonly IEnumerable<string> ColorCommands
            = new[] { Color };

        /// <inheritdoc/>
        public IEnumerable<string> Commands { get; } = ColorCommands;

        /// <inheritdoc/>
        public Image<Rgba32> Process(Image<Rgba32> image, ILogger logger, IDictionary<string, string> commands)
        {
            Rgba32 background = CommandParser.Instance.ParseValue<Rgba32>(commands.GetValueOrDefault(Color));

            if (background != default(Rgba32))
            {
                image.BackgroundColor(background);
            }

            return image;
        }
    }
}
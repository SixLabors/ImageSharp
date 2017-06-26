// <copyright file="Component.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;

    using ImageSharp.Memory;

    /// <summary>
    /// Represents a component block
    /// </summary>
    internal struct Component : IDisposable
    {
        /// <summary>
        /// Gets or sets the output
        /// </summary>
        public Buffer<short> Output;

        /// <summary>
        /// Gets or sets the horizontal scaling factor
        /// </summary>
        public float ScaleX;

        /// <summary>
        /// Gets or sets the vertical scaling factor
        /// </summary>
        public float ScaleY;

        /// <summary>
        /// Gets or sets the number of blocks per line
        /// </summary>
        public int BlocksPerLine;

        /// <summary>
        /// Gets or sets the number of blocks per column
        /// </summary>
        public int BlocksPerColumn;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Output?.Dispose();
        }
    }
}
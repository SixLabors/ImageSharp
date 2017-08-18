// <copyright file="Component.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    using System;
    using System.Numerics;

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
        /// Gets or sets the scaling factors
        /// </summary>
        public Vector2 Scale;

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
            this.Output = null;
        }
    }
}
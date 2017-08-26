// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a component block
    /// </summary>
    internal class PdfJsComponent : IDisposable
    {
#pragma warning disable SA1401
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
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    using System;

    /// <summary>
    /// Contains all the decoded component blocks
    /// </summary>
    internal sealed class PdfJsComponentBlocks : IDisposable
    {
        /// <summary>
        /// Gets or sets the component blocks
        /// </summary>
        public PdfJsComponent[] Components { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.Components != null)
            {
                for (int i = 0; i < this.Components.Length; i++)
                {
                    this.Components[i].Dispose();
                }

                this.Components = null;
            }
        }
    }
}
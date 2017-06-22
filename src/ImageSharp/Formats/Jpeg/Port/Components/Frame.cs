// <copyright file="QuantizationTables.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;

    /// <summary>
    /// Represent a single jpeg frame
    /// </summary>
    internal class Frame : IDisposable
    {
        private bool isDisposed;

        /// <summary>
        /// Gets or sets a value indicating whether the frame uses the extended specification
        /// </summary>
        public bool Extended { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the frame uses the progressive specification
        /// </summary>
        public bool Progressive { get; set; }

        /// <summary>
        /// Gets or sets the precision
        /// </summary>
        public byte Precision { get; set; }

        /// <summary>
        /// Gets or sets the number of scanlines within the frame
        /// </summary>
        public short Scanlines { get; set; }

        /// <summary>
        /// Gets or sets the number of samples per scanline
        /// </summary>
        public short SamplesPerLine { get; set; }

        /// <summary>
        /// Gets or sets the number of components within a frame. In progressive frames this value can range from only 1 to 4
        /// </summary>
        public byte ComponentCount { get; set; }

        /// <summary>
        /// Gets or sets the component id collection
        /// </summary>
        public byte[] ComponentIds { get; set; }

        /// <summary>
        /// Gets or sets the frame component collection
        /// </summary>
        public FrameComponent[] Components { get; set; }

        /// <summary>
        /// Gets or sets the maximum horizontal sampling factor
        /// </summary>
        public int MaxHorizontalFactor { get; set; }

        /// <summary>
        /// Gets or sets the maximum vertical sampling factor
        /// </summary>
        public int MaxVerticalFactor { get; set; }

        /// <summary>
        /// Gets or sets the number of MCU's per line
        /// </summary>
        public int McusPerLine { get; set; }

        /// <summary>
        /// Gets or sets the number of MCU's per column
        /// </summary>
        public int McusPerColumn { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">Whether to dispose of managed objects</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (FrameComponent component in this.Components)
                {
                    component.Dispose();
                }
            }

            // Set large fields to null.
            this.Components = null;

            this.isDisposed = true;
        }
    }
}
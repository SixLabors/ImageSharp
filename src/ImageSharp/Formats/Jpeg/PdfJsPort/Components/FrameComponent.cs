// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a single frame component
    /// </summary>
    internal class FrameComponent : IDisposable
    {
        #pragma warning disable SA1401 // Fields should be private
        /// <summary>
        /// Gets or sets the component Id
        /// </summary>
        public byte Id;

        /// <summary>
        /// TODO: What does pred stand for?
        /// </summary>
        public int Pred;

        /// <summary>
        /// Gets or sets the horizontal sampling factor.
        /// </summary>
        public int HorizontalFactor;

        /// <summary>
        /// Gets or sets the vertical sampling factor.
        /// </summary>
        public int VerticalFactor;

        /// <summary>
        /// Gets or sets the identifier
        /// </summary>
        public byte QuantizationIdentifier;

        /// <summary>
        /// Gets or sets the block data
        /// </summary>
        public Buffer<short> BlockData;

        /// <summary>
        /// Gets or sets the number of blocks per line
        /// </summary>
        public int BlocksPerLine;

        /// <summary>
        /// Gets or sets the number of blocks per column
        /// </summary>
        public int BlocksPerColumn;

        /// <summary>
        /// Gets the index for the DC Huffman table
        /// </summary>
        public int DCHuffmanTableId;

        /// <summary>
        /// Gets the index for the AC Huffman table
        /// </summary>
        public int ACHuffmanTableId;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.BlockData?.Dispose();
            this.BlockData = null;
        }
    }
}
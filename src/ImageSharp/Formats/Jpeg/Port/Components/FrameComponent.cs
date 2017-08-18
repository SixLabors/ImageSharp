// <copyright file="FrameComponent.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;

    using ImageSharp.Memory;

    /// <summary>
    /// Represents a single frame component
    /// </summary>
    internal struct FrameComponent : IDisposable
    {
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
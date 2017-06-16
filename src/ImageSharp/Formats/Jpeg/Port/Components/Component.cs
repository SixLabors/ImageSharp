// <copyright file="Component.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    /// <summary>
    /// Represents a single color component
    /// </summary>
    internal struct Component
    {
        /// <summary>
        /// Gets or sets the component Id
        /// </summary>
        public byte Id;

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
        /// Gets or sets the quantization table
        /// </summary>
        public short[] QuantizationTable;

        /// <summary>
        /// Gets or sets the block data
        /// </summary>
        public short[] BlockData;

        /// <summary>
        /// Gets or sets the number of blocks per line
        /// </summary>
        public int BlocksPerLine;

        /// <summary>
        /// Gets or sets the number of blocks per column
        /// </summary>
        public int BlocksPerColumn;
    }
}
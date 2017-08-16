// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpg
{
    /// <summary>
    /// Represents a single color component
    /// </summary>
    internal struct Component
    {
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
        public byte Identifier;

        /// <summary>
        /// Gets or sets the quantization table destination selector.
        /// </summary>
        public byte Selector;
    }
}

// <copyright file="Component.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Represents a single color component
    /// </summary>
    internal class Component
    {
        /// <summary>
        /// Gets or sets the horizontal sampling factor.
        /// </summary>
        public int HorizontalFactor { get; set; }

        /// <summary>
        /// Gets or sets the vertical sampling factor.
        /// </summary>
        public int VerticalFactor { get; set; }

        /// <summary>
        /// Gets or sets the identifier
        /// </summary>
        public byte Identifier { get; set; }

        /// <summary>
        /// Gets or sets the quantization table destination selector.
        /// </summary>
        public byte Selector { get; set; }
    }
}

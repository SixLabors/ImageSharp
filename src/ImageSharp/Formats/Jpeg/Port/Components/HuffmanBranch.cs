// <copyright file="HuffmanBranch.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a branch in the huffman tree
    /// </summary>
    internal struct HuffmanBranch
    {
        /// <summary>
        /// The index
        /// </summary>
        public int Index;

        /// <summary>
        /// The value
        /// </summary>
        public short Value;

        /// <summary>
        /// The children
        /// </summary>
        public List<HuffmanBranch> Children;

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanBranch"/> struct.
        /// </summary>
        /// <param name="value">The value</param>
        public HuffmanBranch(short value)
            : this(value, new List<HuffmanBranch>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanBranch"/> struct.
        /// </summary>
        /// <param name="children">The branch children</param>
        public HuffmanBranch(List<HuffmanBranch> children)
            : this((short)0, children)
        {
        }

        private HuffmanBranch(short value, List<HuffmanBranch> children)
        {
            this.Index = 0;
            this.Value = value;
            this.Children = children;
        }
    }
}
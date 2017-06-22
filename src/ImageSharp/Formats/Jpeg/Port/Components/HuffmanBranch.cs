// <copyright file="HuffmanBranch.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System.Runtime.CompilerServices;

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
        /// The children.
        /// </summary>
        public HuffmanBranch[] Children;

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanBranch"/> struct.
        /// </summary>
        /// <param name="value">The value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HuffmanBranch(short value)
        {
            this.Index = 0;
            this.Value = value;
            this.Children = new HuffmanBranch[2];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanBranch"/> struct.
        /// </summary>
        /// <param name="children">The branch children</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HuffmanBranch(HuffmanBranch[] children)
        {
            this.Index = 0;
            this.Value = -1;
            this.Children = children;
        }
    }
}
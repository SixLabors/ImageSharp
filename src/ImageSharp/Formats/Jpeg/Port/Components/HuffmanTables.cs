// <copyright file="HuffmanTables.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Defines a pair of huffman tables
    /// </summary>
    internal class HuffmanTables
    {
        private HuffmanBranch[] first;

        private HuffmanBranch[] second;

        /// <summary>
        /// Gets or sets the table at the given index.
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>The <see cref="List{HuffmanBranch}"/></returns>
        public HuffmanBranch[] this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (index == 0)
                {
                    return this.first;
                }

                return this.second;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (index == 0)
                {
                    this.first = value;
                }

                this.second = value;
            }
        }
    }
}
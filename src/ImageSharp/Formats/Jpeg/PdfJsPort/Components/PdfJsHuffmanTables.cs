// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Defines a 2 pairs of huffman tables
    /// </summary>
    internal sealed class PdfJsHuffmanTables
    {
        private readonly PdfJsHuffmanTable[] tables = new PdfJsHuffmanTable[4];

        /// <summary>
        /// Gets or sets the table at the given index.
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>The <see cref="List{HuffmanBranch}"/></returns>
        public ref PdfJsHuffmanTable this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref this.tables[index];
            }
        }
    }
}
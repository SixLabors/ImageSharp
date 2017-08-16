// <copyright file="HuffmanTables.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Defines a pair of huffman tables
    /// </summary>
    internal sealed class PdfJsHuffmanTables : IDisposable
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

        /// <inheritdoc/>
        public void Dispose()
        {
            for (int i = 0; i < this.tables.Length; i++)
            {
                this.tables[i].Dispose();
            }
        }
    }
}
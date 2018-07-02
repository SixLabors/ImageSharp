// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// The collection of lookup tables used for fast AC entropy scan decoding.
    /// </summary>
    internal sealed class FastACTables : IDisposable
    {
        private Buffer2D<short> tables;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastACTables"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator used to allocate memory for image processing operations.</param>
        public FastACTables(MemoryAllocator memoryAllocator)
        {
            this.tables = memoryAllocator.AllocateClean2D<short>(512, 4);
        }

        /// <summary>
        /// Gets the <see cref="Span{Int16}"/> representing the table at the index in the collection.
        /// </summary>
        /// <param name="index">The table index.</param>
        /// <returns><see cref="Span{Int16}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<short> GetTableSpan(int index)
        {
            return this.tables.GetRowSpan(index);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.tables?.Dispose();
            this.tables = null;
        }
    }
}
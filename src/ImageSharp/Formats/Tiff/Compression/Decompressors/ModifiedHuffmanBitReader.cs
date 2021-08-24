// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Bit reader for data encoded with the modified huffman rle method.
    /// See TIFF 6.0 specification, section 10.
    /// </summary>
    internal class ModifiedHuffmanBitReader : T4BitReader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModifiedHuffmanBitReader"/> class.
        /// </summary>
        /// <param name="input">The compressed input stream.</param>
        /// <param name="fillOrder">The logical order of bits within a byte.</param>
        /// <param name="bytesToRead">The number of bytes to read from the stream.</param>
        /// <param name="allocator">The memory allocator.</param>
        public ModifiedHuffmanBitReader(Stream input, TiffFillOrder fillOrder, int bytesToRead, MemoryAllocator allocator)
            : base(input, fillOrder, bytesToRead, allocator)
        {
        }

        /// <inheritdoc/>
        public override bool HasMoreData => this.Position < (ulong)this.DataLength - 1 || ((uint)(this.BitsRead - 1) < (7 - 1));

        /// <inheritdoc/>
        public override bool IsEndOfScanLine
        {
            get
            {
                if (this.IsWhiteRun && this.CurValueBitsRead == 12 && this.Value == 1)
                {
                    return true;
                }

                if (this.CurValueBitsRead == 11 && this.Value == 0)
                {
                    // black run.
                    return true;
                }

                return false;
            }
        }

        /// <inheritdoc/>
        public override void StartNewRow()
        {
            base.StartNewRow();

            int remainder = this.BitsRead & 7;    // bit-hack for % 8
            if (remainder != 0)
            {
                // Skip padding bits, move to next byte.
                this.Position++;
                this.ResetBitsRead();
            }
        }

        /// <summary>
        /// No EOL is expected at the start of a run for the modified huffman encoding.
        /// </summary>
        protected override void ReadEolBeforeFirstData()
        {
            // Nothing to do here.
        }
    }
}

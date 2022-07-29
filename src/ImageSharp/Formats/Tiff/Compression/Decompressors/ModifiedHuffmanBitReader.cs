// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Bit reader for data encoded with the modified huffman rle method.
    /// See TIFF 6.0 specification, section 10.
    /// </summary>
    internal sealed class ModifiedHuffmanBitReader : T4BitReader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModifiedHuffmanBitReader"/> class.
        /// </summary>
        /// <param name="input">The compressed input stream.</param>
        /// <param name="fillOrder">The logical order of bits within a byte.</param>
        /// <param name="bytesToRead">The number of bytes to read from the stream.</param>
        public ModifiedHuffmanBitReader(BufferedReadStream input, TiffFillOrder fillOrder, int bytesToRead)
            : base(input, fillOrder, bytesToRead)
        {
        }

        /// <inheritdoc/>
        public override bool HasMoreData => this.Position < (ulong)this.DataLength - 1 || (uint)(this.BitsRead - 1) < 6;

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

            int remainder = Numerics.Modulo8(this.BitsRead);
            if (remainder != 0)
            {
                // Skip padding bits, move to next byte.
                this.AdvancePosition();
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

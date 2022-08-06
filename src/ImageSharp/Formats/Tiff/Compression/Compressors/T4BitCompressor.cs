// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors
{
    /// <summary>
    /// Bitwriter for writing compressed CCITT T4 1D data.
    /// </summary>
    internal sealed class T4BitCompressor : TiffCcittCompressor
    {
        /// <summary>
        /// The modified huffman is basically the same as CCITT T4, but without EOL markers and padding at the end of the rows.
        /// </summary>
        private readonly bool useModifiedHuffman;

        /// <summary>
        /// Initializes a new instance of the <see cref="T4BitCompressor" /> class.
        /// </summary>
        /// <param name="output">The output stream to write the compressed data.</param>
        /// <param name="allocator">The memory allocator.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="bitsPerPixel">The bits per pixel.</param>
        /// <param name="useModifiedHuffman">Indicates if the modified huffman RLE should be used.</param>
        public T4BitCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel, bool useModifiedHuffman = false)
            : base(output, allocator, width, bitsPerPixel) => this.useModifiedHuffman = useModifiedHuffman;

        /// <inheritdoc/>
        public override TiffCompression Method => this.useModifiedHuffman ? TiffCompression.Ccitt1D : TiffCompression.CcittGroup3Fax;

        /// <summary>
        /// Writes a image compressed with CCITT T4 to the output buffer.
        /// </summary>
        /// <param name="pixelsAsGray">The pixels as 8-bit gray array.</param>
        /// <param name="height">The strip height.</param>
        /// <param name="compressedData">The destination for the compressed data.</param>
        protected override void CompressStrip(Span<byte> pixelsAsGray, int height, Span<byte> compressedData)
        {
            if (!this.useModifiedHuffman)
            {
                // An EOL code is expected at the start of the data.
                this.WriteCode(12, 1, compressedData);
            }

            for (int y = 0; y < height; y++)
            {
                bool isWhiteRun = true;
                bool isStartOrRow = true;
                int x = 0;

                Span<byte> row = pixelsAsGray.Slice(y * this.Width, this.Width);
                while (x < this.Width)
                {
                    uint runLength = 0;
                    for (int i = x; i < this.Width; i++)
                    {
                        if (isWhiteRun && row[i] != 255)
                        {
                            break;
                        }

                        if (isWhiteRun && row[i] == 255)
                        {
                            runLength++;
                            continue;
                        }

                        if (!isWhiteRun && row[i] != 0)
                        {
                            break;
                        }

                        if (!isWhiteRun && row[i] == 0)
                        {
                            runLength++;
                        }
                    }

                    if (isStartOrRow && runLength == 0)
                    {
                        this.WriteCode(8, WhiteZeroRunTermCode, compressedData);

                        isWhiteRun = false;
                        isStartOrRow = false;
                        continue;
                    }

                    uint code;
                    uint codeLength;
                    if (runLength <= 63)
                    {
                        code = this.GetTermCode(runLength, out codeLength, isWhiteRun);
                        this.WriteCode(codeLength, code, compressedData);
                        x += (int)runLength;
                    }
                    else
                    {
                        runLength = this.GetBestFittingMakeupRunLength(runLength);
                        code = this.GetMakeupCode(runLength, out codeLength, isWhiteRun);
                        this.WriteCode(codeLength, code, compressedData);
                        x += (int)runLength;

                        // If we are at the end of the line with a makeup code, we need to write a final term code with a length of zero.
                        if (x == this.Width)
                        {
                            if (isWhiteRun)
                            {
                                this.WriteCode(8, WhiteZeroRunTermCode, compressedData);
                            }
                            else
                            {
                                this.WriteCode(10, BlackZeroRunTermCode, compressedData);
                            }
                        }

                        continue;
                    }

                    isStartOrRow = false;
                    isWhiteRun = !isWhiteRun;
                }

                this.WriteEndOfLine(compressedData);
            }
        }

        private void WriteEndOfLine(Span<byte> compressedData)
        {
            if (this.useModifiedHuffman)
            {
                this.PadByte();
            }
            else
            {
                // Write EOL.
                this.WriteCode(12, 1, compressedData);
            }
        }
    }
}

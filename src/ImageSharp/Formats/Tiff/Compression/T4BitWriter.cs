// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Bitwriter for writing compressed CCITT T4 1D data.
    /// </summary>
    internal class T4BitWriter
    {
        private static readonly Dictionary<uint, uint> WhiteLen4TermCodes = new Dictionary<uint, uint>()
        {
            { 2, 0x7 }, { 3, 0x8 }, { 4, 0xB }, { 5, 0xC }, { 6, 0xE }, { 7, 0xF }
        };

        private static readonly Dictionary<uint, uint> WhiteLen5TermCodes = new Dictionary<uint, uint>()
        {
            { 8, 0x13 }, { 9, 0x14 }, { 10, 0x7 }, { 11, 0x8 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen6TermCodes = new Dictionary<uint, uint>()
        {
            { 1, 0x7 }, { 12, 0x8 }, { 13, 0x3 }, { 14, 0x34 }, { 15, 0x35 }, { 16, 0x2A }, { 17, 0x2B }
        };

        private static readonly Dictionary<uint, uint> WhiteLen7TermCodes = new Dictionary<uint, uint>()
        {
            { 18, 0x27 }, { 19, 0xC }, { 20, 0x8 }, { 21, 0x17 }, { 22, 0x3 }, { 23, 0x4 }, { 24, 0x28 }, { 25, 0x2B }, { 26, 0x13 },
            { 27, 0x24 }, { 28, 0x18 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen8TermCodes = new Dictionary<uint, uint>()
        {
            { 0, 0x35 }, { 29, 0x2 }, { 30, 0x3 }, { 31, 0x1A }, { 32, 0x1B }, { 33, 0x12 }, { 34, 0x13 }, { 35, 0x14 }, { 36, 0x15 },
            { 37, 0x16 }, { 38, 0x17 }, { 39, 0x28 }, { 40, 0x29 }, { 41, 0x2A }, { 42, 0x2B }, { 43, 0x2C }, { 44, 0x2D }, { 45, 0x4 },
            { 46, 0x5 }, { 47, 0xA }, { 48, 0xB }, { 49, 0x52 }, { 50, 0x53 }, { 51, 0x54 }, { 52, 0x55 }, { 53, 0x24 }, { 54, 0x25 },
            { 55, 0x58 }, { 56, 0x59 }, { 57, 0x5A }, { 58, 0x5B }, { 59, 0x4A }, { 60, 0x4B }, { 61, 0x32 }, { 62, 0x33 }, { 63, 0x34 }
        };

        private static readonly Dictionary<uint, uint> BlackLen2TermCodes = new Dictionary<uint, uint>()
        {
            { 2, 0x3 }, { 3, 0x2 }
        };

        private static readonly Dictionary<uint, uint> BlackLen3TermCodes = new Dictionary<uint, uint>()
        {
            { 1, 0x2 }, { 4, 0x3 }
        };

        private static readonly Dictionary<uint, uint> BlackLen4TermCodes = new Dictionary<uint, uint>()
        {
            { 5, 0x3 }, { 6, 0x2 }
        };

        private static readonly Dictionary<uint, uint> BlackLen5TermCodes = new Dictionary<uint, uint>()
        {
            { 7, 0x3 }
        };

        private static readonly Dictionary<uint, uint> BlackLen6TermCodes = new Dictionary<uint, uint>()
        {
            { 8, 0x5 }, { 9, 0x4 }
        };

        private static readonly Dictionary<uint, uint> BlackLen7TermCodes = new Dictionary<uint, uint>()
        {
            { 10, 0x4 }, { 11, 0x5 }, { 12, 0x7 }
        };

        private static readonly Dictionary<uint, uint> BlackLen8TermCodes = new Dictionary<uint, uint>()
        {
            { 13, 0x4 }, { 14, 0x7 }
        };

        private static readonly Dictionary<uint, uint> BlackLen9TermCodes = new Dictionary<uint, uint>()
        {
            { 15, 0x18 }
        };

        private static readonly Dictionary<uint, uint> BlackLen10TermCodes = new Dictionary<uint, uint>()
        {
            { 0, 0x37 }, { 16, 0x17 }, { 17, 0x18 }, { 18, 0x8 }
        };

        private static readonly Dictionary<uint, uint> BlackLen11TermCodes = new Dictionary<uint, uint>()
        {
            { 19, 0x67 }, { 20, 0x68 }, { 21, 0x6C }, { 22, 0x37 }, { 23, 0x28 }, { 24, 0x17 }, { 25, 0x18 }
        };

        private static readonly Dictionary<uint, uint> BlackLen12TermCodes = new Dictionary<uint, uint>()
        {
            { 26, 0xCA }, { 27, 0xCB }, { 28, 0xCC }, { 29, 0xCD }, { 30, 0x68 }, { 31, 0x69 }, { 32, 0x6A }, { 33, 0x6B }, { 34, 0xD2 },
            { 35, 0xD3 }, { 36, 0xD4 }, { 37, 0xD5 }, { 38, 0xD6 }, { 39, 0xD7 }, { 40, 0x6C }, { 41, 0x6D }, { 42, 0xDA }, { 43, 0xDB },
            { 44, 0x54 }, { 45, 0x55 }, { 46, 0x56 }, { 47, 0x57 }, { 48, 0x64 }, { 49, 0x65 }, { 50, 0x52 }, { 51, 0x53 }, { 52, 0x24 },
            { 53, 0x37 }, { 54, 0x38 }, { 55, 0x27 }, { 56, 0x28 }, { 57, 0x58 }, { 58, 0x59 }, { 59, 0x2B }, { 60, 0x2C }, { 61, 0x5A },
            { 62, 0x66 }, { 63, 0x67 }
        };

        private readonly MemoryAllocator memoryAllocator;

        private readonly Configuration configuration;

        private int bytePosition = 0;

        private byte bitPosition = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="T4BitWriter" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The configuration.</param>
        public T4BitWriter(MemoryAllocator memoryAllocator, Configuration configuration)
        {
            this.memoryAllocator = memoryAllocator;
            this.configuration = configuration;
            this.bytePosition = 0;
            this.bitPosition = 0;
        }

        /// <summary>
        /// Writes a image compressed with CCITT T4 to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream. This has to be a bi-color image.</param>
        /// <param name="pixelRowAsGray">A span for converting a pixel row to gray.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <returns>The number of bytes written to the stream.</returns>
        public int CompressImage<TPixel>(Image<TPixel> image, Span<L8> pixelRowAsGray, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // This is too much memory allocated, but just 1 bit per pixel will not do, if the compression rate is not good.
            int maxNeededBytes = image.Width * image.Height;
            IMemoryOwner<byte> compressedDataBuffer = this.memoryAllocator.Allocate<byte>(maxNeededBytes, AllocationOptions.Clean);
            Span<byte> compressedData = compressedDataBuffer.GetSpan();

            this.bytePosition = 0;
            this.bitPosition = 0;

            // An EOL code is expected at the start of the data.
            this.WriteCode(12, 1, compressedData);

            for (int y = 0; y < image.Height; y++)
            {
                bool isWhiteRun = true;
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(this.configuration, pixelRow, pixelRowAsGray);
                int x = 0;
                while (x < image.Width)
                {
                    uint runLength = 0;
                    for (int i = x; i < pixelRow.Length; i++)
                    {
                        if (isWhiteRun && pixelRowAsGray[i].PackedValue != 255)
                        {
                            break;
                        }

                        if (isWhiteRun && pixelRowAsGray[i].PackedValue == 255)
                        {
                            runLength++;
                            continue;
                        }

                        if (!isWhiteRun && pixelRowAsGray[i].PackedValue != 0)
                        {
                            break;
                        }

                        if (!isWhiteRun && pixelRowAsGray[i].PackedValue == 0)
                        {
                            runLength++;
                        }
                    }

                    bool gotTermCode = this.GetTermCode(runLength, out var code, out var codeLength, isWhiteRun);

                    this.WriteCode(codeLength, code, compressedData);

                    x += (int)runLength;

                    isWhiteRun = !isWhiteRun;
                }

                // Write EOL
                this.WriteCode(12, 1, compressedData);
            }

            // Write the compressed data to the stream.
            stream.Write(compressedData.Slice(0, this.bytePosition));

            return this.bytePosition;
        }

        private void WriteCode(uint codeLength, uint code, Span<byte> compressedData)
        {
            while (codeLength > 0)
            {
                var bitNumber = (int) codeLength;
                var bit = (code & (1 << (bitNumber - 1))) != 0;
                if (bit)
                {
                    BitWriterUtils.WriteBit(compressedData, this.bytePosition, this.bitPosition);
                }
                else
                {
                    BitWriterUtils.WriteZeroBit(compressedData, this.bytePosition, this.bitPosition);
                }

                this.bitPosition++;
                if (this.bitPosition == 8)
                {
                    this.bytePosition++;
                    this.bitPosition = 0;
                }

                codeLength--;
            }
        }

        private bool GetTermCode(uint runLength, out uint code, out uint codeLength, bool isWhiteRun)
        {
            if (isWhiteRun)
            {
                return this.GetWhiteTermCode(runLength, out code, out codeLength);
            }

            return this.GetBlackTermCode(runLength, out code, out codeLength);
        }

        private bool GetWhiteTermCode(uint runLength, out uint code, out uint codeLength)
        {
            code = 0;
            codeLength = 0;

            if (WhiteLen4TermCodes.ContainsKey(runLength))
            {
                code = WhiteLen4TermCodes[runLength];
                codeLength = 4;
                return true;
            }

            if (WhiteLen5TermCodes.ContainsKey(runLength))
            {
                code = WhiteLen5TermCodes[runLength];
                codeLength = 5;
                return true;
            }

            if (WhiteLen6TermCodes.ContainsKey(runLength))
            {
                code = WhiteLen6TermCodes[runLength];
                codeLength = 6;
                return true;
            }

            if (WhiteLen7TermCodes.ContainsKey(runLength))
            {
                code = WhiteLen7TermCodes[runLength];
                codeLength = 7;
                return true;
            }

            if (WhiteLen8TermCodes.ContainsKey(runLength))
            {
                code = WhiteLen8TermCodes[runLength];
                codeLength = 8;
                return true;
            }

            return false;
        }

        private bool GetBlackTermCode(uint runLength, out uint code, out uint codeLength)
        {
            code = 0;
            codeLength = 0;

            if (BlackLen2TermCodes.ContainsKey(runLength))
            {
                code = BlackLen2TermCodes[runLength];
                codeLength = 2;
                return true;
            }

            if (BlackLen3TermCodes.ContainsKey(runLength))
            {
                code = BlackLen3TermCodes[runLength];
                codeLength = 3;
                return true;
            }

            if (BlackLen4TermCodes.ContainsKey(runLength))
            {
                code = BlackLen4TermCodes[runLength];
                codeLength = 4;
                return true;
            }

            if (BlackLen5TermCodes.ContainsKey(runLength))
            {
                code = BlackLen5TermCodes[runLength];
                codeLength = 5;
                return true;
            }

            if (BlackLen6TermCodes.ContainsKey(runLength))
            {
                code = BlackLen6TermCodes[runLength];
                codeLength = 6;
                return true;
            }

            if (BlackLen7TermCodes.ContainsKey(runLength))
            {
                code = BlackLen7TermCodes[runLength];
                codeLength = 7;
                return true;
            }

            if (BlackLen8TermCodes.ContainsKey(runLength))
            {
                code = BlackLen8TermCodes[runLength];
                codeLength = 8;
                return true;
            }

            if (BlackLen9TermCodes.ContainsKey(runLength))
            {
                code = BlackLen9TermCodes[runLength];
                codeLength = 9;
                return true;
            }

            if (BlackLen10TermCodes.ContainsKey(runLength))
            {
                code = BlackLen10TermCodes[runLength];
                codeLength = 10;
                return true;
            }

            if (BlackLen11TermCodes.ContainsKey(runLength))
            {
                code = BlackLen11TermCodes[runLength];
                codeLength = 11;
                return true;
            }

            if (BlackLen12TermCodes.ContainsKey(runLength))
            {
                code = BlackLen12TermCodes[runLength];
                codeLength = 12;
                return true;
            }

            return false;
        }
    }
}

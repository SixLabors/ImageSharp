// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Bitreader for reading compressed CCITT T4 1D data.
    /// </summary>
    internal class T4BitReader : IDisposable
    {
        /// <summary>
        /// Number of bits read.
        /// </summary>
        private int bitsRead;

        /// <summary>
        /// Current value.
        /// </summary>
        private uint value;

        /// <summary>
        /// Number of bits read for the current run value.
        /// </summary>
        private int curValueBitsRead;

        /// <summary>
        /// Byte position in the buffer.
        /// </summary>
        private ulong position;

        /// <summary>
        /// Indicates whether its the first line of data which is read from the image.
        /// </summary>
        private bool isFirstScanLine;

        /// <summary>
        /// Indicates whether we have found a termination code which signals the end of a run.
        /// </summary>
        private bool terminationCodeFound;

        /// <summary>
        /// We keep track if its the start of the row, because each run is expected to start with a white run.
        /// If the image row itself starts with black, a white run of zero is expected.
        /// </summary>
        private bool isStartOfRow;

        /// <summary>
        /// Indicates whether the modified huffman compression, as specified in the TIFF spec in section 10, is used.
        /// </summary>
        private readonly bool isModifiedHuffmanRle;

        /// <summary>
        /// Indicates, if fill bits have been added as necessary before EOL codes such that EOL always ends on a byte boundary. Defaults to false.
        /// </summary>
        private readonly bool eolPadding;

        private readonly int dataLength;

        private const int MinCodeLength = 2;

        private readonly int maxCodeLength = 13;

        private static readonly Dictionary<uint, uint> WhiteLen4TermCodes = new Dictionary<uint, uint>()
        {
            { 0x7, 2 }, { 0x8, 3 }, { 0xB, 4 }, { 0xC, 5 }, { 0xE, 6 }, { 0xF, 7 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen5TermCodes = new Dictionary<uint, uint>()
        {
            { 0x13, 8 }, { 0x14, 9 }, { 0x7, 10 }, { 0x8, 11 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen6TermCodes = new Dictionary<uint, uint>()
        {
            { 0x7, 1 }, { 0x8, 12 }, { 0x3, 13 }, { 0x34, 14 }, { 0x35, 15 }, { 0x2A, 16 }, { 0x2B, 17 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen7TermCodes = new Dictionary<uint, uint>()
        {
            { 0x27, 18 }, { 0xC, 19 }, { 0x8, 20 }, { 0x17, 21 }, { 0x3, 22 }, { 0x4, 23 }, { 0x28, 24 }, { 0x2B, 25 }, { 0x13, 26 },
            { 0x24, 27 }, { 0x18, 28 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen8TermCodes = new Dictionary<uint, uint>()
        {
            { 0x35, 0 }, { 0x2, 29 }, { 0x3, 30 }, { 0x1A, 31 }, { 0x1B, 32 }, { 0x12, 33 }, { 0x13, 34 }, { 0x14, 35 }, { 0x15, 36 },
            { 0x16, 37 }, { 0x17, 38 }, { 0x28, 39 }, { 0x29, 40 }, { 0x2A, 41 }, { 0x2B, 42 }, { 0x2C, 43 }, { 0x2D, 44 }, { 0x4, 45 },
            { 0x5, 46 }, { 0xA, 47 }, { 0xB, 48 }, { 0x52, 49 }, { 0x53, 50 }, { 0x54, 51 }, { 0x55, 52 }, { 0x24, 53 }, { 0x25, 54 },
            { 0x58, 55 }, { 0x59, 56 }, { 0x5A, 57 }, { 0x5B, 58 }, { 0x4A, 59 }, { 0x4B, 60 }, { 0x32, 61 }, { 0x33, 62 }, { 0x34, 63 }
        };

        private static readonly Dictionary<uint, uint> BlackLen2TermCodes = new Dictionary<uint, uint>()
        {
            { 0x3, 2 }, { 0x2, 3 }
        };

        private static readonly Dictionary<uint, uint> BlackLen3TermCodes = new Dictionary<uint, uint>()
        {
            { 0x2, 1 }, { 0x3, 4 }
        };

        private static readonly Dictionary<uint, uint> BlackLen4TermCodes = new Dictionary<uint, uint>()
        {
            { 0x3, 5 }, { 0x2, 6 }
        };

        private static readonly Dictionary<uint, uint> BlackLen5TermCodes = new Dictionary<uint, uint>()
        {
            { 0x3, 7 }
        };

        private static readonly Dictionary<uint, uint> BlackLen6TermCodes = new Dictionary<uint, uint>()
        {
            { 0x5, 8 }, { 0x4, 9 }
        };

        private static readonly Dictionary<uint, uint> BlackLen7TermCodes = new Dictionary<uint, uint>()
        {
            { 0x4, 10 }, { 0x5, 11 }, { 0x7, 12 }
        };

        private static readonly Dictionary<uint, uint> BlackLen8TermCodes = new Dictionary<uint, uint>()
        {
            { 0x4, 13 }, { 0x7, 14 }
        };

        private static readonly Dictionary<uint, uint> BlackLen9TermCodes = new Dictionary<uint, uint>()
        {
            { 0x18, 15 }
        };

        private static readonly Dictionary<uint, uint> BlackLen10TermCodes = new Dictionary<uint, uint>()
        {
            { 0x37, 0 }, { 0x17, 16 }, { 0x18, 17 }, { 0x8, 18 }
        };

        private static readonly Dictionary<uint, uint> BlackLen11TermCodes = new Dictionary<uint, uint>()
        {
            { 0x67, 19 }, { 0x68, 20 }, { 0x6C, 21 }, { 0x37, 22 }, { 0x28, 23 }, { 0x17, 24 }, { 0x18, 25 }
        };

        private static readonly Dictionary<uint, uint> BlackLen12TermCodes = new Dictionary<uint, uint>()
        {
            { 0xCA, 26 }, { 0xCB, 27 }, { 0xCC, 28 }, { 0xCD, 29 }, { 0x68, 30 }, { 0x69, 31 }, { 0x6A, 32 }, { 0x6B, 33 }, { 0xD2, 34 },
            { 0xD3, 35 }, { 0xD4, 36 }, { 0xD5, 37 }, { 0xD6, 38 }, { 0xD7, 39 }, { 0x6C, 40 }, { 0x6D, 41 }, { 0xDA, 42 }, { 0xDB, 43 },
            { 0x54, 44 }, { 0x55, 45 }, { 0x56, 46 }, { 0x57, 47 }, { 0x64, 48 }, { 0x65, 49 }, { 0x52, 50 }, { 0x53, 51 }, { 0x24, 52 },
            { 0x37, 53 }, { 0x38, 54 }, { 0x27, 55 }, { 0x28, 56 }, { 0x58, 57 }, { 0x59, 58 }, { 0x2B, 59 }, { 0x2C, 60 }, { 0x5A, 61 },
            { 0x66, 62 }, { 0x67, 63 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen5MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x1B, 64 }, { 0x12, 128 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen6MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x17, 192 }, { 0x18, 1664 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen8MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x36, 320 }, { 0x37, 384 }, { 0x64, 448 }, { 0x65, 512 }, { 0x68, 576 }, { 0x67, 640 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen7MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x37, 256 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen9MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0xCC, 704 }, { 0xCD, 768 }, { 0xD2, 832 }, { 0xD3, 896 }, { 0xD4, 960 }, { 0xD5, 1024 }, { 0xD6, 1088 },
            { 0xD7, 1152 }, { 0xD8, 1216 }, { 0xD9, 1280 }, { 0xDA, 1344 }, { 0xDB, 1408 }, { 0x98, 1472 }, { 0x99, 1536 },
            { 0x9A, 1600 }, { 0x9B, 1728 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen11MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x8, 1792 }, { 0xC, 1856 }, { 0xD, 1920 }
        };

        private static readonly Dictionary<uint, uint> WhiteLen12MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x12, 1984 }, { 0x13, 2048 }, { 0x14, 2112 }, { 0x15, 2176 }, { 0x16, 2240 }, { 0x17, 2304 }, { 0x1C, 2368 },
            { 0x1D, 2432 }, { 0x1E, 2496 }, { 0x1F, 2560 }
        };

        private static readonly Dictionary<uint, uint> BlackLen10MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0xF, 64 }
        };

        private static readonly Dictionary<uint, uint> BlackLen11MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x8, 1792 }, { 0xC, 1856 }, { 0xD, 1920 }
        };

        private static readonly Dictionary<uint, uint> BlackLen12MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0xC8, 128 }, { 0xC9, 192 }, { 0x5B, 256 }, { 0x33, 320 }, { 0x34, 384 }, { 0x35, 448 },
            { 0x12, 1984 }, { 0x13, 2048 }, { 0x14, 2112 }, { 0x15, 2176 }, { 0x16, 2240 }, { 0x17, 2304 }, { 0x1C, 2368 },
            { 0x1D, 2432 }, { 0x1E, 2496 }, { 0x1F, 2560 }
        };

        private static readonly Dictionary<uint, uint> BlackLen13MakeupCodes = new Dictionary<uint, uint>()
        {
            { 0x6C, 512 }, { 0x6D, 576 }, { 0x4A, 640 }, { 0x4B, 704 }, { 0x4C, 768 }, { 0x4D, 832 }, { 0x72, 896 },
            { 0x73, 960 }, { 0x74, 1024 }, { 0x75, 1088 }, { 0x76, 1152 }, { 0x77, 1216 }, { 0x52, 1280 }, { 0x53, 1344 },
            { 0x54, 1408 }, { 0x55, 1472 }, { 0x5A, 1536 }, { 0x5B, 1600 }, { 0x64, 1664 }, { 0x65, 1728 }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="T4BitReader" /> class.
        /// </summary>
        /// <param name="input">The compressed input stream.</param>
        /// <param name="bytesToRead">The number of bytes to read from the stream.</param>
        /// <param name="allocator">The memory allocator.</param>
        /// <param name="eolPadding">Indicates, if fill bits have been added as necessary before EOL codes such that EOL always ends on a byte boundary. Defaults to false.</param>
        /// <param name="isModifiedHuffman">Indicates, if its the modified huffman code variation. Defaults to false.</param>
        public T4BitReader(Stream input, int bytesToRead, MemoryAllocator allocator, bool eolPadding = false, bool isModifiedHuffman = false)
        {
            this.Data = allocator.Allocate<byte>(bytesToRead);
            this.ReadImageDataFromStream(input, bytesToRead);

            this.isModifiedHuffmanRle = isModifiedHuffman;
            this.dataLength = bytesToRead;
            this.bitsRead = 0;
            this.value = 0;
            this.curValueBitsRead = 0;
            this.position = 0;
            this.IsWhiteRun = true;
            this.isFirstScanLine = true;
            this.isStartOfRow = true;
            this.terminationCodeFound = false;
            this.RunLength = 0;
            this.eolPadding = eolPadding;

            if (this.eolPadding)
            {
                this.maxCodeLength = 24;
            }
        }

        /// <summary>
        /// Gets the compressed image data.
        /// </summary>
        public IMemoryOwner<byte> Data { get; }

        /// <summary>
        /// Gets a value indicating whether there is more data to read left.
        /// </summary>
        public bool HasMoreData
        {
            get
            {
                if (this.isModifiedHuffmanRle)
                {
                    return this.position < (ulong)this.dataLength - 1 || (this.bitsRead > 0 && this.bitsRead < 7);
                }

                return this.position < (ulong)this.dataLength - 1;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current run is a white pixel run, otherwise its a black pixel run.
        /// </summary>
        public bool IsWhiteRun { get; private set; }

        /// <summary>
        /// Gets the number of pixels in the current run.
        /// </summary>
        public uint RunLength { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the end of a pixel row has been reached.
        /// </summary>
        public bool IsEndOfScanLine
        {
            get
            {
                if (this.eolPadding)
                {
                    return this.curValueBitsRead >= 12 && this.value == 1;
                }

                return this.curValueBitsRead == 12 && this.value == 1;
            }
        }

        /// <summary>
        /// Read the next run of pixels.
        /// </summary>
        public void ReadNextRun()
        {
            if (this.terminationCodeFound)
            {
                this.IsWhiteRun = !this.IsWhiteRun;
                this.terminationCodeFound = false;
            }

            this.Reset();

            if (this.isFirstScanLine && !this.isModifiedHuffmanRle)
            {
                // We expect an EOL before the first data.
                this.value = this.ReadValue(this.eolPadding ? 16 : 12);

                if (!this.IsEndOfScanLine)
                {
                    TiffThrowHelper.ThrowImageFormatException("t4 parsing error: expected start of data marker not found");
                }

                this.Reset();
            }

            // A code word must have at least 2 bits.
            this.value = this.ReadValue(MinCodeLength);

            do
            {
                if (this.curValueBitsRead > this.maxCodeLength)
                {
                    TiffThrowHelper.ThrowImageFormatException("t4 parsing error: invalid code length read");
                }

                bool isMakeupCode = this.IsMakeupCode();
                if (isMakeupCode)
                {
                    if (this.IsWhiteRun)
                    {
                        this.RunLength += this.WhiteMakeupCodeRunLength();
                    }
                    else
                    {
                        this.RunLength += this.BlackMakeupCodeRunLength();
                    }

                    this.isStartOfRow = false;
                    this.Reset(resetRunLength: false);
                    continue;
                }

                bool isTerminatingCode = this.IsTerminatingCode();
                if (isTerminatingCode)
                {
                    // Each line starts with a white run. If the image starts with black, a white run with length zero is written.
                    if (this.isStartOfRow && this.IsWhiteRun && this.WhiteTerminatingCodeRunLength() == 0)
                    {
                        this.IsWhiteRun = !this.IsWhiteRun;
                        this.Reset();
                        this.isStartOfRow = false;
                        continue;
                    }

                    if (this.IsWhiteRun)
                    {
                        this.RunLength += this.WhiteTerminatingCodeRunLength();
                    }
                    else
                    {
                        this.RunLength += this.BlackTerminatingCodeRunLength();
                    }

                    this.terminationCodeFound = true;
                    this.isStartOfRow = false;
                    break;
                }

                var currBit = this.ReadValue(1);
                this.value = (this.value << 1) | currBit;

                if (this.IsEndOfScanLine)
                {
                    this.StartNewRow();
                }
            }
            while (!this.IsEndOfScanLine);

            this.isFirstScanLine = false;
        }

        public void StartNewRow()
        {
            // Each new row starts with a white run.
            this.IsWhiteRun = true;
            this.isStartOfRow = true;
            this.terminationCodeFound = false;

            if (this.isModifiedHuffmanRle)
            {
                int pad = 8 - (this.bitsRead % 8);
                if (pad != 8)
                {
                    // Skip padding bits, move to next byte.
                    this.position++;
                    this.bitsRead = 0;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose() => this.Data.Dispose();

        private uint WhiteTerminatingCodeRunLength()
        {
            switch (this.curValueBitsRead)
            {
                case 4:
                {
                    return WhiteLen4TermCodes[this.value];
                }

                case 5:
                {
                    return WhiteLen5TermCodes[this.value];
                }

                case 6:
                {
                    return WhiteLen6TermCodes[this.value];
                }

                case 7:
                {
                    return WhiteLen7TermCodes[this.value];
                }

                case 8:
                {
                    return WhiteLen8TermCodes[this.value];
                }
            }

            return 0;
        }

        private uint BlackTerminatingCodeRunLength()
        {
            switch (this.curValueBitsRead)
            {
                case 2:
                {
                    return BlackLen2TermCodes[this.value];
                }

                case 3:
                {
                    return BlackLen3TermCodes[this.value];
                }

                case 4:
                {
                    return BlackLen4TermCodes[this.value];
                }

                case 5:
                {
                    return BlackLen5TermCodes[this.value];
                }

                case 6:
                {
                    return BlackLen6TermCodes[this.value];
                }

                case 7:
                {
                    return BlackLen7TermCodes[this.value];
                }

                case 8:
                {
                    return BlackLen8TermCodes[this.value];
                }

                case 9:
                {
                    return BlackLen9TermCodes[this.value];
                }

                case 10:
                {
                    return BlackLen10TermCodes[this.value];
                }

                case 11:
                {
                    return BlackLen11TermCodes[this.value];
                }

                case 12:
                {
                    return BlackLen12TermCodes[this.value];
                }
            }

            return 0;
        }

        private uint WhiteMakeupCodeRunLength()
        {
            switch (this.curValueBitsRead)
            {
                case 5:
                {
                    return WhiteLen5MakeupCodes[this.value];
                }

                case 6:
                {
                    return WhiteLen6MakeupCodes[this.value];
                }

                case 7:
                {
                    return WhiteLen7MakeupCodes[this.value];
                }

                case 8:
                {
                    return WhiteLen8MakeupCodes[this.value];
                }

                case 9:
                {
                    return WhiteLen9MakeupCodes[this.value];
                }

                case 11:
                {
                    return WhiteLen11MakeupCodes[this.value];
                }

                case 12:
                {
                    return WhiteLen12MakeupCodes[this.value];
                }
            }

            return 0;
        }

        private uint BlackMakeupCodeRunLength()
        {
            switch (this.curValueBitsRead)
            {
                case 10:
                {
                    return BlackLen10MakeupCodes[this.value];
                }

                case 11:
                {
                    return BlackLen11MakeupCodes[this.value];
                }

                case 12:
                {
                    return BlackLen12MakeupCodes[this.value];
                }

                case 13:
                {
                    return BlackLen13MakeupCodes[this.value];
                }
            }

            return 0;
        }

        private bool IsMakeupCode()
        {
            if (this.IsWhiteRun)
            {
                return this.IsWhiteMakeupCode();
            }

            return this.IsBlackMakeupCode();
        }

        private bool IsWhiteMakeupCode()
        {
            switch (this.curValueBitsRead)
            {
                case 5:
                {
                    return WhiteLen5MakeupCodes.ContainsKey(this.value);
                }

                case 6:
                {
                    return WhiteLen6MakeupCodes.ContainsKey(this.value);
                }

                case 7:
                {
                    return WhiteLen7MakeupCodes.ContainsKey(this.value);
                }

                case 8:
                {
                    return WhiteLen8MakeupCodes.ContainsKey(this.value);
                }

                case 9:
                {
                    return WhiteLen9MakeupCodes.ContainsKey(this.value);
                }

                case 11:
                {
                    return WhiteLen11MakeupCodes.ContainsKey(this.value);
                }

                case 12:
                {
                    if (this.isModifiedHuffmanRle)
                    {
                        if (this.value == 1)
                        {
                            return true;
                        }
                    }

                    return WhiteLen12MakeupCodes.ContainsKey(this.value);
                }
            }

            return false;
        }

        private bool IsBlackMakeupCode()
        {
            switch (this.curValueBitsRead)
            {
                case 10:
                {
                    return BlackLen10MakeupCodes.ContainsKey(this.value);
                }

                case 11:
                {
                    if (this.isModifiedHuffmanRle)
                    {
                        if (this.value == 0)
                        {
                            return true;
                        }
                    }

                    return BlackLen11MakeupCodes.ContainsKey(this.value);
                }

                case 12:
                {
                    return BlackLen12MakeupCodes.ContainsKey(this.value);
                }

                case 13:
                {
                    return BlackLen13MakeupCodes.ContainsKey(this.value);
                }
            }

            return false;
        }

        private bool IsTerminatingCode()
        {
            if (this.IsWhiteRun)
            {
                return this.IsWhiteTerminatingCode();
            }

            return this.IsBlackTerminatingCode();
        }

        private bool IsWhiteTerminatingCode()
        {
            switch (this.curValueBitsRead)
            {
                case 4:
                {
                    return WhiteLen4TermCodes.ContainsKey(this.value);
                }

                case 5:
                {
                    return WhiteLen5TermCodes.ContainsKey(this.value);
                }

                case 6:
                {
                    return WhiteLen6TermCodes.ContainsKey(this.value);
                }

                case 7:
                {
                    return WhiteLen7TermCodes.ContainsKey(this.value);
                }

                case 8:
                {
                    return WhiteLen8TermCodes.ContainsKey(this.value);
                }
            }

            return false;
        }

        private bool IsBlackTerminatingCode()
        {
            switch (this.curValueBitsRead)
            {
                case 2:
                {
                    return BlackLen2TermCodes.ContainsKey(this.value);
                }

                case 3:
                {
                    return BlackLen3TermCodes.ContainsKey(this.value);
                }

                case 4:
                {
                    return BlackLen4TermCodes.ContainsKey(this.value);
                }

                case 5:
                {
                    return BlackLen5TermCodes.ContainsKey(this.value);
                }

                case 6:
                {
                    return BlackLen6TermCodes.ContainsKey(this.value);
                }

                case 7:
                {
                    return BlackLen7TermCodes.ContainsKey(this.value);
                }

                case 8:
                {
                    return BlackLen8TermCodes.ContainsKey(this.value);
                }

                case 9:
                {
                    return BlackLen9TermCodes.ContainsKey(this.value);
                }

                case 10:
                {
                    return BlackLen10TermCodes.ContainsKey(this.value);
                }

                case 11:
                {
                    return BlackLen11TermCodes.ContainsKey(this.value);
                }

                case 12:
                {
                    return BlackLen12TermCodes.ContainsKey(this.value);
                }
            }

            return false;
        }

        private void Reset(bool resetRunLength = true)
        {
            this.value = 0;
            this.curValueBitsRead = 0;

            if (resetRunLength)
            {
                this.RunLength = 0;
            }
        }

        private uint ReadValue(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));

            uint v = 0;
            int shift = nBits;
            while (shift-- > 0)
            {
                uint bit = this.GetBit();
                v |= bit << shift;
                this.curValueBitsRead++;
            }

            return v;
        }

        private uint GetBit()
        {
            if (this.bitsRead >= 8)
            {
                this.LoadNewByte();
            }

            Span<byte> dataSpan = this.Data.GetSpan();
            int shift = 8 - this.bitsRead - 1;
            var bit = (uint)((dataSpan[(int)this.position] & (1 << shift)) != 0 ? 1 : 0);
            this.bitsRead++;

            return bit;
        }

        private void LoadNewByte()
        {
            this.position++;
            this.bitsRead = 0;

            if (this.position >= (ulong)this.dataLength)
            {
                TiffThrowHelper.ThrowImageFormatException("tiff image has invalid t4 compressed data");
            }
        }

        private void ReadImageDataFromStream(Stream input, int bytesToRead)
        {
            Span<byte> dataSpan = this.Data.GetSpan();
            input.Read(dataSpan, 0, bytesToRead);
        }
    }
}

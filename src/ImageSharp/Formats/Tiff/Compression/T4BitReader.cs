// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Bitreader for reading compressed CCITT T4 1D data.
    /// </summary>
    internal class T4BitReader
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
        /// Indicates, if the current run are white pixels.
        /// </summary>
        private bool isWhiteRun;

        /// <summary>
        /// Indicates whether its the first line of data which is read from the image.
        /// </summary>
        private bool isFirstScanLine;

        private bool terminationCodeFound;

        /// <summary>
        /// Number of pixels in the current run.
        /// </summary>
        private uint runLength;

        private const int MinCodeLength = 2;

        private const int MaxCodeLength = 13;

        public T4BitReader(Stream input, int bytesToRead)
        {
            // TODO: use memory allocator
            this.Data = new byte[bytesToRead];
            this.ReadImageDataFromStream(input, bytesToRead);

            this.bitsRead = 0;
            this.value = 0;
            this.curValueBitsRead = 0;
            this.position = 0;
            this.isWhiteRun = true;
            this.isFirstScanLine = true;
            this.terminationCodeFound = false;
            this.runLength = 0;
        }

        /// <summary>
        /// Gets the compressed image data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets a value indicating whether there is more data to read left.
        /// </summary>
        public bool HasMoreData
        {
            get
            {
                return this.position < (ulong)this.Data.Length - 1;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current run is a white pixel run, otherwise its a black pixel run.
        /// </summary>
        public bool IsWhiteRun
        {
            get
            {
                return this.isWhiteRun;
            }
        }

        /// <summary>
        /// Gets the number of pixels in the current run.
        /// </summary>
        public uint RunLength
        {
            get
            {
                return this.runLength;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the end of a pixel row has been reached.
        /// </summary>
        public bool IsEndOfScanLine
        {
            get
            {
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
                this.isWhiteRun = !this.IsWhiteRun;
                this.terminationCodeFound = false;
            }

            this.Reset();

            if (this.isFirstScanLine)
            {
                // We expect an EOL before the first data.
                this.value = this.ReadValue(12);
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
                if (this.curValueBitsRead > MaxCodeLength)
                {
                    TiffThrowHelper.ThrowImageFormatException("t4 parsing error: invalid code length read");
                }

                bool isTerminatingCode = this.IsTerminatingCode();
                if (isTerminatingCode)
                {
                    // Each line starts with a white run. If the image starts with black, a white run with length zero is written.
                    if (this.IsWhiteRun && this.WhiteTerminatingCodeRunLength() == 0)
                    {
                        this.isWhiteRun = !this.IsWhiteRun;
                        this.Reset();
                        continue;
                    }

                    if (this.IsWhiteRun)
                    {
                        this.runLength += this.WhiteTerminatingCodeRunLength();
                    }
                    else
                    {
                        this.runLength += this.BlackTerminatingCodeRunLength();
                    }

                    this.terminationCodeFound = true;
                    break;
                }

                bool isMakeupCode = this.IsMakeupCode();
                if (isMakeupCode)
                {
                    if (this.IsWhiteRun)
                    {
                        this.runLength += this.WhiteMakeupCodeRunLength();
                    }
                    else
                    {
                        this.runLength += this.BlackMakeupCodeRunLength();
                    }

                    this.Reset(false);
                    continue;
                }

                var currBit = this.ReadValue(1);
                this.value = (this.value << 1) | currBit;

                if (this.IsEndOfScanLine)
                {
                    // Each new row starts with a white run.
                    this.isWhiteRun = true;
                }
            }
            while (!this.IsEndOfScanLine);

            this.isFirstScanLine = false;
        }

        private uint WhiteTerminatingCodeRunLength()
        {
            switch (this.curValueBitsRead)
            {
                case 4:
                {
                    switch (this.value)
                    {
                        case 0x7:
                            return 2;
                        case 0x8:
                            return 3;
                        case 0xB:
                            return 4;
                        case 0xC:
                            return 5;
                        case 0xE:
                            return 6;
                        case 0xF:
                            return 7;
                    }

                    break;
                }

                case 5:
                {
                    switch (this.value)
                    {
                        case 0x13:
                            return 8;
                        case 0x14:
                            return 9;
                        case 0x7:
                            return 10;
                        case 0x8:
                            return 11;
                    }

                    break;
                }

                case 6:
                {
                    switch (this.value)
                    {
                        case 0x7:
                            return 1;
                        case 0x8:
                            return 12;
                        case 0x3:
                            return 13;
                        case 0x34:
                            return 14;
                        case 0x35:
                            return 15;
                        case 0x2A:
                            return 16;
                        case 0x2B:
                            return 17;
                    }

                    break;
                }

                case 7:
                {
                    switch (this.value)
                    {
                        case 0x27:
                            return 18;
                        case 0xC:
                            return 19;
                        case 0x8:
                            return 20;
                        case 0x17:
                            return 21;
                        case 0x3:
                            return 22;
                        case 0x4:
                            return 23;
                        case 0x28:
                            return 24;
                        case 0x2B:
                            return 25;
                        case 0x13:
                            return 26;
                        case 0x24:
                            return 27;
                        case 0x18:
                            return 28;
                    }

                    break;
                }

                case 8:
                {
                    switch (this.value)
                    {
                        case 0x35:
                            return 0;
                        case 0x2:
                            return 29;
                        case 0x3:
                            return 30;
                        case 0x1A:
                            return 31;
                        case 0x1B:
                            return 32;
                        case 0x12:
                            return 33;
                        case 0x13:
                            return 34;
                        case 0x14:
                            return 35;
                        case 0x15:
                            return 36;
                        case 0x16:
                            return 37;
                        case 0x17:
                            return 38;
                        case 0x28:
                            return 39;
                        case 0x29:
                            return 40;
                        case 0x2A:
                            return 41;
                        case 0x2B:
                            return 42;
                        case 0x2C:
                            return 43;
                        case 0x2D:
                            return 44;
                        case 0x4:
                            return 45;
                        case 0x5:
                            return 46;
                        case 0xA:
                            return 47;
                        case 0xB:
                            return 48;
                        case 0x52:
                            return 49;
                        case 0x53:
                            return 50;
                        case 0x54:
                            return 51;
                        case 0x55:
                            return 52;
                        case 0x24:
                            return 53;
                        case 0x25:
                            return 54;
                        case 0x58:
                            return 55;
                        case 0x59:
                            return 56;
                        case 0x5A:
                            return 57;
                        case 0x5B:
                            return 58;
                        case 0x4A:
                            return 59;
                        case 0x4B:
                            return 60;
                        case 0x32:
                            return 61;
                        case 0x33:
                            return 62;
                        case 0x34:
                            return 63;
                    }

                    break;
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
                    switch (this.value)
                    {
                        case 0x3:
                            return 2;
                        case 0x2:
                            return 3;
                    }
                    break;
                }

                case 3:
                {
                    switch (this.value)
                    {
                        case 0x2:
                            return 1;
                        case 0x3:
                            return 4;
                    }

                    break;
                }

                case 4:
                {
                    switch (this.value)
                    {
                        case 0x3:
                            return 5;
                        case 0x2:
                            return 6;
                    }

                    break;
                }

                case 5:
                {
                    switch (this.value)
                    {
                        case 0x3:
                            return 7;
                    }

                    break;
                }

                case 6:
                {
                    switch (this.value)
                    {
                        case 0x5:
                            return 8;
                        case 0x4:
                            return 9;
                    }

                    break;
                }

                case 7:
                {
                    switch (this.value)
                    {
                        case 0x4:
                            return 10;
                        case 0x5:
                            return 11;
                        case 0x7:
                            return 12;
                    }

                    break;
                }

                case 8:
                {
                    switch (this.value)
                    {
                        case 0x4:
                            return 13;
                        case 0x7:
                            return 14;
                    }

                    break;
                }

                case 9:
                {
                    switch (this.value)
                    {
                        case 0x18:
                            return 15;
                    }

                    break;
                }

                case 10:
                {
                    switch (this.value)
                    {
                        case 0x37:
                            return 0;
                        case 0x17:
                            return 16;
                        case 0x18:
                            return 17;
                        case 0x8:
                            return 18;
                    }

                    break;
                }

                case 11:
                {
                    switch (this.value)
                    {
                        case 0x67:
                            return 19;
                        case 0x68:
                            return 20;
                        case 0x6C:
                            return 21;
                        case 0x37:
                            return 22;
                        case 0x28:
                            return 23;
                        case 0x17:
                            return 24;
                        case 0x18:
                            return 25;
                    }

                    break;
                }

                case 12:
                {
                    switch (this.value)
                    {
                        case 0xCA:
                            return 26;
                        case 0xCB:
                            return 27;
                        case 0xCC:
                            return 28;
                        case 0xCD:
                            return 29;
                        case 0x68:
                            return 30;
                        case 0x69:
                            return 31;
                        case 0x6A:
                            return 32;
                        case 0x6B:
                            return 33;
                        case 0xD2:
                            return 34;
                        case 0xD3:
                            return 35;
                        case 0xD4:
                            return 36;
                        case 0xD5:
                            return 37;
                        case 0xD6:
                            return 38;
                        case 0xD7:
                            return 39;
                        case 0x6C:
                            return 40;
                        case 0x6D:
                            return 41;
                        case 0xDA:
                            return 42;
                        case 0xDB:
                            return 43;
                        case 0x54:
                            return 44;
                        case 0x55:
                            return 45;
                        case 0x56:
                            return 46;
                        case 0x57:
                            return 47;
                        case 0x64:
                            return 48;
                        case 0x65:
                            return 49;
                        case 0x52:
                            return 50;
                        case 0x53:
                            return 51;
                        case 0x24:
                            return 52;
                        case 0x37:
                            return 53;
                        case 0x38:
                            return 54;
                        case 0x27:
                            return 55;
                        case 0x28:
                            return 56;
                        case 0x58:
                            return 57;
                        case 0x59:
                            return 58;
                        case 0x2B:
                            return 59;
                        case 0x2C:
                            return 60;
                        case 0x5A:
                            return 61;
                        case 0x66:
                            return 62;
                        case 0x67:
                            return 63;
                    }

                    break;
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
                    switch (this.value)
                    {
                        case 0x1B:
                            return 64;
                        case 0x12:
                            return 128;
                    }

                    break;
                }

                case 6:
                {
                    switch (this.value)
                    {
                        case 0x17:
                            return 192;
                        case 0x18:
                            return 1664;
                    }

                    break;
                }

                case 7:
                {
                    switch (this.value)
                    {
                        case 0x37:
                            return 256;
                    }

                    break;
                }

                case 8:
                {
                    switch (this.value)
                    {
                        case 0x36:
                            return 320;
                        case 0x37:
                            return 348;
                        case 0x64:
                            return 448;
                        case 0x65:
                            return 512;
                        case 0x68:
                            return 576;
                        case 0x67:
                            return 640;
                    }

                    break;
                }

                case 9:
                {
                    switch (this.value)
                    {
                        case 0xCC:
                            return 704;
                        case 0xCD:
                            return 768;
                        case 0xD2:
                            return 832;
                        case 0xD3:
                            return 896;
                        case 0xD4:
                            return 960;
                        case 0xD5:
                            return 1024;
                        case 0xD6:
                            return 1088;
                        case 0xD7:
                            return 1152;
                        case 0xD8:
                            return 1216;
                        case 0xD9:
                            return 1280;
                        case 0xDA:
                            return 1344;
                        case 0xDB:
                            return 1408;
                        case 0x98:
                            return 1472;
                        case 0x99:
                            return 1536;
                        case 0x9A:
                            return 1600;
                        case 0x9B:
                            return 1728;
                    }

                    break;
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
                    switch (this.value)
                    {
                        case 0xF:
                            return 64;
                    }
                }

                break;

                case 12:
                {
                    switch (this.value)
                    {
                        case 0xC8:
                            return 128;
                        case 0xC9:
                            return 192;
                        case 0x5B:
                            return 256;
                        case 0x33:
                            return 320;
                        case 0x34:
                            return 384;
                        case 0x35:
                            return 448;
                    }
                }

                break;

                case 13:
                {
                    switch (this.value)
                    {
                        case 0x6C:
                            return 512;
                        case 0x6D:
                            return 576;
                        case 0x4A:
                            return 640;
                        case 0x4B:
                            return 704;
                        case 0x4C:
                            return 768;
                        case 0x4D:
                            return 832;
                        case 0x72:
                            return 896;
                        case 0x73:
                            return 960;
                        case 0x74:
                            return 1024;
                        case 0x75:
                            return 1088;
                        case 0x76:
                            return 1152;
                        case 0x77:
                            return 1216;
                        case 0x52:
                            return 1280;
                        case 0x53:
                            return 1344;
                        case 0x54:
                            return 1408;
                        case 0x55:
                            return 1472;
                        case 0x5A:
                            return 1536;
                        case 0x5B:
                            return 1600;
                        case 0x64:
                            return 1664;
                        case 0x65:
                            return 1728;
                    }

                    break;
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
                    uint[] codes = { 0x1B, 0x12 };
                    return codes.Contains(this.value);
                }

                case 6:
                {
                    uint[] codes = { 0x17, 0x18 };
                    return codes.Contains(this.value);
                }

                case 7:
                {
                    uint[] codes = { 0x37 };
                    return codes.Contains(this.value);
                }

                case 8:
                {
                    uint[] codes = { 0x36, 0x37, 0x64, 0x65, 0x68, 0x67 };
                    return codes.Contains(this.value);
                }

                case 9:
                {
                    uint[] codes =
                    {
                        0xCC, 0xCD, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0x98,
                        0x99, 0x9A, 0x9B
                    };
                    return codes.Contains(this.value);
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
                    uint[] codes = { 0xF };
                    return codes.Contains(this.value);
                }

                case 12:
                {
                    uint[] codes = { 0xC8, 0xC9, 0x5B, 0x33, 0x34, 0x35 };
                    return codes.Contains(this.value);
                }

                case 13:
                {
                    uint[] codes =
                    {
                        0x6C, 0x6D, 0x4A, 0x4B, 0x4C, 0x4D, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x52,
                        0x53, 0x54, 0x55, 0x5A, 0x5B, 0x64, 0x65
                    };
                    return codes.Contains(this.value);
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
                    uint[] codes = { 0x7, 0x8, 0xB, 0xC, 0xE, 0xF };
                    return codes.Contains(this.value);
                }

                case 5:
                {
                    uint[] codes = { 0x13, 0x14, 0x7, 0x8 };
                    return codes.Contains(this.value);
                }

                case 6:
                {
                    uint[] codes = { 0x7, 0x8, 0x3, 0x34, 0x35, 0x2A, 0x2B };
                    return codes.Contains(this.value);
                }

                case 7:
                {
                    uint[] codes = { 0x27, 0xC, 0x8, 0x17, 0x3, 0x4, 0x28, 0x2B, 0x13, 0x24, 0x18 };
                    return codes.Contains(this.value);
                }

                case 8:
                {
                    uint[] codes =
                    {
                        0x35, 0x2, 0x3, 0x1A, 0x1B, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x28, 0x29,
                        0x2A, 0x2B, 0x2C, 0x2D, 0x4, 0x5, 0xA, 0xB, 0x52, 0x53, 0x54, 0x55, 0x24, 0x25,
                        0x58, 0x59, 0x5A, 0x5B, 0x4A, 0x4B, 0x32, 0x33, 0x34
                    };
                    return codes.Contains(this.value);
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
                    uint[] codes = {0x3, 0x2};
                    return codes.Contains(this.value);
                }

                case 3:
                {
                    uint[] codes = {0x02, 0x03};
                    return codes.Contains(this.value);
                }

                case 4:
                {
                    uint[] codes = {0x03, 0x02};
                    return codes.Contains(this.value);
                }

                case 5:
                {
                    uint[] codes = {0x03};
                    return codes.Contains(this.value);
                }

                case 6:
                {
                    uint[] codes = {0x5, 0x4};
                    return codes.Contains(this.value);
                }

                case 7:
                {
                    uint[] codes = { 0x4, 0x5, 0x7 };
                    return codes.Contains(this.value);
                }

                case 8:
                {
                    uint[] codes = { 0x4, 0x7 };
                    return codes.Contains(this.value);
                }

                case 9:
                {
                    uint[] codes = { 0x18 };
                    return codes.Contains(this.value);
                }

                case 10:
                {
                    uint[] codes = { 0x37, 0x17, 0x18, 0x8 };
                    return codes.Contains(this.value);
                }

                case 11:
                {
                    uint[] codes = { 0x67, 0x68, 0x6C, 0x37, 0x28, 0x17, 0x18 };
                    return codes.Contains(this.value);
                }

                case 12:
                {
                    uint[] codes =
                    {
                        0xCA, 0xCB, 0xCC, 0xCD, 0x68, 0x69, 0x6A, 0x6B, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6,
                        0xD7, 0x6C, 0x6D, 0xDA, 0xDB, 0x54, 0x55, 0x56, 0x57, 0x64, 0x65, 0x52, 0x53,
                        0x24, 0x37, 0x38, 0x27, 0x28, 0x58, 0x59, 0x2B, 0x2C, 0x5A, 0x66, 0x67
                    };
                    return codes.Contains(this.value);
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
                this.runLength = 0;
            }
        }

        private uint ReadValue(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));
            Guard.MustBeLessThanOrEqualTo(nBits, 12, nameof(nBits));

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

            int shift = 8 - this.bitsRead - 1;
            var bit = (uint)((this.Data[this.position] & (1 << shift)) != 0 ? 1 : 0);
            this.bitsRead++;

            return bit;
        }

        private void LoadNewByte()
        {
            this.position++;
            this.bitsRead = 0;

            if (this.position >= (ulong)this.Data.Length)
            {
                TiffThrowHelper.ThrowImageFormatException("tiff image has invalid t4 compressed data");
            }
        }

        private void ReadImageDataFromStream(Stream input, int bytesToRead)
        {
            var buffer = new byte[4096];

            Span<byte> bufferSpan = buffer.AsSpan();
            Span<byte> dataSpan = this.Data.AsSpan();

            int read;
            while (bytesToRead > 0 &&
                   (read = input.Read(buffer, 0, Math.Min(bufferSpan.Length, bytesToRead))) > 0)
            {
                buffer.AsSpan(0, read).CopyTo(dataSpan);
                bytesToRead -= read;
                dataSpan = dataSpan.Slice(read);
            }

            if (bytesToRead > 0)
            {
                TiffThrowHelper.ThrowImageFormatException("tiff image file has insufficient data");
            }
        }
}
}

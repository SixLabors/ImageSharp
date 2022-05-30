// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Bit reader for reading CCITT T6 compressed fax data.
    /// See: Facsimile Coding Schemes and Coding Control Functions for Group 4 Facsimile Apparatus, itu-t recommendation t.6
    /// </summary>
    internal sealed class T6BitReader : T4BitReader
    {
        private readonly int maxCodeLength = 12;

        private static readonly CcittTwoDimensionalCode None = new(CcittTwoDimensionalCodeType.None, 0);

        private static readonly Dictionary<uint, CcittTwoDimensionalCode> Len1Codes = new()
        {
            { 0b1, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.Vertical0, 1) }
        };

        private static readonly Dictionary<uint, CcittTwoDimensionalCode> Len3Codes = new()
        {
            { 0b001, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.Horizontal, 3) },
            { 0b010, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.VerticalL1, 3) },
            { 0b011, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.VerticalR1, 3) }
        };

        private static readonly Dictionary<uint, CcittTwoDimensionalCode> Len4Codes = new()
        {
            { 0b0001, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.Pass, 4) }
        };

        private static readonly Dictionary<uint, CcittTwoDimensionalCode> Len6Codes = new()
        {
            { 0b000011, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.VerticalR2, 6) },
            { 0b000010, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.VerticalL2, 6) }
        };

        private static readonly Dictionary<uint, CcittTwoDimensionalCode> Len7Codes = new()
        {
            { 0b0000011, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.VerticalR3, 7) },
            { 0b0000010, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.VerticalL3, 7) },
            { 0b0000001, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.Extensions2D, 7) },
            { 0b0000000, new CcittTwoDimensionalCode(CcittTwoDimensionalCodeType.Extensions1D, 7) }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="T6BitReader"/> class.
        /// </summary>
        /// <param name="input">The compressed input stream.</param>
        /// <param name="fillOrder">The logical order of bits within a byte.</param>
        /// <param name="bytesToRead">The number of bytes to read from the stream.</param>
        public T6BitReader(BufferedReadStream input, TiffFillOrder fillOrder, int bytesToRead)
            : base(input, fillOrder, bytesToRead)
        {
        }

        /// <inheritdoc/>
        public override bool HasMoreData => this.Position < (ulong)this.DataLength - 1 || ((uint)(this.BitsRead - 1) < (7 - 1));

        /// <summary>
        /// Gets or sets the two dimensional code.
        /// </summary>
        public CcittTwoDimensionalCode Code { get; internal set; }

        public bool ReadNextCodeWord()
        {
            this.Code = None;
            this.Reset();
            uint value = this.ReadValue(1);

            do
            {
                if (this.CurValueBitsRead > this.maxCodeLength)
                {
                    TiffThrowHelper.ThrowImageFormatException("ccitt compression parsing error: invalid code length read");
                }

                switch (this.CurValueBitsRead)
                {
                    case 1:
                        if (Len1Codes.ContainsKey(value))
                        {
                            this.Code = Len1Codes[value];
                            return false;
                        }

                        break;

                    case 3:
                        if (Len3Codes.ContainsKey(value))
                        {
                            this.Code = Len3Codes[value];
                            return false;
                        }

                        break;

                    case 4:
                        if (Len4Codes.ContainsKey(value))
                        {
                            this.Code = Len4Codes[value];
                            return false;
                        }

                        break;

                    case 6:
                        if (Len6Codes.ContainsKey(value))
                        {
                            this.Code = Len6Codes[value];
                            return false;
                        }

                        break;

                    case 7:
                        if (Len7Codes.ContainsKey(value))
                        {
                            this.Code = Len7Codes[value];
                            return false;
                        }

                        break;
                }

                uint currBit = this.ReadValue(1);
                value = (value << 1) | currBit;
            }
            while (!this.IsEndOfScanLine);

            if (this.IsEndOfScanLine)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// No EOL is expected at the start of a run.
        /// </summary>
        protected override void ReadEolBeforeFirstData()
        {
            // Nothing to do here.
        }

        /// <summary>
        /// Swaps the white run to black run an vise versa.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void SwapColor() => this.IsWhiteRun = !this.IsWhiteRun;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

        private static readonly CcittTwoDimensionalCode None = new(0, CcittTwoDimensionalCodeType.None, 0);

        private static readonly CcittTwoDimensionalCode Len1Code1 = new(0b1, CcittTwoDimensionalCodeType.Vertical0, 1);

        private static readonly CcittTwoDimensionalCode Len3Code001 = new(0b001, CcittTwoDimensionalCodeType.Horizontal, 3);
        private static readonly CcittTwoDimensionalCode Len3Code010 = new(0b010, CcittTwoDimensionalCodeType.VerticalL1, 3);
        private static readonly CcittTwoDimensionalCode Len3Code011 = new(0b011, CcittTwoDimensionalCodeType.VerticalR1, 3);

        private static readonly CcittTwoDimensionalCode Len4Code0001 = new(0b0001, CcittTwoDimensionalCodeType.Pass, 4);

        private static readonly CcittTwoDimensionalCode Len6Code000011 = new(0b000011, CcittTwoDimensionalCodeType.VerticalR2, 6);
        private static readonly CcittTwoDimensionalCode Len6Code000010 = new(0b000010, CcittTwoDimensionalCodeType.VerticalL2, 6);

        private static readonly CcittTwoDimensionalCode Len7Code0000011 = new(0b0000011, CcittTwoDimensionalCodeType.VerticalR3, 7);
        private static readonly CcittTwoDimensionalCode Len7Code0000010 = new(0b0000010, CcittTwoDimensionalCodeType.VerticalL3, 7);
        private static readonly CcittTwoDimensionalCode Len7Code0000001 = new(0b0000001, CcittTwoDimensionalCodeType.Extensions2D, 7);
        private static readonly CcittTwoDimensionalCode Len7Code0000000 = new(0b0000000, CcittTwoDimensionalCodeType.Extensions1D, 7);

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
        public override bool HasMoreData => this.Position < (ulong)this.DataLength - 1 || (uint)(this.BitsRead - 1) < (7 - 1);

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
                        if (value == Len1Code1.Code)
                        {
                            this.Code = Len1Code1;
                            return false;
                        }

                        break;

                    case 3:
                        if (value == Len3Code001.Code)
                        {
                            this.Code = Len3Code001;
                            return false;
                        }

                        if (value == Len3Code010.Code)
                        {
                            this.Code = Len3Code010;
                            return false;
                        }

                        if (value == Len3Code011.Code)
                        {
                            this.Code = Len3Code011;
                            return false;
                        }

                        break;

                    case 4:
                        if (value == Len4Code0001.Code)
                        {
                            this.Code = Len4Code0001;
                            return false;
                        }

                        break;

                    case 6:
                        if (value == Len6Code000010.Code)
                        {
                            this.Code = Len6Code000010;
                            return false;
                        }

                        if (value == Len6Code000011.Code)
                        {
                            this.Code = Len6Code000011;
                            return false;
                        }

                        break;

                    case 7:
                        if (value == Len7Code0000000.Code)
                        {
                            this.Code = Len7Code0000000;
                            return false;
                        }

                        if (value == Len7Code0000001.Code)
                        {
                            this.Code = Len7Code0000001;
                            return false;
                        }

                        if (value == Len7Code0000011.Code)
                        {
                            this.Code = Len7Code0000011;
                            return false;
                        }

                        if (value == Len7Code0000010.Code)
                        {
                            this.Code = Len7Code0000010;
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

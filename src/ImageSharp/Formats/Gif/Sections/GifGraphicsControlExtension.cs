// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// The Graphic Control Extension contains parameters used when
    /// processing a graphic rendering block.
    /// </summary>
    internal readonly struct GifGraphicsControlExtension
    {
        public const int Size = 8;

        public GifGraphicsControlExtension(
            DisposalMethod disposalMethod,
            bool transparencyFlag,
            ushort delayTime,
            byte transparencyIndex)
        {
            this.DisposalMethod = disposalMethod;
            this.TransparencyFlag = transparencyFlag;
            this.DelayTime = delayTime;
            this.TransparencyIndex = transparencyIndex;
        }

        /// <summary>
        /// Gets the disposal method which indicates the way in which the
        /// graphic is to be treated after being displayed.
        /// </summary>
        public DisposalMethod DisposalMethod { get; }

        /// <summary>
        /// Gets a value indicating whether transparency flag is to be set.
        /// This indicates whether a transparency index is given in the Transparent Index field.
        /// (This field is the least significant bit of the byte.)
        /// </summary>
        public bool TransparencyFlag { get; }

        /// <summary>
        /// Gets the delay time.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public ushort DelayTime { get; }

        /// <summary>
        /// Gets the transparency index.
        /// The Transparency Index is such that when encountered, the corresponding pixel
        /// of the display device is not modified and processing goes on to the next pixel.
        /// </summary>
        public byte TransparencyIndex { get; }

        public byte PackField()
        {
            PackedField field = default;

            field.SetBits(3, 3, (int)this.DisposalMethod); // 1-3 : Reserved, 4-6 : Disposal

            // TODO: Allow this as an option.
            field.SetBit(6, false); // 7 : User input - 0 = none
            field.SetBit(7, this.TransparencyFlag); // 8: Has transparent.

            return field.Byte;
        }

        public void WriteTo(Span<byte> buffer)
        {
            buffer[0] = GifConstants.ExtensionIntroducer;
            buffer[1] = GifConstants.GraphicControlLabel;
            buffer[2] = 4;                                                                 // Block Size
            buffer[3] = this.PackField();                                                  // Packed Field
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(4, 2), this.DelayTime);  // Delay Time
            buffer[6] = this.TransparencyIndex;
            buffer[7] = GifConstants.Terminator;
        }

        public static GifGraphicsControlExtension Parse(ReadOnlySpan<byte> buffer)
        {
            // We've already read the Extension Introducer introducer & Graphic Control Label
            // Start from the block size (0)
            byte packed = buffer[1];

            return new GifGraphicsControlExtension(
                disposalMethod: (DisposalMethod)((packed & 0x1C) >> 2),
                delayTime: BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2, 2)),
                transparencyIndex: buffer[4],
                transparencyFlag: (packed & 0x01) == 1);
        }
    }
}
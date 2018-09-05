// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// The Graphic Control Extension contains parameters used when
    /// processing a graphic rendering block.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct GifGraphicControlExtension : IGifExtension
    {
        public GifGraphicControlExtension(
            byte packed,
            ushort delayTime,
            byte transparencyIndex)
        {
            this.BlockSize = 4;
            this.Packed = packed;
            this.DelayTime = delayTime;
            this.TransparencyIndex = transparencyIndex;
        }

        /// <summary>
        /// Gets the size of the block.
        /// </summary>
        public byte BlockSize { get; }

        /// <summary>
        /// Gets the packed disposalMethod and transparencyFlag value.
        /// </summary>
        public byte Packed { get; }

        /// <summary>
        /// Gets the delay time in of hundredths (1/100) of a second
        /// to wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public ushort DelayTime { get; }

        /// <summary>
        /// Gets the transparency index.
        /// The Transparency Index is such that when encountered, the corresponding pixel
        /// of the display device is not modified and processing goes on to the next pixel.
        /// </summary>
        public byte TransparencyIndex { get; }

        /// <summary>
        /// Gets the disposal method which indicates the way in which the
        /// graphic is to be treated after being displayed.
        /// </summary>
        public GifDisposalMethod DisposalMethod => (GifDisposalMethod)((this.Packed & 0x1C) >> 2);

        /// <summary>
        /// Gets a value indicating whether transparency flag is to be set.
        /// This indicates whether a transparency index is given in the Transparent Index field.
        /// </summary>
        public bool TransparencyFlag => (this.Packed & 0x01) == 1;

        byte IGifExtension.Label => GifConstants.GraphicControlLabel;

        public int WriteTo(Span<byte> buffer)
        {
            ref GifGraphicControlExtension dest = ref Unsafe.As<byte, GifGraphicControlExtension>(ref MemoryMarshal.GetReference(buffer));

            dest = this;

            return 5;
        }

        public static GifGraphicControlExtension Parse(ReadOnlySpan<byte> buffer)
        {
            return MemoryMarshal.Cast<byte, GifGraphicControlExtension>(buffer)[0];
        }

        public static byte GetPackedValue(GifDisposalMethod disposalMethod, bool userInputFlag = false, bool transparencyFlag = false)
        {
            /*
            Reserved               | 3 Bits
            Disposal Method        | 3 Bits
            User Input Flag        | 1 Bit
            Transparent Color Flag | 1 Bit
            */

            byte value = 0;

            value |= (byte)((int)disposalMethod << 2);

            if (userInputFlag)
            {
                value |= 1 << 1;
            }

            if (transparencyFlag)
            {
                value |= 1;
            }

            return value;
        }
    }
}
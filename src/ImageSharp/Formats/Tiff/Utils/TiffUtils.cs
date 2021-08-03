// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Utils
{
    /// <summary>
    /// Helper methods for TIFF decoding.
    /// </summary>
    internal static class TiffUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ConvertToShortBigEndian(ReadOnlySpan<byte> buffer) =>
            BinaryPrimitives.ReadUInt16BigEndian(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ConvertToShortLittleEndian(ReadOnlySpan<byte> buffer) =>
            BinaryPrimitives.ReadUInt16LittleEndian(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorFromL8<TPixel>(L8 l8, byte intensity, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            l8.PackedValue = intensity;
            color.FromL8(l8);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorFromL16<TPixel>(L16 l16, ushort intensity, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            l16.PackedValue = intensity;
            color.FromL16(l16);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorFromRgba64<TPixel>(Rgba64 rgba, ulong r, ulong g, ulong b, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            rgba.PackedValue = r | (g << 16) | (b << 32) | (0xfffful << 48);
            color.FromRgba64(rgba);
            return color;
        }
    }
}

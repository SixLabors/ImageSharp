// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Utils
{
    /// <summary>
    /// Helper methods for TIFF decoding.
    /// </summary>
    internal static class TiffUtils
    {
        private const float Scale24Bit = 1.0f / 0xFFFFFF;

        private const float Scale32Bit = 1.0f / 0xFFFFFFFF;

        public static Vector4 Vector4Default { get; } = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

        public static Rgba64 Rgba64Default { get; } = new Rgba64(0, 0, 0, 0);

        public static L16 L16Default { get; } = new L16(0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ConvertToUShortBigEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt16BigEndian(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ConvertToUShortLittleEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt16LittleEndian(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ConvertToUIntBigEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt32BigEndian(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ConvertToUIntLittleEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt32LittleEndian(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorFromL8<TPixel>(L8 l8, byte intensity, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            l8.PackedValue = intensity;
            color.FromL8(l8);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo24Bit<TPixel>(ulong r, ulong g, ulong b, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colorVector = new Vector4(r * Scale24Bit, g * Scale24Bit, b * Scale24Bit, 1.0f);
            color.FromVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo32Bit<TPixel>(ulong r, ulong g, ulong b, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colorVector = new Vector4(r * Scale32Bit, g * Scale32Bit, b * Scale32Bit, 1.0f);
            color.FromVector4(colorVector);
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
        public static TPixel ColorScaleTo24Bit<TPixel>(ulong intensity, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colorVector = new Vector4(intensity * Scale24Bit, intensity * Scale24Bit, intensity * Scale24Bit, 1.0f);
            color.FromVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo32Bit<TPixel>(ulong intensity, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colorVector = new Vector4(intensity * Scale32Bit, intensity * Scale32Bit, intensity * Scale32Bit, 1.0f);
            color.FromVector4(colorVector);
            return color;
        }
    }
}

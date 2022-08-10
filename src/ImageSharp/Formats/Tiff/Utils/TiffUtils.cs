// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

        public static Rgba64 Rgba64Default { get; } = new(0, 0, 0, 0);

        public static L16 L16Default { get; } = new(0);

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
        public static TPixel ColorFromRgb64<TPixel>(Rgba64 rgba, ulong r, ulong g, ulong b, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            rgba.PackedValue = r | (g << 16) | (b << 32) | (0xfffful << 48);
            color.FromRgba64(rgba);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorFromRgba64<TPixel>(Rgba64 rgba, ulong r, ulong g, ulong b, ulong a, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            rgba.PackedValue = r | (g << 16) | (b << 32) | (a << 48);
            color.FromRgba64(rgba);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorFromRgba64Premultiplied<TPixel>(Rgba64 rgba, ulong r, ulong g, ulong b, ulong a, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            rgba.PackedValue = r | (g << 16) | (b << 32) | (a << 48);
            var vec = rgba.ToVector4();
            return UnPremultiply(ref vec, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo24Bit<TPixel>(ulong r, ulong g, ulong b, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colorVector = new Vector4(r * Scale24Bit, g * Scale24Bit, b * Scale24Bit, 1.0f);
            color.FromScaledVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo24Bit<TPixel>(ulong r, ulong g, ulong b, ulong a, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 colorVector = new Vector4(r, g, b, a) * Scale24Bit;
            color.FromScaledVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo24BitPremultiplied<TPixel>(ulong r, ulong g, ulong b, ulong a, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 colorVector = new Vector4(r, g, b, a) * Scale24Bit;
            return UnPremultiply(ref colorVector, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo32Bit<TPixel>(ulong r, ulong g, ulong b, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colorVector = new Vector4(r * Scale32Bit, g * Scale32Bit, b * Scale32Bit, 1.0f);
            color.FromScaledVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo32Bit<TPixel>(ulong r, ulong g, ulong b, ulong a, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 colorVector = new Vector4(r, g, b, a) * Scale32Bit;
            color.FromScaledVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo32BitPremultiplied<TPixel>(ulong r, ulong g, ulong b, ulong a, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 colorVector = new Vector4(r, g, b, a) * Scale32Bit;
            return UnPremultiply(ref colorVector, color);
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
            color.FromScaledVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ColorScaleTo32Bit<TPixel>(ulong intensity, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colorVector = new Vector4(intensity * Scale32Bit, intensity * Scale32Bit, intensity * Scale32Bit, 1.0f);
            color.FromScaledVector4(colorVector);
            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel UnPremultiply<TPixel>(ref Vector4 vector, TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Numerics.UnPremultiply(ref vector);
            color.FromScaledVector4(vector);

            return color;
        }

        /// <summary>
        /// Finds the padding needed to round 'valueToRoundUp' to the next integer multiple of subSampling value.
        /// </summary>
        /// <param name="valueToRoundUp">The width or height to round up.</param>
        /// <param name="subSampling">The sub sampling.</param>
        /// <returns>The padding.</returns>
        public static int PaddingToNextInteger(int valueToRoundUp, int subSampling)
        {
            if (valueToRoundUp % subSampling == 0)
            {
                return 0;
            }

            return subSampling - (valueToRoundUp % subSampling);
        }
    }
}

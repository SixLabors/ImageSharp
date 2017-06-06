namespace ImageSharp.PixelFormats
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Extension methods for copying single pixel data into byte Spans.
    /// TODO: This utility class exists for legacy reasons. Need to do a lot of chore work to remove it (mostly in test classes).
    /// </summary>
    internal static class PixelConversionExtensions
    {
        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to X-> Y-> Z order. Equivalent to R-> G-> B in <see cref="Rgb24"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="pixel">The pixel to copy the data from.</param>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToXyzBytes<TPixel>(this TPixel pixel, Span<byte> bytes, int startIndex)
            where TPixel : struct, IPixel<TPixel>
        {
            ref Rgb24 dest = ref bytes.GetRgb24(startIndex);
            pixel.ToRgb24(ref dest);
        }

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to X-> Y-> Z-> W order. Equivalent to R-> G-> B-> A in <see cref="Rgba32"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="pixel">The pixel to copy the data from.</param>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToXyzwBytes<TPixel>(this TPixel pixel, Span<byte> bytes, int startIndex)
            where TPixel : struct, IPixel<TPixel>
        {
            ref Rgba32 dest = ref Unsafe.As<byte, Rgba32>(ref bytes[startIndex]);
            pixel.ToRgba32(ref dest);
        }

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to Z-> Y-> X order. Equivalent to B-> G-> R in <see cref="Bgr24"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="pixel">The pixel to copy the data from.</param>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToZyxBytes<TPixel>(this TPixel pixel, Span<byte> bytes, int startIndex)
            where TPixel : struct, IPixel<TPixel>
        {
            ref Bgr24 dest = ref Unsafe.As<byte, Bgr24>(ref bytes[startIndex]);
            pixel.ToBgr24(ref dest);
        }

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to Z-> Y-> X-> W order. Equivalent to B-> G-> R-> A in <see cref="Bgra32"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="pixel">The pixel to copy the data from.</param>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToZyxwBytes<TPixel>(this TPixel pixel, Span<byte> bytes, int startIndex)
            where TPixel : struct, IPixel<TPixel>
        {
            ref Bgra32 dest = ref Unsafe.As<byte, Bgra32>(ref bytes[startIndex]);
            pixel.ToBgra32(ref dest);
        }
    }
}
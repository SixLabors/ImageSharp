// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides methods to allow the decoding of raw scanlines to image rows of different pixel formats.
    /// </summary>
    internal static class PngScanlineProcessor
    {
        public static void ProcessGrayscaleScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            bool hasTrans,
            ushort luminance16Trans,
            byte luminanceTrans)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
            int scaleFactor = 255 / (ImageMaths.GetColorCountForBitDepth(header.BitDepth) - 1);

            if (!hasTrans)
            {
                if (header.BitDepth == 16)
                {
                    Rgb48 rgb48 = default;
                    for (int x = 0, o = 0; x < header.Width; x++, o += 2)
                    {
                        ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                        rgb48.R = luminance;
                        rgb48.G = luminance;
                        rgb48.B = luminance;

                        pixel.PackFromRgb48(rgb48);
                        Unsafe.Add(ref rowSpanRef, x) = pixel;
                    }
                }
                else
                {
                    // TODO: We should really be using Rgb24 here but IPixel does not have a PackFromRgb24 method.
                    var rgba32 = new Rgba32(0, 0, 0, byte.MaxValue);
                    for (int x = 0; x < header.Width; x++)
                    {
                        byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, x) * scaleFactor);
                        rgba32.R = luminance;
                        rgba32.G = luminance;
                        rgba32.B = luminance;

                        pixel.PackFromRgba32(rgba32);
                        Unsafe.Add(ref rowSpanRef, x) = pixel;
                    }
                }

                return;
            }

            if (header.BitDepth == 16)
            {
                Rgba64 rgba64 = default;
                for (int x = 0, o = 0; x < header.Width; x++, o += 2)
                {
                    ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                    rgba64.R = luminance;
                    rgba64.G = luminance;
                    rgba64.B = luminance;
                    rgba64.A = luminance.Equals(luminance16Trans) ? ushort.MinValue : ushort.MaxValue;

                    pixel.PackFromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                Rgba32 rgba32 = default;
                for (int x = 0; x < header.Width; x++)
                {
                    byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, x) * scaleFactor);
                    rgba32.R = luminance;
                    rgba32.G = luminance;
                    rgba32.B = luminance;
                    rgba32.A = luminance.Equals(luminanceTrans) ? byte.MinValue : byte.MaxValue;

                    pixel.PackFromRgba32(rgba32);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessInterlacedGrayscaleScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int pixelOffset,
            int increment,
            bool hasTrans,
            ushort luminance16Trans,
            byte luminanceTrans)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
            int scaleFactor = 255 / (ImageMaths.GetColorCountForBitDepth(header.BitDepth) - 1);

            if (!hasTrans)
            {
                if (header.BitDepth == 16)
                {
                    Rgb48 rgb48 = default;
                    for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += 2)
                    {
                        ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                        rgb48.R = luminance;
                        rgb48.G = luminance;
                        rgb48.B = luminance;

                        pixel.PackFromRgb48(rgb48);
                        Unsafe.Add(ref rowSpanRef, x) = pixel;
                    }
                }
                else
                {
                    // TODO: We should really be using Rgb24 here but IPixel does not have a PackFromRgb24 method.
                    var rgba32 = new Rgba32(0, 0, 0, byte.MaxValue);
                    for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o++)
                    {
                        byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, o) * scaleFactor);
                        rgba32.R = luminance;
                        rgba32.G = luminance;
                        rgba32.B = luminance;

                        pixel.PackFromRgba32(rgba32);
                        Unsafe.Add(ref rowSpanRef, x) = pixel;
                    }
                }

                return;
            }

            if (header.BitDepth == 16)
            {
                Rgba64 rgba64 = default;
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += 2)
                {
                    ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                    rgba64.R = luminance;
                    rgba64.G = luminance;
                    rgba64.B = luminance;
                    rgba64.A = luminance.Equals(luminance16Trans) ? ushort.MinValue : ushort.MaxValue;

                    pixel.PackFromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                Rgba32 rgba32 = default;
                for (int x = pixelOffset; x < header.Width; x += increment)
                {
                    byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, x) * scaleFactor);
                    rgba32.R = luminance;
                    rgba32.G = luminance;
                    rgba32.B = luminance;
                    rgba32.A = luminance.Equals(luminanceTrans) ? byte.MinValue : byte.MaxValue;

                    pixel.PackFromRgba32(rgba32);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessGrayscaleWithAlphaScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int bytesPerPixel,
            int bytesPerSample)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

            if (header.BitDepth == 16)
            {
                Rgba64 rgba64 = default;
                for (int x = 0, o = 0; x < header.Width; x++, o += 4)
                {
                    ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                    ushort alpha = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                    rgba64.R = luminance;
                    rgba64.G = luminance;
                    rgba64.B = luminance;
                    rgba64.A = alpha;

                    pixel.PackFromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                Rgba32 rgba32 = default;
                int bps = bytesPerSample;
                for (int x = 0; x < header.Width; x++)
                {
                    int offset = x * bytesPerPixel;
                    byte luminance = Unsafe.Add(ref scanlineSpanRef, offset);
                    byte alpha = Unsafe.Add(ref scanlineSpanRef, offset + bps);

                    rgba32.R = luminance;
                    rgba32.G = luminance;
                    rgba32.B = luminance;
                    rgba32.A = alpha;

                    pixel.PackFromRgba32(rgba32);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessInterlacedGrayscaleWithAlphaScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int pixelOffset,
            int increment,
            int bytesPerPixel,
            int bytesPerSample)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

            if (header.BitDepth == 16)
            {
                Rgba64 rgba64 = default;
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += 4)
                {
                    ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                    ushort alpha = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                    rgba64.R = luminance;
                    rgba64.G = luminance;
                    rgba64.B = luminance;
                    rgba64.A = alpha;

                    pixel.PackFromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                Rgba32 rgba32 = default;
                for (int x = pixelOffset; x < header.Width; x += increment)
                {
                    int offset = x * bytesPerPixel;
                    byte luminance = Unsafe.Add(ref scanlineSpanRef, offset);
                    byte alpha = Unsafe.Add(ref scanlineSpanRef, offset + bytesPerSample);
                    rgba32.R = luminance;
                    rgba32.G = luminance;
                    rgba32.B = luminance;
                    rgba32.A = alpha;

                    pixel.PackFromRgba32(rgba32);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessPaletteScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            ReadOnlySpan<byte> palette,
            byte[] paletteAlpha)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
            ReadOnlySpan<Rgb24> palettePixels = MemoryMarshal.Cast<byte, Rgb24>(palette);
            ref Rgb24 palettePixelsRef = ref MemoryMarshal.GetReference(palettePixels);

            if (paletteAlpha?.Length > 0)
            {
                // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                // channel and we should try to read it.
                Rgba32 rgba = default;
                ref byte paletteAlphaRef = ref paletteAlpha[0];

                for (int x = 0; x < header.Width; x++)
                {
                    int index = Unsafe.Add(ref scanlineSpanRef, x);
                    rgba.Rgb = Unsafe.Add(ref palettePixelsRef, index);
                    rgba.A = paletteAlpha.Length > index ? Unsafe.Add(ref paletteAlphaRef, index) : byte.MaxValue;

                    pixel.PackFromRgba32(rgba);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                // TODO: We should have PackFromRgb24.
                var rgba = new Rgba32(0, 0, 0, byte.MaxValue);
                for (int x = 0; x < header.Width; x++)
                {
                    int index = Unsafe.Add(ref scanlineSpanRef, x);
                    rgba.Rgb = Unsafe.Add(ref palettePixelsRef, index);

                    pixel.PackFromRgba32(rgba);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessInterlacedPaletteScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int pixelOffset,
            int increment,
            ReadOnlySpan<byte> palette,
            byte[] paletteAlpha)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
            ReadOnlySpan<Rgb24> palettePixels = MemoryMarshal.Cast<byte, Rgb24>(palette);
            ref Rgb24 palettePixelsRef = ref MemoryMarshal.GetReference(palettePixels);

            if (paletteAlpha?.Length > 0)
            {
                // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                // channel and we should try to read it.
                Rgba32 rgba = default;
                ref byte paletteAlphaRef = ref paletteAlpha[0];
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o++)
                {
                    int index = Unsafe.Add(ref scanlineSpanRef, o);
                    rgba.A = paletteAlpha.Length > index ? Unsafe.Add(ref paletteAlphaRef, index) : byte.MaxValue;
                    rgba.Rgb = Unsafe.Add(ref palettePixelsRef, index);

                    pixel.PackFromRgba32(rgba);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                var rgba = new Rgba32(0, 0, 0, byte.MaxValue);
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o++)
                {
                    int index = Unsafe.Add(ref scanlineSpanRef, o);
                    rgba.Rgb = Unsafe.Add(ref palettePixelsRef, index);

                    pixel.PackFromRgba32(rgba);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessRgbScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int bytesPerPixel,
            int bytesPerSample,
            bool hasTrans,
            Rgb48 rgb48Trans,
            Rgb24 rgb24Trans)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

            if (!hasTrans)
            {
                if (header.BitDepth == 16)
                {
                    Rgb48 rgb48 = default;
                    for (int x = 0, o = 0; x < header.Width; x++, o += bytesPerPixel)
                    {
                        rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                        rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                        rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                        pixel.PackFromRgb48(rgb48);
                        Unsafe.Add(ref rowSpanRef, x) = pixel;
                    }
                }
                else
                {
                    PixelOperations<TPixel>.Instance.PackFromRgb24Bytes(scanlineSpan, rowSpan, header.Width);
                }

                return;
            }

            if (header.BitDepth == 16)
            {
                Rgb48 rgb48 = default;
                Rgba64 rgba64 = default;
                for (int x = 0, o = 0; x < header.Width; x++, o += bytesPerPixel)
                {
                    rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                    rgba64.Rgb = rgb48;
                    rgba64.A = rgb48.Equals(rgb48Trans) ? ushort.MinValue : ushort.MaxValue;

                    pixel.PackFromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                ReadOnlySpan<Rgb24> rgb24Span = MemoryMarshal.Cast<byte, Rgb24>(scanlineSpan);
                ref Rgb24 rgb24SpanRef = ref MemoryMarshal.GetReference(rgb24Span);
                for (int x = 0; x < header.Width; x++)
                {
                    ref readonly Rgb24 rgb24 = ref Unsafe.Add(ref rgb24SpanRef, x);
                    Rgba32 rgba32 = default;
                    rgba32.Rgb = rgb24;
                    rgba32.A = rgb24.Equals(rgb24Trans) ? byte.MinValue : byte.MaxValue;

                    pixel.PackFromRgba32(rgba32);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessInterlacedRgbScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int pixelOffset,
            int increment,
            int bytesPerPixel,
            int bytesPerSample,
            bool hasTrans,
            Rgb48 rgb48Trans,
            Rgb24 rgb24Trans)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

            if (header.BitDepth == 16)
            {
                if (hasTrans)
                {
                    Rgb48 rgb48 = default;
                    Rgba64 rgba64 = default;
                    for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += bytesPerPixel)
                    {
                        rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                        rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                        rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                        rgba64.Rgb = rgb48;
                        rgba64.A = rgb48.Equals(rgb48Trans) ? ushort.MinValue : ushort.MaxValue;

                        pixel.PackFromRgba64(rgba64);
                        Unsafe.Add(ref rowSpanRef, x) = pixel;
                    }
                }
                else
                {
                    Rgb48 rgb48 = default;
                    for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += bytesPerPixel)
                    {
                        rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                        rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                        rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                        pixel.PackFromRgb48(rgb48);
                        Unsafe.Add(ref rowSpanRef, x) = pixel;
                    }
                }

                return;
            }

            if (hasTrans)
            {
                Rgba32 rgba = default;
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += bytesPerPixel)
                {
                    rgba.R = Unsafe.Add(ref scanlineSpanRef, o);
                    rgba.G = Unsafe.Add(ref scanlineSpanRef, o + bytesPerSample);
                    rgba.B = Unsafe.Add(ref scanlineSpanRef, o + (2 * bytesPerSample));
                    rgba.A = rgb24Trans.Equals(rgba.Rgb) ? byte.MinValue : byte.MaxValue;

                    pixel.PackFromRgba32(rgba);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                var rgba = new Rgba32(0, 0, 0, byte.MaxValue);
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += bytesPerPixel)
                {
                    rgba.R = Unsafe.Add(ref scanlineSpanRef, o);
                    rgba.G = Unsafe.Add(ref scanlineSpanRef, o + bytesPerSample);
                    rgba.B = Unsafe.Add(ref scanlineSpanRef, o + (2 * bytesPerSample));

                    pixel.PackFromRgba32(rgba);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }

        public static void ProcessRgbaScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int bytesPerPixel,
            int bytesPerSample)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

            if (header.BitDepth == 16)
            {
                Rgba64 rgba64 = default;
                for (int x = 0, o = 0; x < header.Width; x++, o += bytesPerPixel)
                {
                    rgba64.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    rgba64.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    rgba64.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                    rgba64.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (3 * bytesPerSample), bytesPerSample));

                    pixel.PackFromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(scanlineSpan, rowSpan, header.Width);
            }
        }

        public static void ProcessInterlacedRgbaScanline<TPixel>(
            in PngHeader header,
            ReadOnlySpan<byte> scanlineSpan,
            Span<TPixel> rowSpan,
            int pixelOffset,
            int increment,
            int bytesPerPixel,
            int bytesPerSample)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

            if (header.BitDepth == 16)
            {
                Rgba64 rgba64 = default;
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += bytesPerPixel)
                {
                    rgba64.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    rgba64.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    rgba64.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                    rgba64.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (3 * bytesPerSample), bytesPerSample));

                    pixel.PackFromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                Rgba32 rgba = default;
                for (int x = pixelOffset, o = 0; x < header.Width; x += increment, o += bytesPerPixel)
                {
                    rgba.R = Unsafe.Add(ref scanlineSpanRef, o);
                    rgba.G = Unsafe.Add(ref scanlineSpanRef, o + bytesPerSample);
                    rgba.B = Unsafe.Add(ref scanlineSpanRef, o + (2 * bytesPerSample));
                    rgba.A = Unsafe.Add(ref scanlineSpanRef, o + (3 * bytesPerSample));

                    pixel.PackFromRgba32(rgba);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
        }
    }
}
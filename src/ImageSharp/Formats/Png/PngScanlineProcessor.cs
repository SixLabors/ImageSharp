// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Provides methods to allow the decoding of raw scanlines to image rows of different pixel formats.
/// TODO: We should make this a stateful class or struct to reduce the number of arguments on methods (most are invariant).
/// </summary>
internal static class PngScanlineProcessor
{
    public static void ProcessGrayscaleScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        bool hasTrans,
        L16 luminance16Trans,
        L8 luminanceTrans)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
        int scaleFactor = 255 / (ColorNumerics.GetColorCountForBitDepth(header.BitDepth) - 1);

        if (!hasTrans)
        {
            if (header.BitDepth == 16)
            {
                int o = 0;
                for (nuint x = 0; x < (uint)header.Width; x++, o += 2)
                {
                    ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                    pixel.FromL16(Unsafe.As<ushort, L16>(ref luminance));
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                for (nuint x = 0; x < (uint)header.Width; x++)
                {
                    byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, x) * scaleFactor);
                    pixel.FromL8(Unsafe.As<byte, L8>(ref luminance));
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }

            return;
        }

        if (header.BitDepth == 16)
        {
            La32 source = default;
            int o = 0;
            for (nuint x = 0; x < (uint)header.Width; x++, o += 2)
            {
                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                source.L = luminance;
                source.A = luminance.Equals(luminance16Trans.PackedValue) ? ushort.MinValue : ushort.MaxValue;

                pixel.FromLa32(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            La16 source = default;
            byte scaledLuminanceTrans = (byte)(luminanceTrans.PackedValue * scaleFactor);
            for (nuint x = 0; x < (uint)header.Width; x++)
            {
                byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, x) * scaleFactor);
                source.L = luminance;
                source.A = luminance.Equals(scaledLuminanceTrans) ? byte.MinValue : byte.MaxValue;

                pixel.FromLa16(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessInterlacedGrayscaleScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        bool hasTrans,
        L16 luminance16Trans,
        L8 luminanceTrans)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
        int scaleFactor = 255 / (ColorNumerics.GetColorCountForBitDepth(header.BitDepth) - 1);

        if (!hasTrans)
        {
            if (header.BitDepth == 16)
            {
                int o = 0;
                for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += 2)
                {
                    ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                    pixel.FromL16(Unsafe.As<ushort, L16>(ref luminance));
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                for (nuint x = pixelOffset, o = 0; x < (uint)header.Width; x += increment, o++)
                {
                    byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, o) * scaleFactor);
                    pixel.FromL8(Unsafe.As<byte, L8>(ref luminance));
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }

            return;
        }

        if (header.BitDepth == 16)
        {
            La32 source = default;
            int o = 0;
            for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += 2)
            {
                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                source.L = luminance;
                source.A = luminance.Equals(luminance16Trans.PackedValue) ? ushort.MinValue : ushort.MaxValue;

                pixel.FromLa32(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            La16 source = default;
            byte scaledLuminanceTrans = (byte)(luminanceTrans.PackedValue * scaleFactor);
            for (nuint x = pixelOffset, o = 0; x < (uint)header.Width; x += increment, o++)
            {
                byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, o) * scaleFactor);
                source.L = luminance;
                source.A = luminance.Equals(scaledLuminanceTrans) ? byte.MinValue : byte.MaxValue;

                pixel.FromLa16(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessGrayscaleWithAlphaScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint bytesPerPixel,
        uint bytesPerSample)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (header.BitDepth == 16)
        {
            La32 source = default;
            int o = 0;
            for (nuint x = 0; x < (uint)header.Width; x++, o += 4)
            {
                source.L = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                source.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));

                pixel.FromLa32(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            La16 source = default;
            for (nuint x = 0; x < (uint)header.Width; x++)
            {
                nuint offset = x * bytesPerPixel;
                source.L = Unsafe.Add(ref scanlineSpanRef, offset);
                source.A = Unsafe.Add(ref scanlineSpanRef, offset + bytesPerSample);

                pixel.FromLa16(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessInterlacedGrayscaleWithAlphaScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        uint bytesPerPixel,
        uint bytesPerSample)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (header.BitDepth == 16)
        {
            La32 source = default;
            int o = 0;
            for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += 4)
            {
                source.L = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                source.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));

                pixel.FromLa32(source);
                Unsafe.Add(ref rowSpanRef, (uint)x) = pixel;
            }
        }
        else
        {
            La16 source = default;
            nuint offset = 0;
            for (nuint x = pixelOffset; x < (uint)header.Width; x += increment)
            {
                source.L = Unsafe.Add(ref scanlineSpanRef, offset);
                source.A = Unsafe.Add(ref scanlineSpanRef, offset + bytesPerSample);

                pixel.FromLa16(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
                offset += bytesPerPixel;
            }
        }
    }

    public static void ProcessPaletteScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        ReadOnlySpan<byte> palette,
        byte[] paletteAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (palette.IsEmpty)
        {
            PngThrowHelper.ThrowMissingPalette();
        }

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
            ref byte paletteAlphaRef = ref MemoryMarshal.GetArrayDataReference(paletteAlpha);

            for (nuint x = 0; x < (uint)header.Width; x++)
            {
                uint index = Unsafe.Add(ref scanlineSpanRef, x);
                rgba.Rgb = Unsafe.Add(ref palettePixelsRef, index);
                rgba.A = paletteAlpha.Length > index ? Unsafe.Add(ref paletteAlphaRef, index) : byte.MaxValue;

                pixel.FromRgba32(rgba);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            for (nuint x = 0; x < (uint)header.Width; x++)
            {
                int index = Unsafe.Add(ref scanlineSpanRef, x);
                Rgb24 rgb = Unsafe.Add(ref palettePixelsRef, index);

                pixel.FromRgb24(rgb);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessInterlacedPaletteScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        ReadOnlySpan<byte> palette,
        byte[] paletteAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
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
            ref byte paletteAlphaRef = ref MemoryMarshal.GetArrayDataReference(paletteAlpha);
            for (nuint x = pixelOffset, o = 0; x < (uint)header.Width; x += increment, o++)
            {
                uint index = Unsafe.Add(ref scanlineSpanRef, o);
                rgba.A = paletteAlpha.Length > index ? Unsafe.Add(ref paletteAlphaRef, index) : byte.MaxValue;
                rgba.Rgb = Unsafe.Add(ref palettePixelsRef, index);

                pixel.FromRgba32(rgba);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            for (nuint x = pixelOffset, o = 0; x < (uint)header.Width; x += increment, o++)
            {
                int index = Unsafe.Add(ref scanlineSpanRef, o);
                Rgb24 rgb = Unsafe.Add(ref palettePixelsRef, index);

                pixel.FromRgb24(rgb);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessRgbScanline<TPixel>(
        Configuration configuration,
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        int bytesPerPixel,
        int bytesPerSample,
        bool hasTrans,
        Rgb48 rgb48Trans,
        Rgb24 rgb24Trans)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = default;
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (!hasTrans)
        {
            if (header.BitDepth == 16)
            {
                Rgb48 rgb48 = default;
                int o = 0;
                for (nuint x = 0; x < (uint)header.Width; x++, o += bytesPerPixel)
                {
                    rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                    pixel.FromRgb48(rgb48);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                PixelOperations<TPixel>.Instance.FromRgb24Bytes(configuration, scanlineSpan, rowSpan, header.Width);
            }

            return;
        }

        if (header.BitDepth == 16)
        {
            Rgb48 rgb48 = default;
            Rgba64 rgba64 = default;
            int o = 0;
            for (nuint x = 0; x < (uint)header.Width; x++, o += bytesPerPixel)
            {
                rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                rgba64.Rgb = rgb48;
                rgba64.A = rgb48.Equals(rgb48Trans) ? ushort.MinValue : ushort.MaxValue;

                pixel.FromRgba64(rgba64);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            Rgba32 rgba32 = default;
            ReadOnlySpan<Rgb24> rgb24Span = MemoryMarshal.Cast<byte, Rgb24>(scanlineSpan);
            ref Rgb24 rgb24SpanRef = ref MemoryMarshal.GetReference(rgb24Span);
            for (nuint x = 0; x < (uint)header.Width; x++)
            {
                ref readonly Rgb24 rgb24 = ref Unsafe.Add(ref rgb24SpanRef, x);
                rgba32.Rgb = rgb24;
                rgba32.A = rgb24.Equals(rgb24Trans) ? byte.MinValue : byte.MaxValue;

                pixel.FromRgba32(rgba32);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessInterlacedRgbScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        int bytesPerPixel,
        int bytesPerSample,
        bool hasTrans,
        Rgb48 rgb48Trans,
        Rgb24 rgb24Trans)
        where TPixel : unmanaged, IPixel<TPixel>
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
                int o = 0;
                for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += bytesPerPixel)
                {
                    rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                    rgba64.Rgb = rgb48;
                    rgba64.A = rgb48.Equals(rgb48Trans) ? ushort.MinValue : ushort.MaxValue;

                    pixel.FromRgba64(rgba64);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                Rgb48 rgb48 = default;
                int o = 0;
                for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += bytesPerPixel)
                {
                    rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                    pixel.FromRgb48(rgb48);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }

            return;
        }

        if (hasTrans)
        {
            Rgba32 rgba = default;
            int o = 0;
            for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += bytesPerPixel)
            {
                rgba.R = Unsafe.Add(ref scanlineSpanRef, (uint)o);
                rgba.G = Unsafe.Add(ref scanlineSpanRef, (uint)(o + bytesPerSample));
                rgba.B = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (2 * bytesPerSample)));
                rgba.A = rgb24Trans.Equals(rgba.Rgb) ? byte.MinValue : byte.MaxValue;

                pixel.FromRgba32(rgba);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            Rgb24 rgb = default;
            int o = 0;
            for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += bytesPerPixel)
            {
                rgb.R = Unsafe.Add(ref scanlineSpanRef, (uint)o);
                rgb.G = Unsafe.Add(ref scanlineSpanRef, (uint)(o + bytesPerSample));
                rgb.B = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (2 * bytesPerSample)));

                pixel.FromRgb24(rgb);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessRgbaScanline<TPixel>(
        Configuration configuration,
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        int bytesPerPixel,
        int bytesPerSample)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = default;
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (header.BitDepth == 16)
        {
            Rgba64 rgba64 = default;
            int o = 0;
            for (nuint x = 0; x < (uint)header.Width; x++, o += bytesPerPixel)
            {
                rgba64.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                rgba64.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                rgba64.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                rgba64.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (3 * bytesPerSample), bytesPerSample));

                pixel.FromRgba64(rgba64);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            PixelOperations<TPixel>.Instance.FromRgba32Bytes(configuration, scanlineSpan, rowSpan, header.Width);
        }
    }

    public static void ProcessInterlacedRgbaScanline<TPixel>(
        in PngHeader header,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        int bytesPerPixel,
        int bytesPerSample)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (header.BitDepth == 16)
        {
            Rgba64 rgba64 = default;
            int o = 0;
            for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += bytesPerPixel)
            {
                rgba64.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                rgba64.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                rgba64.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                rgba64.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (3 * bytesPerSample), bytesPerSample));

                pixel.FromRgba64(rgba64);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            Rgba32 rgba = default;
            int o = 0;
            for (nuint x = pixelOffset; x < (uint)header.Width; x += increment, o += bytesPerPixel)
            {
                rgba.R = Unsafe.Add(ref scanlineSpanRef, (uint)o);
                rgba.G = Unsafe.Add(ref scanlineSpanRef, (uint)(o + bytesPerSample));
                rgba.B = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (2 * bytesPerSample)));
                rgba.A = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (3 * bytesPerSample)));

                pixel.FromRgba32(rgba);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }
}

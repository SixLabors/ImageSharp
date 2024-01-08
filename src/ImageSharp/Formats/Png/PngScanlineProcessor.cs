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
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        Color? transparentColor)
        where TPixel : unmanaged, IPixel<TPixel> =>
        ProcessInterlacedGrayscaleScanline(
            bitDepth,
            frameControl,
            scanlineSpan,
            rowSpan,
            0,
            1,
            transparentColor);

    public static void ProcessInterlacedGrayscaleScanline<TPixel>(
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        Color? transparentColor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint offset = pixelOffset + frameControl.XOffset;
        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
        int scaleFactor = 255 / (ColorNumerics.GetColorCountForBitDepth(bitDepth) - 1);

        if (transparentColor is null)
        {
            if (bitDepth == 16)
            {
                int o = 0;
                for (nuint x = offset; x < frameControl.XMax; x += increment, o += 2)
                {
                    ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                    pixel.FromL16(Unsafe.As<ushort, L16>(ref luminance));
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else
            {
                for (nuint x = offset, o = 0; x < frameControl.XMax; x += increment, o++)
                {
                    byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, o) * scaleFactor);
                    pixel.FromL8(Unsafe.As<byte, L8>(ref luminance));
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }

            return;
        }

        if (bitDepth == 16)
        {
            L16 transparent = transparentColor.Value.ToPixel<L16>();
            La32 source = default;
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += 2)
            {
                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                source.L = luminance;
                source.A = luminance.Equals(transparent.PackedValue) ? ushort.MinValue : ushort.MaxValue;

                pixel.FromLa32(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            byte transparent = (byte)(transparentColor.Value.ToPixel<L8>().PackedValue * scaleFactor);
            La16 source = default;
            for (nuint x = offset, o = 0; x < frameControl.XMax; x += increment, o++)
            {
                byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, o) * scaleFactor);
                source.L = luminance;
                source.A = luminance.Equals(transparent) ? byte.MinValue : byte.MaxValue;

                pixel.FromLa16(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessGrayscaleWithAlphaScanline<TPixel>(
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint bytesPerPixel,
        uint bytesPerSample)
        where TPixel : unmanaged, IPixel<TPixel> =>
        ProcessInterlacedGrayscaleWithAlphaScanline(
            bitDepth,
            frameControl,
            scanlineSpan,
            rowSpan,
            0,
            1,
            bytesPerPixel,
            bytesPerSample);

    public static void ProcessInterlacedGrayscaleWithAlphaScanline<TPixel>(
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        uint bytesPerPixel,
        uint bytesPerSample)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint offset = pixelOffset + frameControl.XOffset;
        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (bitDepth == 16)
        {
            La32 source = default;
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += 4)
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
            nuint offset2 = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment)
            {
                source.L = Unsafe.Add(ref scanlineSpanRef, offset2);
                source.A = Unsafe.Add(ref scanlineSpanRef, offset2 + bytesPerSample);

                pixel.FromLa16(source);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
                offset2 += bytesPerPixel;
            }
        }
    }

    public static void ProcessPaletteScanline<TPixel>(
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        ReadOnlyMemory<Color>? palette)
        where TPixel : unmanaged, IPixel<TPixel> =>
        ProcessInterlacedPaletteScanline(
            frameControl,
            scanlineSpan,
            rowSpan,
            0,
            1,
            palette);

    public static void ProcessInterlacedPaletteScanline<TPixel>(
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        ReadOnlyMemory<Color>? palette)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (palette is null)
        {
            PngThrowHelper.ThrowMissingPalette();
        }

        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
        ref Color paletteBase = ref MemoryMarshal.GetReference(palette.Value.Span);

        for (nuint x = pixelOffset, o = 0; x < frameControl.XMax; x += increment, o++)
        {
            uint index = Unsafe.Add(ref scanlineSpanRef, o);
            pixel.FromRgba32(Unsafe.Add(ref paletteBase, index).ToPixel<Rgba32>());
            Unsafe.Add(ref rowSpanRef, x) = pixel;
        }
    }

    public static void ProcessRgbScanline<TPixel>(
        Configuration configuration,
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        int bytesPerPixel,
        int bytesPerSample,
        Color? transparentColor)
       where TPixel : unmanaged, IPixel<TPixel> =>
       ProcessInterlacedRgbScanline(
           configuration,
           bitDepth,
           frameControl,
           scanlineSpan,
           rowSpan,
           0,
           1,
           bytesPerPixel,
           bytesPerSample,
           transparentColor);

    public static void ProcessInterlacedRgbScanline<TPixel>(
        Configuration configuration,
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        int bytesPerPixel,
        int bytesPerSample,
        Color? transparentColor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint offset = pixelOffset + frameControl.XOffset;

        TPixel pixel = default;
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (transparentColor is null)
        {
            if (bitDepth == 16)
            {
                Rgb48 rgb48 = default;
                int o = 0;
                for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
                {
                    rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                    pixel.FromRgb48(rgb48);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }
            else if (pixelOffset == 0 && increment == 1)
            {
                PixelOperations<TPixel>.Instance.FromRgb24Bytes(
                    configuration,
                    scanlineSpan[..(int)(frameControl.Width * bytesPerPixel)],
                    rowSpan.Slice((int)frameControl.XOffset, (int)frameControl.Width),
                    (int)frameControl.Width);
            }
            else
            {
                Rgb24 rgb = default;
                int o = 0;
                for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
                {
                    rgb.R = Unsafe.Add(ref scanlineSpanRef, (uint)o);
                    rgb.G = Unsafe.Add(ref scanlineSpanRef, (uint)(o + bytesPerSample));
                    rgb.B = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (2 * bytesPerSample)));

                    pixel.FromRgb24(rgb);
                    Unsafe.Add(ref rowSpanRef, x) = pixel;
                }
            }

            return;
        }

        if (bitDepth == 16)
        {
            Rgb48 transparent = transparentColor.Value.ToPixel<Rgb48>();

            Rgb48 rgb48 = default;
            Rgba64 rgba64 = default;
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
            {
                rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));

                rgba64.Rgb = rgb48;
                rgba64.A = rgb48.Equals(transparent) ? ushort.MinValue : ushort.MaxValue;

                pixel.FromRgba64(rgba64);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else
        {
            Rgb24 transparent = transparentColor.Value.ToPixel<Rgb24>();

            Rgba32 rgba = default;
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
            {
                rgba.R = Unsafe.Add(ref scanlineSpanRef, (uint)o);
                rgba.G = Unsafe.Add(ref scanlineSpanRef, (uint)(o + bytesPerSample));
                rgba.B = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (2 * bytesPerSample)));
                rgba.A = transparent.Equals(rgba.Rgb) ? byte.MinValue : byte.MaxValue;

                pixel.FromRgba32(rgba);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
    }

    public static void ProcessRgbaScanline<TPixel>(
        Configuration configuration,
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        int bytesPerPixel,
        int bytesPerSample)
       where TPixel : unmanaged, IPixel<TPixel> =>
       ProcessInterlacedRgbaScanline(
           configuration,
           bitDepth,
           frameControl,
           scanlineSpan,
           rowSpan,
           0,
           1,
           bytesPerPixel,
           bytesPerSample);

    public static void ProcessInterlacedRgbaScanline<TPixel>(
        Configuration configuration,
        int bitDepth,
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanlineSpan,
        Span<TPixel> rowSpan,
        uint pixelOffset,
        uint increment,
        int bytesPerPixel,
        int bytesPerSample)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint offset = pixelOffset + frameControl.XOffset;
        TPixel pixel = default;
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (bitDepth == 16)
        {
            Rgba64 rgba64 = default;
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
            {
                rgba64.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                rgba64.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                rgba64.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                rgba64.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (3 * bytesPerSample), bytesPerSample));

                pixel.FromRgba64(rgba64);
                Unsafe.Add(ref rowSpanRef, x) = pixel;
            }
        }
        else if (pixelOffset == 0 && increment == 1)
        {
            PixelOperations<TPixel>.Instance.FromRgba32Bytes(
                configuration,
                scanlineSpan[..(int)(frameControl.Width * bytesPerPixel)],
                rowSpan.Slice((int)frameControl.XOffset, (int)frameControl.Width),
                (int)frameControl.Width);
        }
        else
        {
            ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
            Rgba32 rgba = default;
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
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

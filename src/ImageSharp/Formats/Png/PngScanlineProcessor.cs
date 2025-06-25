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
                    Unsafe.Add(ref rowSpanRef, x) = TPixel.FromL16(Unsafe.As<ushort, L16>(ref luminance));
                }
            }
            else
            {
                for (nuint x = offset, o = 0; x < frameControl.XMax; x += increment, o++)
                {
                    byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, o) * scaleFactor);
                    Unsafe.Add(ref rowSpanRef, x) = TPixel.FromL8(Unsafe.As<byte, L8>(ref luminance));
                }
            }

            return;
        }

        if (bitDepth == 16)
        {
            L16 transparent = transparentColor.Value.ToPixel<L16>();
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += 2)
            {
                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                La32 source = new(luminance, luminance.Equals(transparent.PackedValue) ? ushort.MinValue : ushort.MaxValue);
                Unsafe.Add(ref rowSpanRef, x) = TPixel.FromLa32(source);
            }
        }
        else
        {
            byte transparent = (byte)(transparentColor.Value.ToPixel<L8>().PackedValue * scaleFactor);
            for (nuint x = offset, o = 0; x < frameControl.XMax; x += increment, o++)
            {
                byte luminance = (byte)(Unsafe.Add(ref scanlineSpanRef, o) * scaleFactor);
                La16 source = new(luminance, luminance.Equals(transparent) ? byte.MinValue : byte.MaxValue);
                Unsafe.Add(ref rowSpanRef, x) = TPixel.FromLa16(source);
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
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (bitDepth == 16)
        {
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += 4)
            {
                ushort l = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                ushort a = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));

                Unsafe.Add(ref rowSpanRef, (uint)x) = TPixel.FromLa32(new La32(l, a));
            }
        }
        else
        {
            nuint offset2 = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment)
            {
                byte l = Unsafe.Add(ref scanlineSpanRef, offset2);
                byte a = Unsafe.Add(ref scanlineSpanRef, offset2 + bytesPerSample);
                Unsafe.Add(ref rowSpanRef, x) = TPixel.FromLa16(new La16(l, a));
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

        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
        ref Color paletteBase = ref MemoryMarshal.GetReference(palette.Value.Span);
        uint offset = pixelOffset + frameControl.XOffset;
        int maxIndex = palette.Value.Length - 1;

        for (nuint x = offset, o = 0; x < frameControl.XMax; x += increment, o++)
        {
            uint index = Unsafe.Add(ref scanlineSpanRef, o);
            Unsafe.Add(ref rowSpanRef, x) = TPixel.FromRgba32(Unsafe.Add(ref paletteBase, (int)Math.Min(index, maxIndex)).ToPixel<Rgba32>());
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
        ref byte scanlineSpanRef = ref MemoryMarshal.GetReference(scanlineSpan);
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (transparentColor is null)
        {
            if (bitDepth == 16)
            {
                int o = 0;
                for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
                {
                    ushort r = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                    ushort g = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                    ushort b = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                    Unsafe.Add(ref rowSpanRef, x) = TPixel.FromRgb48(new Rgb48(r, g, b));
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
                int o = 0;
                for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
                {
                    byte r = Unsafe.Add(ref scanlineSpanRef, (uint)o);
                    byte g = Unsafe.Add(ref scanlineSpanRef, (uint)(o + bytesPerSample));
                    byte b = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (2 * bytesPerSample)));
                    Unsafe.Add(ref rowSpanRef, x) = TPixel.FromRgb24(new Rgb24(r, g, b));
                }
            }

            return;
        }

        if (bitDepth == 16)
        {
            Rgb48 transparent = transparentColor.Value.ToPixel<Rgb48>();
            Rgba64 rgba = default;
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
            {
                rgba.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                rgba.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                rgba.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                rgba.A = rgba.Rgb.Equals(transparent) ? ushort.MinValue : ushort.MaxValue;
                Unsafe.Add(ref rowSpanRef, x) = TPixel.FromRgba64(rgba);
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
                Unsafe.Add(ref rowSpanRef, x) = TPixel.FromRgba32(rgba);
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
        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);

        if (bitDepth == 16)
        {
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
            {
                ushort r = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, bytesPerSample));
                ushort g = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + bytesPerSample, bytesPerSample));
                ushort b = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (2 * bytesPerSample), bytesPerSample));
                ushort a = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + (3 * bytesPerSample), bytesPerSample));
                Unsafe.Add(ref rowSpanRef, x) = TPixel.FromRgba64(new Rgba64(r, g, b, a));
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
            int o = 0;
            for (nuint x = offset; x < frameControl.XMax; x += increment, o += bytesPerPixel)
            {
                byte r = Unsafe.Add(ref scanlineSpanRef, (uint)o);
                byte g = Unsafe.Add(ref scanlineSpanRef, (uint)(o + bytesPerSample));
                byte b = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (2 * bytesPerSample)));
                byte a = Unsafe.Add(ref scanlineSpanRef, (uint)(o + (3 * bytesPerSample)));
                Unsafe.Add(ref rowSpanRef, x) = TPixel.FromRgba32(new Rgba32(r, g, b, a));
            }
        }
    }
}

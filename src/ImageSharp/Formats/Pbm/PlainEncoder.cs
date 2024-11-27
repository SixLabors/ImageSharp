// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Text;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Pixel encoding methods for the PBM plain encoding.
/// </summary>
internal static class PlainEncoder
{
    private const byte NewLine = 0x0a;
    private const byte Space = 0x20;
    private const byte Zero = 0x30;
    private const byte One = 0x31;

    private const int MaxCharsPerPixelBlackAndWhite = 2;
    private const int MaxCharsPerPixelGrayscale = 4;
    private const int MaxCharsPerPixelGrayscaleWide = 6;
    private const int MaxCharsPerPixelRgb = 4 * 3;
    private const int MaxCharsPerPixelRgbWide = 6 * 3;

    private static readonly StandardFormat DecimalFormat = StandardFormat.Parse("D");

    /// <summary>
    /// Decode pixels into the PBM plain encoding.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="colorType">The ColorType to use.</param>
    /// <param name="componentType">Data type of the pixels components.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static void WritePixels<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        PbmColorType colorType,
        PbmComponentType componentType,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (colorType == PbmColorType.Grayscale)
        {
            if (componentType == PbmComponentType.Byte)
            {
                WriteGrayscale(configuration, stream, image, cancellationToken);
            }
            else
            {
                WriteWideGrayscale(configuration, stream, image, cancellationToken);
            }
        }
        else if (colorType == PbmColorType.Rgb)
        {
            if (componentType == PbmComponentType.Byte)
            {
                WriteRgb(configuration, stream, image, cancellationToken);
            }
            else
            {
                WriteWideRgb(configuration, stream, image, cancellationToken);
            }
        }
        else
        {
            WriteBlackAndWhite(configuration, stream, image, cancellationToken);
        }

        // Write EOF indicator, as some encoders expect it.
        stream.WriteByte(Space);
    }

    private static void WriteGrayscale<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();
        using IMemoryOwner<byte> plainMemory = allocator.Allocate<byte>(width * MaxCharsPerPixelGrayscale);
        Span<byte> plainSpan = plainMemory.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToL8(
                configuration,
                pixelSpan,
                rowSpan);

            int written = 0;
            for (int x = 0; x < width; x++)
            {
                Utf8Formatter.TryFormat(rowSpan[x].PackedValue, plainSpan[written..], out int bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
            }

            plainSpan[written - 1] = NewLine;
            stream.Write(plainSpan, 0, written);
        }
    }

    private static void WriteWideGrayscale<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L16> row = allocator.Allocate<L16>(width);
        Span<L16> rowSpan = row.GetSpan();
        using IMemoryOwner<byte> plainMemory = allocator.Allocate<byte>(width * MaxCharsPerPixelGrayscaleWide);
        Span<byte> plainSpan = plainMemory.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToL16(
                configuration,
                pixelSpan,
                rowSpan);

            int written = 0;
            for (int x = 0; x < width; x++)
            {
                Utf8Formatter.TryFormat(rowSpan[x].PackedValue, plainSpan[written..], out int bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
            }

            plainSpan[written - 1] = NewLine;
            stream.Write(plainSpan, 0, written);
        }
    }

    private static void WriteRgb<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<Rgb24> row = allocator.Allocate<Rgb24>(width);
        Span<Rgb24> rowSpan = row.GetSpan();
        using IMemoryOwner<byte> plainMemory = allocator.Allocate<byte>(width * MaxCharsPerPixelRgb);
        Span<byte> plainSpan = plainMemory.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToRgb24(
                configuration,
                pixelSpan,
                rowSpan);

            int written = 0;
            for (int x = 0; x < width; x++)
            {
                Utf8Formatter.TryFormat(rowSpan[x].R, plainSpan[written..], out int bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
                Utf8Formatter.TryFormat(rowSpan[x].G, plainSpan[written..], out bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
                Utf8Formatter.TryFormat(rowSpan[x].B, plainSpan[written..], out bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
            }

            plainSpan[written - 1] = NewLine;
            stream.Write(plainSpan, 0, written);
        }
    }

    private static void WriteWideRgb<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<Rgb48> row = allocator.Allocate<Rgb48>(width);
        Span<Rgb48> rowSpan = row.GetSpan();
        using IMemoryOwner<byte> plainMemory = allocator.Allocate<byte>(width * MaxCharsPerPixelRgbWide);
        Span<byte> plainSpan = plainMemory.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToRgb48(
                configuration,
                pixelSpan,
                rowSpan);

            int written = 0;
            for (int x = 0; x < width; x++)
            {
                Utf8Formatter.TryFormat(rowSpan[x].R, plainSpan[written..], out int bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
                Utf8Formatter.TryFormat(rowSpan[x].G, plainSpan[written..], out bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
                Utf8Formatter.TryFormat(rowSpan[x].B, plainSpan[written..], out bytesWritten, DecimalFormat);
                written += bytesWritten;
                plainSpan[written++] = Space;
            }

            plainSpan[written - 1] = NewLine;
            stream.Write(plainSpan, 0, written);
        }
    }

    private static void WriteBlackAndWhite<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();
        using IMemoryOwner<byte> plainMemory = allocator.Allocate<byte>(width * MaxCharsPerPixelBlackAndWhite);
        Span<byte> plainSpan = plainMemory.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToL8(
                configuration,
                pixelSpan,
                rowSpan);

            int written = 0;
            for (int x = 0; x < width; x++)
            {
                plainSpan[written++] = (rowSpan[x].PackedValue < 128) ? One : Zero;
                plainSpan[written++] = Space;
            }

            plainSpan[written - 1] = NewLine;
            stream.Write(plainSpan, 0, written);
        }
    }
}

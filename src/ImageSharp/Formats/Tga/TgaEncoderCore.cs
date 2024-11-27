// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga;

/// <summary>
/// Image encoder for writing an image to a stream as a truevision targa image.
/// </summary>
internal sealed class TgaEncoderCore
{
    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The color depth, in number of bits per pixel.
    /// </summary>
    private TgaBitsPerPixel? bitsPerPixel;

    /// <summary>
    /// Indicates if run length compression should be used.
    /// </summary>
    private readonly TgaCompression compression;

    private readonly TransparentColorMode transparentColorMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="TgaEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder with options.</param>
    /// <param name="memoryAllocator">The memory manager.</param>
    public TgaEncoderCore(TgaEncoder encoder, MemoryAllocator memoryAllocator)
    {
        this.memoryAllocator = memoryAllocator;
        this.bitsPerPixel = encoder.BitsPerPixel;
        this.compression = encoder.Compression;
        this.transparentColorMode = encoder.TransparentColorMode;
    }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        ImageMetadata metadata = image.Metadata;
        TgaMetadata tgaMetadata = metadata.GetTgaMetadata();
        this.bitsPerPixel ??= tgaMetadata.BitsPerPixel;

        TgaImageType imageType = this.compression is TgaCompression.RunLength ? TgaImageType.RleTrueColor : TgaImageType.TrueColor;
        if (this.bitsPerPixel == TgaBitsPerPixel.Bit8)
        {
            imageType = this.compression is TgaCompression.RunLength ? TgaImageType.RleBlackAndWhite : TgaImageType.BlackAndWhite;
        }

        byte imageDescriptor = 0;
        if (this.compression is TgaCompression.RunLength)
        {
            // If compression is used, set bit 5 of the image descriptor to indicate a left top origin.
            imageDescriptor |= 0x20;
        }

        if (this.bitsPerPixel is TgaBitsPerPixel.Bit32)
        {
            // Indicate, that 8 bit are used for the alpha channel.
            imageDescriptor |= 0x8;
        }

        if (this.bitsPerPixel is TgaBitsPerPixel.Bit16)
        {
            // Indicate, that 1 bit is used for the alpha channel.
            imageDescriptor |= 0x1;
        }

        TgaFileHeader fileHeader = new(
            idLength: 0,
            colorMapType: 0,
            imageType: imageType,
            cMapStart: 0,
            cMapLength: 0,
            cMapDepth: 0,
            xOffset: 0,

            // When run length encoding is used, the origin should be top left instead of the default bottom left.
            yOffset: this.compression is TgaCompression.RunLength ? (short)image.Height : (short)0,
            width: (short)image.Width,
            height: (short)image.Height,
            pixelDepth: (byte)this.bitsPerPixel.Value,
            imageDescriptor: imageDescriptor);

        Span<byte> buffer = stackalloc byte[TgaFileHeader.Size];
        fileHeader.WriteTo(buffer);

        stream.Write(buffer, 0, TgaFileHeader.Size);

        ImageFrame<TPixel>? clonedFrame = null;
        try
        {
            if (EncodingUtilities.ShouldClearTransparentPixels<TPixel>(this.transparentColorMode))
            {
                clonedFrame = image.Frames.RootFrame.Clone();
                EncodingUtilities.ClearTransparentPixels(clonedFrame, Color.Transparent);
            }

            ImageFrame<TPixel> encodingFrame = clonedFrame ?? image.Frames.RootFrame;

            if (this.compression is TgaCompression.RunLength)
            {
                this.WriteRunLengthEncodedImage(stream, encodingFrame, cancellationToken);
            }
            else
            {
                this.WriteImage(image.Configuration, stream, encodingFrame, cancellationToken);
            }

            stream.Flush();
        }
        finally
        {
            clonedFrame?.Dispose();
        }
    }

    /// <summary>
    /// Writes the pixel data to the binary stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="image">    /// The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void WriteImage<TPixel>(Configuration configuration, Stream stream, ImageFrame<TPixel> image, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2D<TPixel> pixels = image.PixelBuffer;
        switch (this.bitsPerPixel)
        {
            case TgaBitsPerPixel.Bit8:
                this.Write8Bit(configuration, stream, pixels, cancellationToken);
                break;

            case TgaBitsPerPixel.Bit16:
                this.Write16Bit(configuration, stream, pixels, cancellationToken);
                break;

            case TgaBitsPerPixel.Bit24:
                this.Write24Bit(configuration, stream, pixels, cancellationToken);
                break;

            case TgaBitsPerPixel.Bit32:
                this.Write32Bit(configuration, stream, pixels, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Writes a run length encoded tga image to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The stream to write the image to.</param>
    /// <param name="image">The image to encode.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void WriteRunLengthEncodedImage<TPixel>(Stream stream, ImageFrame<TPixel> image, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2D<TPixel> pixels = image.PixelBuffer;

        using IMemoryOwner<Rgba32> rgbaOwner = this.memoryAllocator.Allocate<Rgba32>(image.Width);
        Span<Rgba32> rgbaRow = rgbaOwner.GetSpan();

        for (int y = 0; y < image.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToRgba32(image.Configuration, pixelRow, rgbaRow);

            for (int x = 0; x < image.Width;)
            {
                TPixel currentPixel = pixelRow[x];
                Rgba32 rgba = rgbaRow[x];
                byte equalPixelCount = FindEqualPixels(pixelRow, x);

                if (equalPixelCount > 0)
                {
                    // Write the number of equal pixels, with the high bit set, indicating it's a compressed pixel run.
                    stream.WriteByte((byte)(equalPixelCount | 128));
                    this.WritePixel(stream, rgba);
                    x += equalPixelCount + 1;
                }
                else
                {
                    // Write Raw Packet (i.e., Non-Run-Length Encoded):
                    byte unEqualPixelCount = FindUnEqualPixels(pixelRow, x);
                    stream.WriteByte(unEqualPixelCount);
                    this.WritePixel(stream, rgba);
                    x++;
                    for (int i = 0; i < unEqualPixelCount; i++)
                    {
                        currentPixel = pixelRow[x];
                        rgba = rgbaRow[x];
                        this.WritePixel(stream, rgba);
                        x++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Writes a the pixel to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="color">The color of the pixel to write.</param>
    private void WritePixel(Stream stream, Rgba32 color)
    {
        switch (this.bitsPerPixel)
        {
            case TgaBitsPerPixel.Bit8:
                L8 l8 = L8.FromRgba32(color);
                stream.WriteByte(l8.PackedValue);
                break;

            case TgaBitsPerPixel.Bit16:
                Bgra5551 bgra5551 = Bgra5551.FromRgba32(color);
                Span<byte> buffer = stackalloc byte[2];
                BinaryPrimitives.WriteInt16LittleEndian(buffer, (short)bgra5551.PackedValue);
                stream.WriteByte(buffer[0]);
                stream.WriteByte(buffer[1]);

                break;

            case TgaBitsPerPixel.Bit24:
                stream.WriteByte(color.B);
                stream.WriteByte(color.G);
                stream.WriteByte(color.R);
                break;

            case TgaBitsPerPixel.Bit32:
                stream.WriteByte(color.B);
                stream.WriteByte(color.G);
                stream.WriteByte(color.R);
                stream.WriteByte(color.A);
                break;
        }
    }

    /// <summary>
    /// Finds consecutive pixels which have the same value up to 128 pixels maximum.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="pixelRow">A pixel row of the image to encode.</param>
    /// <param name="xStart">X coordinate to start searching for the same pixels.</param>
    /// <returns>The number of equal pixels.</returns>
    private static byte FindEqualPixels<TPixel>(Span<TPixel> pixelRow, int xStart)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte equalPixelCount = 0;
        TPixel startPixel = pixelRow[xStart];
        for (int x = xStart + 1; x < pixelRow.Length; x++)
        {
            TPixel nextPixel = pixelRow[x];
            if (startPixel.Equals(nextPixel))
            {
                equalPixelCount++;
            }
            else
            {
                return equalPixelCount;
            }

            if (equalPixelCount >= 127)
            {
                return equalPixelCount;
            }
        }

        return equalPixelCount;
    }

    /// <summary>
    /// Finds consecutive pixels which are unequal up to 128 pixels maximum.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="pixelRow">A pixel row of the image to encode.</param>
    /// <param name="xStart">X coordinate to start searching for the unequal pixels.</param>
    /// <returns>The number of equal pixels.</returns>
    private static byte FindUnEqualPixels<TPixel>(Span<TPixel> pixelRow, int xStart)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte unEqualPixelCount = 0;
        TPixel currentPixel = pixelRow[xStart];
        for (int x = xStart + 1; x < pixelRow.Length; x++)
        {
            TPixel nextPixel = pixelRow[x];
            if (currentPixel.Equals(nextPixel))
            {
                return unEqualPixelCount;
            }

            unEqualPixelCount++;

            if (unEqualPixelCount >= 127)
            {
                return unEqualPixelCount;
            }

            currentPixel = nextPixel;
        }

        return unEqualPixelCount;
    }

    private IMemoryOwner<byte> AllocateRow(int width, int bytesPerPixel)
        => this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, 0);

    /// <summary>
    /// Writes the 8bit pixels uncompressed to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void Write8Bit<TPixel>(Configuration configuration, Stream stream, Buffer2D<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 1);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = pixels.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToL8Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                pixelSpan.Length);
            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Writes the 16bit pixels uncompressed to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void Write16Bit<TPixel>(Configuration configuration, Stream stream, Buffer2D<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 2);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = pixels.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToBgra5551Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                pixelSpan.Length);
            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Writes the 24bit pixels uncompressed to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void Write24Bit<TPixel>(Configuration configuration, Stream stream, Buffer2D<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 3);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = pixels.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToBgr24Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                pixelSpan.Length);
            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Writes the 32bit pixels uncompressed to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    private void Write32Bit<TPixel>(Configuration configuration, Stream stream, Buffer2D<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 4);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = pixels.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToBgra32Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                pixelSpan.Length);
            stream.Write(rowSpan);
        }
    }
}

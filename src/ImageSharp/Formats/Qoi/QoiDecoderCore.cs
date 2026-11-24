// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Qoi;

internal class QoiDecoderCore : ImageDecoderCore
{
    /// <summary>
    ///     The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    ///     Used the manage memory allocations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    ///     The QOI header.
    /// </summary>
    private QoiHeader header;

    public QoiDecoderCore(DecoderOptions options)
        : base(options)
    {
        this.configuration = options.Configuration;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        // Process the header to get metadata
        this.ProcessHeader(stream);

        // Create Image object
        ImageMetadata metadata = new();
        QoiMetadata qoiMetadata = metadata.GetQoiMetadata();
        qoiMetadata.Channels = this.header.Channels;
        qoiMetadata.ColorSpace = this.header.ColorSpace;
        Image<TPixel> image = new(this.configuration, (int)this.header.Width, (int)this.header.Height, metadata);

        try
        {
            this.ProcessPixels(stream, image.GetRootFramePixelBuffer());
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ProcessHeader(stream);
        Size size = new((int)this.header.Width, (int)this.header.Height);

        ImageMetadata metadata = new();
        QoiMetadata qoiMetadata = metadata.GetQoiMetadata();
        qoiMetadata.Channels = this.header.Channels;
        qoiMetadata.ColorSpace = this.header.ColorSpace;

        return new ImageInfo(size, metadata);
    }

    /// <summary>
    /// Processes the 14-byte header to validate the image and save the metadata
    /// in <see cref="header"/>
    /// </summary>
    /// <param name="stream">The stream where the bytes are being read</param>
    /// <exception cref="InvalidImageContentException">If the stream doesn't store a qoi image</exception>
    private void ProcessHeader(BufferedReadStream stream)
    {
        Span<byte> magicBytes = stackalloc byte[4];
        Span<byte> widthBytes = stackalloc byte[4];
        Span<byte> heightBytes = stackalloc byte[4];

        // Read magic bytes
        int read = stream.Read(magicBytes);
        if (read != 4 || !magicBytes.SequenceEqual([.. QoiConstants.Magic]))
        {
            ThrowInvalidImageContentException();
        }

        // If it's a qoi image, read the rest of properties
        read = stream.Read(widthBytes);
        if (read != 4)
        {
            ThrowInvalidImageContentException();
        }

        read = stream.Read(heightBytes);
        if (read != 4)
        {
            ThrowInvalidImageContentException();
        }

        // These numbers are in Big Endian so we have to reverse them to get the real number
        uint width = BinaryPrimitives.ReadUInt32BigEndian(widthBytes);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(heightBytes);

        // The image types store each dimension as a positive int.
        if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue)
        {
            throw new InvalidImageContentException(
                $"The image has an invalid size: width = {width}, height = {height}");
        }

        int channels = stream.ReadByte();
        if (channels is -1 or (not 3 and not 4))
        {
            ThrowInvalidImageContentException();
        }

        int colorSpace = stream.ReadByte();
        if (colorSpace is -1 or (not 0 and not 1))
        {
            ThrowInvalidImageContentException();
        }

        this.header = new QoiHeader(width, height, (QoiChannels)channels, (QoiColorSpace)colorSpace);
    }

    [DoesNotReturn]
    private static void ThrowInvalidImageContentException()
        => throw new InvalidImageContentException("The image is not a valid QOI image.");

    /// <summary>
    /// Decodes the QOI chunks into the pixel buffer and reads the end marker.
    /// Damaged or truncated chunk data goes to <see cref="SegmentIntegrityHandlingExtensions.ThrowOrIgnoreImageDataError(SegmentIntegrityHandling, string)"/>.
    /// When that method returns, the decode stops. The pixels that the decoder did not decode keep the fill value.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The stream positioned at the first chunk.</param>
    /// <param name="pixels">The destination pixel buffer.</param>
    private void ProcessPixels<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<Rgba32> previouslySeenPixelsBuffer = this.memoryAllocator.Allocate<Rgba32>(64, AllocationOptions.Clean);
        Span<Rgba32> previouslySeenPixels = previouslySeenPixelsBuffer.GetSpan();
        Rgba32 previousPixel = new(0, 0, 0, 255);

        // We save the pixel to avoid losing the fully opaque black pixel
        // See https://github.com/phoboslab/qoi/issues/258
        int pixelArrayPosition = GetArrayPosition(previousPixel);
        previouslySeenPixels[pixelArrayPosition] = previousPixel;
        byte operationByte;
        Rgba32 readPixel = default;
        Span<byte> pixelBytes = MemoryMarshal.CreateSpan(ref Unsafe.As<Rgba32, byte>(ref readPixel), 4);
        TPixel pixel = default;

        for (int i = 0; i < this.header.Height; i++)
        {
            Span<TPixel> row = pixels.DangerousGetRowSpan(i);
            for (int j = 0; j < row.Length; j++)
            {
                int operation = stream.ReadByte();
                if (operation < 0)
                {
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a QOI chunk.");
                    return;
                }

                operationByte = (byte)operation;
                switch ((QoiChunk)operationByte)
                {
                    // Reading one pixel with previous alpha intact
                    case QoiChunk.QoiOpRgb:
                        if (stream.Read(pixelBytes[..3]) < 3)
                        {
                            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a QOI RGB chunk.");
                            return;
                        }

                        readPixel.A = previousPixel.A;
                        pixel = TPixel.FromRgba32(readPixel);
                        pixelArrayPosition = GetArrayPosition(readPixel);
                        previouslySeenPixels[pixelArrayPosition] = readPixel;
                        break;

                    // Reading one pixel with new alpha
                    case QoiChunk.QoiOpRgba:
                        if (stream.Read(pixelBytes) < 4)
                        {
                            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a QOI RGBA chunk.");
                            return;
                        }

                        pixel = TPixel.FromRgba32(readPixel);
                        pixelArrayPosition = GetArrayPosition(readPixel);
                        previouslySeenPixels[pixelArrayPosition] = readPixel;
                        break;

                    default:
                        switch ((QoiChunk)(operationByte & 0b11000000))
                        {
                            // Getting one pixel from previously seen pixels
                            case QoiChunk.QoiOpIndex:
                                readPixel = previouslySeenPixels[operationByte];
                                pixel = TPixel.FromRgba32(readPixel);
                                break;

                            // Get one pixel from the difference (-2..1) of the previous pixel
                            case QoiChunk.QoiOpDiff:
                                int redDifference = (operationByte & 0b00110000) >> 4;
                                int greenDifference = (operationByte & 0b00001100) >> 2;
                                int blueDifference = operationByte & 0b00000011;
                                readPixel = previousPixel with
                                {
                                    R = (byte)Numerics.Modulo256(previousPixel.R + (redDifference - 2)),
                                    G = (byte)Numerics.Modulo256(previousPixel.G + (greenDifference - 2)),
                                    B = (byte)Numerics.Modulo256(previousPixel.B + (blueDifference - 2))
                                };
                                pixel = TPixel.FromRgba32(readPixel);
                                pixelArrayPosition = GetArrayPosition(readPixel);
                                previouslySeenPixels[pixelArrayPosition] = readPixel;
                                break;

                            // Get green difference in 6 bits and red and blue differences
                            // depending on the green one
                            case QoiChunk.QoiOpLuma:
                                int diffGreen = operationByte & 0b00111111;
                                int currentGreen = Numerics.Modulo256(previousPixel.G + (diffGreen - 32));
                                int nextByte = stream.ReadByte();
                                if (nextByte < 0)
                                {
                                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a QOI luma chunk.");
                                    return;
                                }

                                int diffRedDG = nextByte >> 4;
                                int diffBlueDG = nextByte & 0b00001111;
                                int currentRed = Numerics.Modulo256(diffRedDG - 8 + (diffGreen - 32) + previousPixel.R);
                                int currentBlue = Numerics.Modulo256(diffBlueDG - 8 + (diffGreen - 32) + previousPixel.B);
                                readPixel = previousPixel with { R = (byte)currentRed, B = (byte)currentBlue, G = (byte)currentGreen };
                                pixel = TPixel.FromRgba32(readPixel);
                                pixelArrayPosition = GetArrayPosition(readPixel);
                                previouslySeenPixels[pixelArrayPosition] = readPixel;
                                break;

                            // Repeating the previous pixel 1..63 times
                            case QoiChunk.QoiOpRun:
                                int repetitions = operationByte & 0b00111111;
                                if (repetitions is 62 or 63)
                                {
                                    ThrowInvalidImageContentException();
                                }

                                readPixel = previousPixel;
                                pixel = TPixel.FromRgba32(readPixel);
                                for (int k = -1; k < repetitions; k++, j++)
                                {
                                    if (j == row.Length)
                                    {
                                        j = 0;
                                        i++;
                                        if (i == pixels.Height)
                                        {
                                            // The run is longer than the rest of the image. The decoder decoded all
                                            // pixels of the image, so the IgnoreImageData mode keeps them.
                                            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("A QOI run chunk exceeds the image size.");
                                            return;
                                        }

                                        row = pixels.DangerousGetRowSpan(i);
                                    }

                                    row[j] = pixel;
                                }

                                j--;
                                continue;

                            default:
                                ThrowInvalidImageContentException();
                                return;
                        }

                        break;
                }

                row[j] = pixel;
                previousPixel = readPixel;
            }
        }

        // A QOI file is a 14-byte header, the data chunks and an 8-byte end marker. The end marker is seven 0x00 bytes
        // and one 0x01 byte. See the QOI specification, version 1.0. A missing, short or wrong marker is damaged image
        // data. The decoder decoded all pixels at this point, so the IgnoreImageData mode keeps them.
        for (int i = 0; i < 8; i++)
        {
            // ReadByte returns -1 at the end of the stream, which is not a marker byte.
            if (stream.ReadByte() != (i < 7 ? 0 : 1))
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The QOI end marker is missing or not valid.");
                return;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetArrayPosition(Rgba32 pixel)
        => Numerics.Modulo64((pixel.R * 3) + (pixel.G * 5) + (pixel.B * 7) + (pixel.A * 11));
}

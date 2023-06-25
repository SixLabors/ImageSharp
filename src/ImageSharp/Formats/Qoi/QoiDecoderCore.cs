// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Qoi;

internal class QoiDecoderCore : IImageDecoderInternals
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
    ///     Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    ///     The QOI header.
    /// </summary>
    private QoiHeader header;

    public QoiDecoderCore(DecoderOptions options)
    {
        this.Options = options;
        this.configuration = options.Configuration;
        this.skipMetadata = options.SkipMetadata;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    public DecoderOptions Options { get; }

    public Size Dimensions { get; }

    /// <inheritdoc />
    public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Process the header to get metadata
        this.ProcessHeader(stream);

        // Create Image object
        ImageMetadata metadata = new()
        {
            DecodedImageFormat = QoiFormat.Instance,
            HorizontalResolution = this.header.Width,
            VerticalResolution = this.header.Height,
            ResolutionUnits = PixelResolutionUnit.AspectRatio
        };
        Image<TPixel> image = new(this.configuration, (int)this.header.Width, (int)this.header.Height, metadata);
        Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
        
        this.ProcessPixels(stream, pixels);

        return image;
    }

    /// <inheritdoc />
    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        ImageMetadata metadata = new();

        this.ProcessHeader(stream);
        PixelTypeInfo pixelType = new(8 * (int)this.header.Channels);
        Size size = new((int)this.header.Width, (int)this.header.Height);

        return new ImageInfo(pixelType, size, metadata);
    }

    /// <summary>
    /// Processes the 14-byte header to validate the image and save the metadata
    /// in <see cref="header"/>
    /// </summary>
    /// <param name="stream">The stream where the bytes are being read</param>
    /// <exception cref="InvalidImageContentException">If the stream doesn't store a qoi image</exception>
    private void ProcessHeader(Stream stream)
    {
        Span<byte> magicBytes = stackalloc byte[4];
        Span<byte> widthBytes = stackalloc byte[4];
        Span<byte> heightBytes = stackalloc byte[4];

        // Read magic bytes
        int read = stream.Read(magicBytes);
        if (read != 4 || !magicBytes.SequenceEqual(QoiConstants.Magic.ToArray()))
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
        uint width = BinaryPrimitives.ReadUInt32BigEndian(widthBytes),
            height = BinaryPrimitives.ReadUInt32BigEndian(heightBytes);
        if (width == 0 || height == 0)
        {
            throw new InvalidImageContentException(
                $"The image has an invalid size: width = {width}, height = {height}");
        }

        int channels = stream.ReadByte();
        if (channels is -1 or (not 3 and not 4))
        {
            ThrowInvalidImageContentException();
        }

        PixelTypeInfo pixelType = new(8 * channels);

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

    private void ProcessPixels<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rgba32[] previouslySeenPixels = new Rgba32[64];
        Rgba32 previousPixel = new (0,0,0,255);

        // We save the pixel to avoid loosing the fully opaque black pixel
        // See https://github.com/phoboslab/qoi/issues/258
        int pixelArrayPosition = this.GetArrayPosition(previousPixel);
        previouslySeenPixels[pixelArrayPosition] = previousPixel;

        for (int i = 0; i < this.header.Height; i++)
        {
            for (int j = 0; j < this.header.Width; j++)
            {
                byte operationByte = (byte)stream.ReadByte();
                byte[] pixelBytes;
                Rgba32 readPixel;
                TPixel pixel = new();
                switch ((QoiChunkEnum)operationByte)
                {
                    // Reading one pixel with previous alpha intact
                    case QoiChunkEnum.QOI_OP_RGB:
                        pixelBytes = new byte[3];
                        if (stream.Read(pixelBytes) < 3)
                        {
                            ThrowInvalidImageContentException();
                        }

                        readPixel = previousPixel with { R = pixelBytes[0], G = pixelBytes[1], B = pixelBytes[2] };
                        pixel.FromRgba32(readPixel);
                        pixelArrayPosition = this.GetArrayPosition(readPixel);
                        previouslySeenPixels[pixelArrayPosition] = readPixel;
                        break;

                    // Reading one pixel with new alpha
                    case QoiChunkEnum.QOI_OP_RGBA:
                        pixelBytes = new byte[4];
                        if (stream.Read(pixelBytes) < 4)
                        {
                            ThrowInvalidImageContentException();
                        }

                        readPixel = new Rgba32(pixelBytes[0], pixelBytes[1], pixelBytes[2], pixelBytes[3]);
                        pixel.FromRgba32(readPixel);
                        pixelArrayPosition = this.GetArrayPosition(readPixel);
                        previouslySeenPixels[pixelArrayPosition] = readPixel;
                        break;

                    default:
                        switch ((QoiChunkEnum)(operationByte & 0b11000000))
                        {
                            // Getting one pixel from previously seen pixels
                            case QoiChunkEnum.QOI_OP_INDEX:
                                readPixel = previouslySeenPixels[operationByte];
                                pixel.FromRgba32(readPixel);
                                break;

                            // Get one pixel from the difference (-2..1) of the previous pixel
                            case QoiChunkEnum.QOI_OP_DIFF:
                                byte redDifference = (byte)((operationByte & 0b00110000) >> 4),
                                    greenDifference = (byte)((operationByte & 0b00001100) >> 2),
                                    blueDifference = (byte)(operationByte & 0b00000011);
                                readPixel = previousPixel with
                                {
                                    R = (byte)((previousPixel.R + (redDifference - 2)) % 256),
                                    G = (byte)((previousPixel.G + (greenDifference - 2)) % 256),
                                    B = (byte)((previousPixel.B + (blueDifference - 2)) % 256)
                                };
                                pixel.FromRgba32(readPixel);
                                pixelArrayPosition = this.GetArrayPosition(readPixel);
                                previouslySeenPixels[pixelArrayPosition] = readPixel;
                                break;

                            // Get green difference in 6 bits and red and blue differences
                            // depending on the green one
                            case QoiChunkEnum.QOI_OP_LUMA:
                                byte diffGreen = (byte)(operationByte & 0b00111111),
                                    currentGreen = (byte)((previousPixel.G + (diffGreen - 32)) % 256),
                                    nextByte = (byte)stream.ReadByte(),
                                    diffRedDG = (byte)(nextByte >> 4),
                                    diffBlueDG = (byte)(nextByte & 0b00001111),
                                    currentRed = (byte)((diffRedDG-8 + (diffGreen - 32) + previousPixel.R)%256),
                                    currentBlue = (byte)((diffBlueDG-8 + (diffGreen - 32) + previousPixel.B)%256);
                                readPixel = previousPixel with { R = currentRed, B = currentBlue, G = currentGreen };
                                pixel.FromRgba32(readPixel);
                                pixelArrayPosition = this.GetArrayPosition(readPixel);
                                previouslySeenPixels[pixelArrayPosition] = readPixel;
                                break;

                            // Repeating the previous pixel 1..63 times
                            case QoiChunkEnum.QOI_OP_RUN:
                                byte repetitions = (byte)(operationByte & 0b00111111);
                                if(repetitions is 62 or 63)
                                {
                                    ThrowInvalidImageContentException();
                                }

                                readPixel = previousPixel;
                                pixel.FromRgba32(readPixel);
                                for (int k = -1; k < repetitions; k++, j++)
                                {
                                    if (j == this.header.Width)
                                    {
                                        j = 0;
                                        i++;
                                    }
                                    pixels[j,i] = pixel;
                                }

                                j--;
                                continue;

                            default:
                                ThrowInvalidImageContentException();
                                return;
                        }
                        break;
                }
                pixels[j,i] = pixel;
                previousPixel = readPixel;
            }
        }
    }

    private int GetArrayPosition(Rgba32 pixel) => ((pixel.R * 3) + (pixel.G * 5) + (pixel.B * 7) + (pixel.A * 11)) % 64;
}

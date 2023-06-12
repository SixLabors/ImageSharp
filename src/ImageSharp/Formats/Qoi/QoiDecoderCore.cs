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

    public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel> => throw new NotImplementedException();

    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        ImageMetadata metadata = new();
        QoiMetadata qoiMetadata = metadata.GetQoiMetadata();

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

        qoiMetadata.Width = width;
        qoiMetadata.Height = height;

        Size size = new((int)width, (int)height);

        int channels = stream.ReadByte();
        if (channels is -1 or (not 3 and not 4))
        {
            ThrowInvalidImageContentException();
        }

        PixelTypeInfo pixelType = new(8 * channels);
        qoiMetadata.Channels = (QoiChannels)channels;

        int colorSpace = stream.ReadByte();
        if (colorSpace is -1 or (not 0 and not 1))
        {
            ThrowInvalidImageContentException();
        }

        qoiMetadata.ColorSpace = (QoiColorSpace)colorSpace;

        return new ImageInfo(pixelType, size, metadata);
    }

    [DoesNotReturn]
    private static void ThrowInvalidImageContentException()
        => throw new InvalidImageContentException("The image is not a valid QOI image.");
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

        byte[] magicBytes = new byte[4], widthBytes = new byte[4], heightBytes = new byte[4];

        // Read magic bytes
        int read = stream.Read(magicBytes);
        if (read != 4 || !magicBytes.SequenceEqual(QoiConstants.Magic.ToArray()))
        {
            throw new InvalidImageContentException("The image is not a QOI image");
        }

        // If it's a qoi image, read the rest of properties
        read = stream.Read(widthBytes);
        if (read != 4)
        {
            throw new InvalidImageContentException("The image is not a QOI image");
        }

        read = stream.Read(heightBytes);
        if (read != 4)
        {
            throw new InvalidImageContentException("The image is not a QOI image");
        }

        widthBytes = widthBytes.Reverse().ToArray();
        heightBytes = heightBytes.Reverse().ToArray();
        Size size = new((int)BitConverter.ToUInt32(widthBytes), (int)BitConverter.ToUInt32(heightBytes));

        int channels = stream.ReadByte();
        if (channels == -1)
        {
            throw new InvalidImageContentException("The image is not a QOI image");
        }

        PixelTypeInfo pixelType = new(8 * channels);

        int colorSpace = stream.ReadByte();
        if (colorSpace == -1)
        {
            throw new InvalidImageContentException("The image is not a QOI image");
        }

        return new ImageInfo(pixelType, size, metadata);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using ImageMagick;
using ImageMagick.Formats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

public class MagickReferenceDecoder : ImageDecoder
{
    private readonly bool validate;

    public MagickReferenceDecoder()
        : this(true)
    {
    }

    public MagickReferenceDecoder(bool validate) => this.validate = validate;

    public static MagickReferenceDecoder Instance { get; } = new();

    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Configuration configuration = options.Configuration;
        BmpReadDefines bmpReadDefines = new()
        {
            IgnoreFileSize = !this.validate
        };

        MagickReadSettings settings = new()
        {
            FrameCount = (int)options.MaxFrames
        };
        settings.SetDefines(bmpReadDefines);

        using MagickImageCollection magickImageCollection = new(stream, settings);
        List<ImageFrame<TPixel>> framesList = new();
        foreach (IMagickImage<ushort> magicFrame in magickImageCollection)
        {
            ImageFrame<TPixel> frame = new(configuration, magicFrame.Width, magicFrame.Height);
            framesList.Add(frame);

            MemoryGroup<TPixel> framePixels = frame.PixelBuffer.FastMemoryGroup;

            using IUnsafePixelCollection<ushort> pixels = magicFrame.GetPixelsUnsafe();
            if (magicFrame.Depth is 12 or 10 or 8 or 6 or 5 or 4 or 3 or 2 or 1)
            {
                byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                FromRgba32Bytes(configuration, data, framePixels);
            }
            else if (magicFrame.Depth is 16 or 14)
            {
                ushort[] data = pixels.ToShortArray(PixelMapping.RGBA);
                Span<byte> bytes = MemoryMarshal.Cast<ushort, byte>(data.AsSpan());
                FromRgba64Bytes(configuration, bytes, framePixels);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        return new Image<TPixel>(configuration, new ImageMetadata(), framesList);
    }

    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using Image<Rgba32> image = this.Decode<Rgba32>(options, stream, cancellationToken);
        return new(image.PixelType, image.Width, image.Height, image.Metadata);
    }

    private static void FromRgba32Bytes<TPixel>(Configuration configuration, Span<byte> rgbaBytes, IMemoryGroup<TPixel> destinationGroup)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        Span<Rgba32> sourcePixels = MemoryMarshal.Cast<byte, Rgba32>(rgbaBytes);
        foreach (Memory<TPixel> m in destinationGroup)
        {
            Span<TPixel> destBuffer = m.Span;
            PixelOperations<TPixel>.Instance.FromRgba32(
                configuration,
                sourcePixels[..destBuffer.Length],
                destBuffer);
            sourcePixels = sourcePixels[destBuffer.Length..];
        }
    }

    private static void FromRgba64Bytes<TPixel>(Configuration configuration, Span<byte> rgbaBytes, IMemoryGroup<TPixel> destinationGroup)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        foreach (Memory<TPixel> m in destinationGroup)
        {
            Span<TPixel> destBuffer = m.Span;
            PixelOperations<TPixel>.Instance.FromRgba64Bytes(
                configuration,
                rgbaBytes,
                destBuffer,
                destBuffer.Length);
            rgbaBytes = rgbaBytes[(destBuffer.Length * 8)..];
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

internal static class ReferenceCodecUtilities
{
    /// <summary>
    /// Ensures that the metadata is properly initialized for reference and test encoders which cannot initialize
    /// metadata in the same manner as our built in decoders.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="image">The decoded image.</param>
    /// <param name="format">The image format</param>
    /// <exception cref="NotSupportedException">The format is unknown.</exception>
    public static Image<TPixel> EnsureDecodedMetadata<TPixel>(Image<TPixel> image, IImageFormat format)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (image.Metadata.DecodedImageFormat is null)
        {
            image.Metadata.DecodedImageFormat = format;
        }

        foreach (ImageFrame frame in image.Frames)
        {
            frame.Metadata.DecodedImageFormat = format;
        }

        switch (format)
        {
            case BmpFormat:
                image.Metadata.GetBmpMetadata();
                break;
            case GifFormat:
                image.Metadata.GetGifMetadata();
                foreach (ImageFrame frame in image.Frames)
                {
                    frame.Metadata.GetGifMetadata();
                }

                break;
            case JpegFormat:
                image.Metadata.GetJpegMetadata();
                break;
            case PbmFormat:
                image.Metadata.GetPbmMetadata();
                break;
            case PngFormat:
                image.Metadata.GetPngMetadata();
                foreach (ImageFrame frame in image.Frames)
                {
                    frame.Metadata.GetPngMetadata();
                }

                break;
            case QoiFormat:
                image.Metadata.GetQoiMetadata();
                break;
            case TgaFormat:
                image.Metadata.GetTgaMetadata();
                break;
            case TiffFormat:
                image.Metadata.GetTiffMetadata();
                foreach (ImageFrame frame in image.Frames)
                {
                    frame.Metadata.GetTiffMetadata();
                }

                break;
            case WebpFormat:
                image.Metadata.GetWebpMetadata();
                foreach (ImageFrame frame in image.Frames)
                {
                    frame.Metadata.GetWebpMetadata();
                }

                break;
        }

        return image;
    }
}

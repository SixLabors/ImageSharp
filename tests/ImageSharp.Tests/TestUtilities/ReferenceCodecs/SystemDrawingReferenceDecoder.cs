// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SDBitmap = System.Drawing.Bitmap;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

public class SystemDrawingReferenceDecoder : ImageDecoder
{
    public static SystemDrawingReferenceDecoder Instance { get; } = new SystemDrawingReferenceDecoder();

    protected internal override IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using SDBitmap sourceBitmap = new(stream);
        PixelTypeInfo pixelType = new(SDImage.GetPixelFormatSize(sourceBitmap.PixelFormat));
        return new ImageInfo(pixelType, sourceBitmap.Width, sourceBitmap.Height, new ImageMetadata());
    }

    protected internal override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using SDBitmap sourceBitmap = new(stream);
        if (sourceBitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
        {
            return SystemDrawingBridge.From32bppArgbSystemDrawingBitmap<TPixel>(sourceBitmap);
        }

        using SDBitmap convertedBitmap = new(
            sourceBitmap.Width,
            sourceBitmap.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(convertedBitmap))
        {
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            g.DrawImage(sourceBitmap, 0, 0, sourceBitmap.Width, sourceBitmap.Height);
        }

        return SystemDrawingBridge.From32bppArgbSystemDrawingBitmap<TPixel>(convertedBitmap);
    }

    protected internal override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);
}

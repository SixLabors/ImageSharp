// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

public class SystemDrawingReferenceEncoder : ImageEncoder
{
    private readonly ImageFormat imageFormat;

    public SystemDrawingReferenceEncoder(ImageFormat imageFormat)
        => this.imageFormat = imageFormat;

    public static SystemDrawingReferenceEncoder Png { get; } = new SystemDrawingReferenceEncoder(ImageFormat.Png);

    public static SystemDrawingReferenceEncoder Bmp { get; } = new SystemDrawingReferenceEncoder(ImageFormat.Bmp);

    public override void Encode<TPixel>(Image<TPixel> image, Stream stream)
    {
        using System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.To32bppArgbSystemDrawingBitmap(image);
        sdBitmap.Save(stream, this.imageFormat);
    }

    public override Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        using (System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.To32bppArgbSystemDrawingBitmap(image))
        {
            sdBitmap.Save(stream, this.imageFormat);
        }

        return Task.CompletedTask;
    }
}

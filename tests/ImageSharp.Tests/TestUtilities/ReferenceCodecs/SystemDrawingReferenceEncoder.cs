// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing.Imaging;
using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    public class SystemDrawingReferenceEncoder : IImageEncoder
    {
        private readonly System.Drawing.Imaging.ImageFormat imageFormat;

        public SystemDrawingReferenceEncoder(ImageFormat imageFormat)
        {
            this.imageFormat = imageFormat;
        }

        public static SystemDrawingReferenceEncoder Png { get; } = new SystemDrawingReferenceEncoder(ImageFormat.Png);

        public static SystemDrawingReferenceEncoder Bmp { get; } = new SystemDrawingReferenceEncoder(ImageFormat.Bmp);

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.To32bppArgbSystemDrawingBitmap(image))
            {
                sdBitmap.Save(stream, this.imageFormat);
            }
        }
    }
}

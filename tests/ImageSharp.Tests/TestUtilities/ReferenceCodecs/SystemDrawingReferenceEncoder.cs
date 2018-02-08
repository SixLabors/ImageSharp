// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Text;

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

        public static SystemDrawingReferenceEncoder Png { get; } = new SystemDrawingReferenceEncoder(System.Drawing.Imaging.ImageFormat.Png);

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.ToSystemDrawingBitmap(image))
            {
                sdBitmap.Save(stream, this.imageFormat);
            }
        }
    }
}

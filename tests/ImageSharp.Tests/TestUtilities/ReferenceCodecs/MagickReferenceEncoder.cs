// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ImageMagick;

using SixLabors.ImageSharp.Advanced;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    public class MagickReferenceEncoder : IImageEncoder
    {
        public MagickReferenceEncoder(MagickFormat format)
        {
            this.Format = format;
        }

        public MagickFormat Format { get; }

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var black = MagickColor.FromRgba(0, 0, 0, 255);
            using (var magickImage = new MagickImage(black, image.Width, image.Height))
            {
                bool isDeep = Unsafe.SizeOf<TPixel>() > 32;

                magickImage.Depth = isDeep ? 16 : 8;

                Span<TPixel> allPixels = image.GetPixelSpan();

                using (IPixelCollection magickPixels = magickImage.GetPixelsUnsafe())
                {
                    if (isDeep)
                    {
                        ushort[] data = new ushort[allPixels.Length * 4];
                        Span<Rgba64> dataSpan = MemoryMarshal.Cast<ushort, Rgba64>(data);
                        PixelOperations<TPixel>.Instance.ToRgba64(allPixels, dataSpan, allPixels.Length);
                        magickPixels.SetPixels(data);
                    }
                    else
                    {
                        byte[] data = new byte[allPixels.Length * 4];
                        PixelOperations<TPixel>.Instance.ToRgba32Bytes(allPixels, data, allPixels.Length);
                        magickPixels.SetPixels(data);
                    }
                }

                magickImage.Write(stream, this.Format);
            }
        }
    }
}
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.InteropServices;

using ImageMagick;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    public class MagickReferenceDecoder : IImageDecoder
    {
        public static MagickReferenceDecoder Instance { get; } = new MagickReferenceDecoder();

        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var magickImage = new MagickImage(stream))
            {
                var result = new Image<TPixel>(configuration, magickImage.Width, magickImage.Height);
                Span<TPixel> resultPixels = result.GetPixelSpan();

                using (IPixelCollection pixels = magickImage.GetPixelsUnsafe())
                {
                    if (magickImage.Depth == 8)
                    {
                        byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                        PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(data, resultPixels, resultPixels.Length);
                    }
                    else if (magickImage.Depth == 16)
                    {
                        ushort[] data = pixels.ToShortArray(PixelMapping.RGBA);
                        Span<byte> bytes = MemoryMarshal.Cast<ushort, byte>(data.AsSpan());

                        PixelOperations<TPixel>.Instance.PackFromRgba64Bytes(bytes, resultPixels, resultPixels.Length);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                return result;
            }
        }
    }
}
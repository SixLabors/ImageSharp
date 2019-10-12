// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using ImageMagick;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    using static TestImages.Tga;

    public class TgaDecoderTests
    {
        [Theory]
        [WithFile(Grey, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_MonoChrome<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit15, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_15Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit15Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_15Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16PalRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24TopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(GreyRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLenghtEncoded_MonoChrome<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLenghtEncoded_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLenghtEncoded_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLenghtEncoded_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16Pal, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24Pal, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                CompareWithReferenceDecoder(provider, image);
            }
        }

        private void CompareWithReferenceDecoder<TPixel>(TestImageProvider<TPixel> provider, Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestImageProvider<TPixel>.GetFilePathOrNull(provider);
            if (path == null)
            {
                throw new InvalidOperationException("CompareToOriginal() works only with file providers!");
            }

            TestFile testFile = TestFile.Create(path);
            Image<Rgba32> magickImage = this.DecodeWithMagick<Rgba32>(Configuration.Default, new FileInfo(testFile.FullPath));
            ImageComparer.Exact.VerifySimilarity(image, magickImage);
        }

        private Image<TPixel> DecodeWithMagick<TPixel>(Configuration configuration, FileInfo fileInfo)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var magickImage = new MagickImage(fileInfo))
            {
                var result = new Image<TPixel>(configuration, magickImage.Width, magickImage.Height);
                Span<TPixel> resultPixels = result.GetPixelSpan();

                using (IPixelCollection pixels = magickImage.GetPixelsUnsafe())
                {
                    byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                    PixelOperations<TPixel>.Instance.FromRgba32Bytes(
                        configuration,
                        data,
                        resultPixels,
                        resultPixels.Length);
                }

                return result;
            }
        }
    }
}

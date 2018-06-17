// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class JpegDecoderTests
    {
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_Orig<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipTest(provider))
            {
                return;
            }

            // For 32 bit test enviroments:
            provider.Configuration.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithModeratePooling();

            using (Image<TPixel> image = provider.GetImage(GolangJpegDecoder))
            {
                image.DebugSave(provider);
                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(
                    this.GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }

            provider.Configuration.MemoryAllocator.ReleaseRetainedResources();
        }

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipTest(provider))
            {
                // skipping to avoid OutOfMemoryException on CI
                return;
            }

            using (Image<TPixel> image = provider.GetImage(PdfJsJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(
                    this.GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Issues.CriticalEOF214, PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_CriticalEOF_ShouldThrow_Golang<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: We need a public ImageDecoderException class in ImageSharp!
            Assert.ThrowsAny<Exception>(() => provider.GetImage(GolangJpegDecoder));
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Issues.CriticalEOF214, PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_CriticalEOF_ShouldThrow_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: We need a public ImageDecoderException class in ImageSharp!
            Assert.ThrowsAny<Exception>(() => provider.GetImage(PdfJsJpegDecoder));
        }

        [Theory(Skip = "Debug only, enable manually!")]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void CompareJpegDecoders_Baseline<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            this.CompareJpegDecodersImpl(provider, DecodeBaselineJpegOutputName);
        }
    }
}
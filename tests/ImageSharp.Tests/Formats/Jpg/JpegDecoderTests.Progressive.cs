// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class JpegDecoderTests
    {
        public const string DecodeProgressiveJpegOutputName = "DecodeProgressiveJpeg";

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void DecodeProgressiveJpeg_Orig<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (TestEnvironment.RunsOnCI && !TestEnvironment.Is64BitProcess)
            {
                // skipping to avoid OutOfMemoryException on CI
                return;
            }

            // Golang decoder is unable to decode these:
            if (PdfJsOnly.Any(fn => fn.Contains(provider.SourceFileOrDescription)))
            {
                return;
            }

            // For 32 bit test enviroments:
            provider.Configuration.MemoryManager = ArrayPoolMemoryManager.CreateWithModeratePooling();

            using (Image<TPixel> image = provider.GetImage(GolangJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeProgressiveJpegOutputName;
                image.CompareToReferenceOutput(
                    this.GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }

            provider.Configuration.MemoryManager.ReleaseRetainedResources();
        }

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void DecodeProgressiveJpeg_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
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

                provider.Utility.TestName = DecodeProgressiveJpegOutputName;
                image.CompareToReferenceOutput(
                    this.GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }
        }

        [Theory(Skip = "Debug only, enable manually!")]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void CompareJpegDecoders_Progressive<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            this.CompareJpegDecodersImpl(provider, DecodeProgressiveJpegOutputName);
        }
    }
}
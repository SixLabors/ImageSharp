// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders
{
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;

    using Xunit;

    public class PorterDuffCompositorTests
    {
        // TODO: Add other modes to compare.
        public static readonly TheoryData<PixelAlphaCompositionMode> CompositingOperators =
            new TheoryData<PixelAlphaCompositionMode>
                {
                    PixelAlphaCompositionMode.Src,
                    PixelAlphaCompositionMode.SrcAtop,
                    PixelAlphaCompositionMode.SrcOver,
                    PixelAlphaCompositionMode.SrcIn,
                    PixelAlphaCompositionMode.SrcOut,
                    PixelAlphaCompositionMode.Dest,
                    PixelAlphaCompositionMode.DestAtop,
                    PixelAlphaCompositionMode.DestOver,
                    PixelAlphaCompositionMode.DestIn,
                    PixelAlphaCompositionMode.DestOut,
                    PixelAlphaCompositionMode.Clear,
                    PixelAlphaCompositionMode.Xor
                };

        [Theory]
        [WithFile(TestImages.Png.PDDest, nameof(CompositingOperators), PixelTypes.Rgba32)]
        public void PorterDuffOutputIsCorrect(TestImageProvider<Rgba32> provider, PixelAlphaCompositionMode mode)
        {
            var srcFile = TestFile.Create(TestImages.Png.PDSrc);
            using (Image<Rgba32> src = srcFile.CreateRgba32Image())
            using (Image<Rgba32> dest = provider.GetImage())
            {
                var options = new GraphicsOptions
                {
                    Antialias = false,
                    AlphaCompositionMode = mode
                };

                using (Image<Rgba32> res = dest.Clone(x => x.DrawImage(src, options)))
                {
                    string combinedMode = mode.ToString();

                    if (combinedMode != "Src" && combinedMode.StartsWith("Src"))
                    {
                        combinedMode = combinedMode.Substring(3);
                    }

                    res.DebugSave(provider, combinedMode);
                    res.CompareToReferenceOutput(provider, combinedMode);
                }
            }
        }
    }
}

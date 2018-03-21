// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders
{
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Drawing;

    using Xunit;

    public class PorterDuffCompositorTests
    {
        // TODO: Add other modes to compare.
        public static readonly TheoryData<PixelBlenderMode> CompositingOperators =
            new TheoryData<PixelBlenderMode>
                {
                    PixelBlenderMode.Src,
                    PixelBlenderMode.Atop,
                    PixelBlenderMode.Over,
                    PixelBlenderMode.In,
                    PixelBlenderMode.Out,
                    PixelBlenderMode.Dest,
                    PixelBlenderMode.DestAtop,
                    PixelBlenderMode.DestOver,
                    PixelBlenderMode.DestIn,
                    PixelBlenderMode.DestOut,
                    PixelBlenderMode.Clear,
                    PixelBlenderMode.Xor
                };

        [Theory]
        [WithFile(TestImages.Png.PDDest, nameof(CompositingOperators), PixelTypes.Rgba32)]
        public void PorterDuffOutputIsCorrect(TestImageProvider<Rgba32> provider, PixelBlenderMode mode)
        {
            var srcFile = TestFile.Create(TestImages.Png.PDSrc);
            using (Image<Rgba32> src = srcFile.CreateImage())
            using (Image<Rgba32> dest = provider.GetImage())
            {
                using (Image<Rgba32> res = dest.Clone(x => x.Blend(src, new GraphicsOptions { BlenderMode = mode })))
                {
                    res.DebugSave(provider, mode.ToString());
                    res.CompareToReferenceOutput(provider, mode.ToString());
                }
            }
        }
    }
}
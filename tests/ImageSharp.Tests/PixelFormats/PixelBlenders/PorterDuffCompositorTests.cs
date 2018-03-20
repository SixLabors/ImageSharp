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
        [MemberData(nameof(CompositingOperators))]
        public void PorterDuffOutputIsCorrect(PixelBlenderMode mode)
        {
            string path = TestEnvironment.CreateOutputDirectory("PorterDuff");
            var srcFile = TestFile.Create(TestImages.Png.PDSrc);
            var destFile = TestFile.Create(TestImages.Png.PDDest);

            using (Image<Rgba32> src = srcFile.CreateImage())
            using (Image<Rgba32> dest = destFile.CreateImage())
            {
                using (Image<Rgba32> res = dest.Clone(x => x.Blend(src, new GraphicsOptions { BlenderMode = mode })))
                {
                    // TODO: Generate reference files once this works.
                    res.Save($"{path}/{mode}.png");
                }
            }
        }
    }
}
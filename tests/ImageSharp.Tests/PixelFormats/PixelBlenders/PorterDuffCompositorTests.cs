// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders
{
    public class PorterDuffCompositorTests
    {
        // TODO: Add other modes to compare.
        private static PixelBlenderMode[] CompositingOperators =
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

        [Fact]
        public void PorterDuffOutputIsCorrect()
        {
            string path = TestEnvironment.CreateOutputDirectory("PorterDuff");
            var srcFile = TestFile.Create(TestImages.Png.PDSrc);
            var destFile = TestFile.Create(TestImages.Png.PDDest);

            using (Image<Rgba32> src = srcFile.CreateImage())
            using (Image<Rgba32> dest = destFile.CreateImage())
            {
                foreach (PixelBlenderMode m in CompositingOperators)
                {
                    using (Image<Rgba32> res = dest.Clone(x => x.Blend(src, new GraphicsOptions { BlenderMode = m })))
                    {
                        // TODO: Generate reference files once this works.
                        res.Save($"{path}/{m}.png");
                    }
                }
            }
        }
    }
}

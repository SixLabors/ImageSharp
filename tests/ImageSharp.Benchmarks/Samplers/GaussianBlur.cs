// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Samplers
{
    [Config(typeof(Config.ShortClr))]
    public class GaussianBlur
    {
        [Benchmark]
        public void Blur()
        {
            using (var image = new Image<Rgba32>(Configuration.Default, 400, 400, Color.White))
            {
                image.Mutate(c => c.GaussianBlur());
            }
        }
    }
}

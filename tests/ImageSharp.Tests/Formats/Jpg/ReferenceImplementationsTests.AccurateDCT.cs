// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class ReferenceImplementationsTests
    {
        public class AccurateDCT : JpegFixture
        {
            public AccurateDCT(ITestOutputHelper output)
                : base(output)
            {
            }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void ForwardThenInverse(int seed)
            {
                float[] data = JpegFixture.Create8x8RandomFloatData(-1000, 1000, seed);

                var b0 = default(Block8x8F);
                b0.LoadFrom(data);

                Block8x8F b1 = ReferenceImplementations.AccurateDCT.TransformFDCT(ref b0);
                Block8x8F b2 = ReferenceImplementations.AccurateDCT.TransformIDCT(ref b1);

                this.CompareBlocks(b0, b2, 1e-4f);
            }
        }
    }
}
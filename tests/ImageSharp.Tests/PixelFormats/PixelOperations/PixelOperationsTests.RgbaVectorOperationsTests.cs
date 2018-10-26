// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class RgbaVectorOperationsTests : PixelOperationsTests<RgbaVector>
        {
            public RgbaVectorOperationsTests(ITestOutputHelper output)
                : base(output)
            {
            }

            [Fact]
            public void IsSpecialImplementation() => Assert.IsType<RgbaVector.PixelOperations>(PixelOperations<RgbaVector>.Instance);
        }
    }
}
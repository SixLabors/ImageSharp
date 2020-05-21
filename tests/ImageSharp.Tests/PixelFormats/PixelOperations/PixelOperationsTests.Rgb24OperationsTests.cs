// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class Rgb24OperationsTests : PixelOperationsTests<Rgb24>
        {
            public Rgb24OperationsTests(ITestOutputHelper output)
                : base(output)
            {
                this.HasAlpha = false;
            }

            [Fact]
            public void IsSpecialImplementation() => Assert.IsType<Rgb24.PixelOperations>(PixelOperations<Rgb24>.Instance);
        }
    }
}

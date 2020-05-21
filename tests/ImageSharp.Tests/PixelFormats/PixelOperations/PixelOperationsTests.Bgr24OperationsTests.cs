// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class Bgr24OperationsTests : PixelOperationsTests<Bgr24>
        {
            public Bgr24OperationsTests(ITestOutputHelper output)
                : base(output)
            {
                this.HasAlpha = false;
            }

            [Fact]
            public void IsSpecialImplementation() => Assert.IsType<Bgr24.PixelOperations>(PixelOperations<Bgr24>.Instance);
        }
    }
}
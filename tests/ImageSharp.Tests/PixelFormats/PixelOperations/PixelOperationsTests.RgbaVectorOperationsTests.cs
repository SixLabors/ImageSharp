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
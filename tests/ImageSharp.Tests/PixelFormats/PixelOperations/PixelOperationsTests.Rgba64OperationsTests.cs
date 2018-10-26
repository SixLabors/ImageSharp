using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class Rgba64OperationsTests : PixelOperationsTests<Rgba64>
        {
            public Rgba64OperationsTests(ITestOutputHelper output)
                : base(output)
            {
            }

            [Fact]
            public void IsSpecialImplementation() => Assert.IsType<Rgba64.PixelOperations>(PixelOperations<Rgba64>.Instance);
        }
    }
}
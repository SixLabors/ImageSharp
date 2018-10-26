using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class Bgra32OperationsTests : PixelOperationsTests<Bgra32>
        {
            public Bgra32OperationsTests(ITestOutputHelper output)
                : base(output)
            {
            }

            [Fact]
            public void IsSpecialImplementation() => Assert.IsType<Bgra32.PixelOperations>(PixelOperations<Bgra32>.Instance);
        }
    }
}
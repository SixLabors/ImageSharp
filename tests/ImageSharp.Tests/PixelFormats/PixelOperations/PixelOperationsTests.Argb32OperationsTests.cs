using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class Argb32OperationsTests : PixelOperationsTests<Argb32>
        {
            
            public Argb32OperationsTests(ITestOutputHelper output)
                : base(output)
            {
            }

            [Fact]
            public void IsSpecialImplementation() => Assert.IsType<Argb32.PixelOperations>(PixelOperations<Argb32>.Instance);
        }
    }
}
using System.IO;

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [GroupOutput("Foo")]
    public class GroupOutputTests
    {
        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32)]
        public void OutputSubfolderName_ValueIsTakeFromGroupOutputAttribute<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.Equal("Foo", provider.Utility.OutputSubfolderName);
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32)]
        public void GetTestOutputDir_ShouldDefineSubfolder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            string expected = $"{Path.DirectorySeparatorChar}Foo{Path.DirectorySeparatorChar}";
            Assert.Contains(expected, provider.Utility.GetTestOutputDir());
        }
    }
}
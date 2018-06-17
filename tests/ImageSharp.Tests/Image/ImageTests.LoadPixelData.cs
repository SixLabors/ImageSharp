namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.PixelFormats;

    using Xunit;

    public partial class ImageTests
    {
        public class LoadPixelData
        {
            [Fact]
            public void LoadFromPixelData_Bytes()
            {
                var img = Image.LoadPixelData<Rgba32>(new byte[] {
                                                                         0,0,0,255, // 0,0
                                                                         255,255,255,255, // 0,1
                                                                         255,255,255,255, // 1,0
                                                                         0,0,0,255, // 1,1
                                                                     }, 2, 2);

                Assert.NotNull(img);
                Assert.Equal(Rgba32.Black, img[0, 0]);
                Assert.Equal(Rgba32.White, img[0, 1]);

                Assert.Equal(Rgba32.White, img[1, 0]);
                Assert.Equal(Rgba32.Black, img[1, 1]);
            }

        }
    }
}
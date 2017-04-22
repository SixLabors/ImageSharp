
namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using ImageSharp.PixelFormats;

    public static class TestImageExtensions
    {
        public static void DebugSave<TPixel>(this Image<TPixel> img, TestImageProvider<TPixel> provider, string extension = "png")
                        where TPixel : struct, IPixel<TPixel>
        {
            if(!bool.TryParse(Environment.GetEnvironmentVariable("CI"), out bool isCI) || !isCI)
            {
                // we are running locally then we want to save it out
                provider.Utility.SaveTestOutputFile(img, extension);
            }
        }
    }
}

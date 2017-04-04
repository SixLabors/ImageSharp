
namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class TestImageExtensions
    {
        public static void DebugSave<TColor>(this Image<TColor> img, TestImageProvider<TColor> provider, string extension = "png")
                        where TColor : struct, IPixel<TColor>
        {
            if(!bool.TryParse(Environment.GetEnvironmentVariable("CI"), out bool isCI) || !isCI)
            {
                // we are running locally then we want to save it out
                provider.Utility.SaveTestOutputFile(img, extension);
            }
        }
    }
}

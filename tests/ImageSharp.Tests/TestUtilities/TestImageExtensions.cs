
namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    using ImageSharp.PixelFormats;

    public static class TestImageExtensions
    {
        public static void DebugSave<TPixel>(this Image<TPixel> img, ITestImageProvider provider, object settings = null, string extension = "png")
                        where TPixel : struct, IPixel<TPixel>
        {
            string tag = null;
            if (settings is string)
            {
                tag = (string)settings;
            }
            else if (settings != null)
            {
                var properties = settings.GetType().GetRuntimeProperties();

                tag = string.Join("_", properties.ToDictionary(x => x.Name, x => x.GetValue(settings)).Select(x => $"{x.Key}-{x.Value}"));
            }
            if(!bool.TryParse(Environment.GetEnvironmentVariable("CI"), out bool isCI) || !isCI)
            {
                // we are running locally then we want to save it out
                provider.Utility.SaveTestOutputFile(img, extension, tag: tag);
            }
        }
    }
}

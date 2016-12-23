using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests
{
    public class JpegTests
    {
        
        public const string TestOutputDirectory = "TestOutput/Jpeg";

        private ITestOutputHelper Output { get; }

        public JpegTests(ITestOutputHelper output)
        {
            this.Output = output;
        }
        public static IEnumerable<string> AllJpegFiles => TestImages.Jpeg.All;

        // TODO: Turned off PixelTypes.All to be CI-friendly, what should be the practice?
        [Theory]
        //[WithFileCollection(nameof(AllJpegFiles), PixelTypes.All)] 
        [WithFileCollection(nameof(AllJpegFiles), PixelTypes.Color | PixelTypes.ColorWithDefaultImageClass | PixelTypes.Argb)]
        public void OpenJpeg_SaveBmp<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var image = provider.GetImage();

            provider.Utility.SaveTestOutputFile(image, "bmp");
        }


        public static IEnumerable<string> AllBmpFiles => TestImages.Bmp.All;

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.ColorWithDefaultImageClass | PixelTypes.Argb, JpegSubsample.Ratio420, 75)]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.ColorWithDefaultImageClass | PixelTypes.Argb, JpegSubsample.Ratio444, 75)]
        public void OpenBmp_SaveJpeg<TColor>(TestImageProvider<TColor> provider, JpegSubsample subSample, int quality)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var image = provider.GetImage();

            var utility = provider.Utility;
            utility.TestName += "_" + subSample + "_Q" + quality;

            using (var outputStream = File.OpenWrite(utility.GetTestOutputFileName("jpg")))
            {
                var encoder = new JpegEncoder()
                {
                    Subsample = subSample,
                    Quality = quality
                };

                image.Save(outputStream, encoder);
            }
        }
    }
}
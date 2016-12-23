using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests.Formats.Jpg
{
    using ImageSharp.Tests.TestUtilities;

    public class JpegTests
    {
        
        public const string TestOutputDirectory = "TestOutput/Jpeg";

        private ITestOutputHelper Output { get; }

        public JpegTests(ITestOutputHelper output)
        {
            Output = output;
        }
        public static IEnumerable<string> AllJpegFiles => TestImages.Jpeg.All;

        // TODO: Turned off PixelTypes.All to be CI-friendly, what should be the practice?
        [Theory]
        //[WithFileCollection(nameof(AllJpegFiles), PixelTypes.All)] 
        [WithFileCollection(nameof(AllJpegFiles), PixelTypes.Color | PixelTypes.Argb)]
        public void OpenJpeg_SaveBmp<TColor>(TestImageFactory<TColor> factory)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var image = factory.Create();

            factory.Utility.SaveTestOutputFile(image, "bmp");
        }


        public static IEnumerable<string> AllBmpFiles => TestImages.Bmp.All;

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.Argb, JpegSubsample.Ratio420, 75)]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.Argb, JpegSubsample.Ratio444, 75)]
        public void OpenBmp_SaveJpeg<TColor>(TestImageFactory<TColor> factory, JpegSubsample subSample, int quality)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var image = factory.Create();

            var utility = factory.Utility;
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
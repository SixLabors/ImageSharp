using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests.Formats.Jpg
{
    using ImageSharp.Tests.TestUtilities;

    public class JpegTests
    {
        private ITestOutputHelper Output { get; }

        public JpegTests(ITestOutputHelper output)
        {
            this.Output = output;
        }
        
        public static IEnumerable<string> AllJpegFiles => TestImages.Jpeg.All;

        [Theory]
        [WithFileCollection(nameof(AllJpegFiles), PixelTypes.All)] // TODO: Turned off to be kind to AppVeyor, should I re-enable?
        //[WithFileCollection(nameof(AllJpegFiles), PixelTypes.Color | PixelTypes.Argb)]
        public void OpenJpeg_SaveBmp<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var image = factory.Create();
            
            factory.Utility.SaveTestOutputFile(image, "bmp");
        }
        

        public static IEnumerable<string> AllBmpFiles => TestImages.Bmp.All;

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.Argb, JpegSubsample.Ratio420, 75)]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.Argb, JpegSubsample.Ratio444, 75)]
        public void OpenBmp_SaveJpeg<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory, JpegSubsample subSample, int quality)
           where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var image = factory.Create();

            var utility = factory.Utility;
            utility.TestName += "_"+subSample + "_Q" + quality;

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
        
        private static void AssertSamePixels(PixelArea<Color, uint> data, int x1, int y1, int x2, int y2)
        {
            int idx1 = data.RowByteCount * y1 + x1*3;
            byte r1 = data.Bytes[idx1];
            byte g1 = data.Bytes[idx1+1];

            int idx2 = data.RowByteCount * y2 + x2*3;
            byte r2 = data.Bytes[idx2];
            byte g2 = data.Bytes[idx2 + 1];

            Assert.Equal(r1, r2);
            Assert.Equal(g1, g2);
        }
    }
}
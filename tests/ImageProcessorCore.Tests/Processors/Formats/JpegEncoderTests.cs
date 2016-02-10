namespace ImageProcessorCore.Tests
{
    using System.IO;
    using System.Linq;

    using Formats;

    using ImageProcessorCore.IO;

    using Xunit;

    public class JpegEncoderTests
    {
        [Fact]
        public void ForwardDCTTransformsCorrectly()
        {
            // Values taken from.
            // https://en.wikipedia.org/wiki/JPEG#Discrete_cosine_transform
            float[] input =
            {
                52, 55, 61, 66, 70, 61, 64, 73,
                63, 59, 55, 90, 109, 85, 69, 72,
                62, 59, 68, 113, 144, 104, 66, 73,
                63, 58, 71, 122, 154, 106, 70, 69,
                67, 61, 68, 104, 126, 88, 68, 70,
                79, 65, 60, 70, 77, 68, 58, 75,
                85, 71, 64, 59, 55, 61, 65, 83,
                87, 79, 69, 68, 65, 76, 78, 94
            };

            FDCT fdct = new FDCT(50);
            float[] arr = fdct.FastFDCT(input);
            int[] actual = fdct.QuantizeBlock(arr, 0);

            int[] expected =
            {
                -26, -3, -6, 2, 2, -1, 0, 0,
                 0, -2, -4, 1, 1, 0, 0, 0,
                -3, 1, 5, -1, -1, 0, 0, 0,
                -3, 1, 2, -1, 0, 0, 0, 0,
                 1, 0, 0, 0, 0, 0, 0, 0,
                 0, 0, 0, 0, 0, 0, 0, 0,
                 0, 0, 0, 0, 0, 0, 0, 0,
                 0, 0, 0, 0, 0, 0, 0, 0
            };

            Assert.True(actual.SequenceEqual(expected));
        }
    }
}
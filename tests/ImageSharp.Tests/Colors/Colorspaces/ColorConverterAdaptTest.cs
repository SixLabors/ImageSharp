using ImageSharp.Colors.Conversion;
using ImageSharp.Colors.Conversion.Implementation;
using ImageSharp.Colors.Spaces;

namespace ImageSharp.Tests
{
    using System.Collections.Generic;
    using Xunit;

    public class ColorConverterAdaptTest
    {
        private static readonly IEqualityComparer<float> FloatComparer = new ApproximateFloatComparer(4);

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.5, 0.5, 0.5, 0.507233, 0.500000, 0.378943)]
        public void Adapt_CieXyz_D65_To_D50_XyzScaling(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // Arrange
            CieXyz input = new CieXyz(x1, y1, z1);
            CieXyz expectedOutput = new CieXyz(x2, y2, z2);
            ColorConverter converter = new ColorConverter
            {
                ChromaticAdaptation = new VonKriesChromaticAdaptation(LmsAdaptationMatrix.XYZScaling),
                WhitePoint = Illuminants.D50
            };

            // Action
            CieXyz output = converter.Adapt(input, Illuminants.D65);

            // Assert
            Assert.Equal(output.X, expectedOutput.X, FloatComparer);
            Assert.Equal(output.Y, expectedOutput.Y, FloatComparer);
            Assert.Equal(output.Z, expectedOutput.Z, FloatComparer);
        }
    }
}
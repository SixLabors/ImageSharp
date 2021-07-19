// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Normalization
{
    // ReSharper disable InconsistentNaming
    public class AutoLevelTests
    {
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0456F);

        [Fact]
        public void AutoLevel_WhenL16_StretchesBetweenMinAndMax()
        {
            // Arrange
            ushort[] pixels = new ushort[]
            {
                100,  120,  140,  160,  180, 200,
            };

            ushort step = (ushort)(ushort.MaxValue / (pixels.Length - 1));

            using var image = new Image<L16>(pixels.Length, 1);
            for (int x = 0; x < pixels.Length; x++)
            {
                image[x, 0] = new L16(pixels[x]);
            }

            ushort[] expected =
                Enumerable.Range(0, pixels.Length)
                .Select(e => (ushort)(e * step))
                .ToArray();

            // Act
            image.Mutate(x => x.AutoLevel());

            ushort[] actual = image.GetPixelRowSpan(0).ToArray().Select(e => e.PackedValue).ToArray();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AutoLevel_WhenL8_StretchesBetweenMinAndMax()
        {
            // Arrange
            byte[] pixels = new byte[]
            {
                100,  120,  140,  160,  180, 200,
            };

            byte step = (byte)(byte.MaxValue / (pixels.Length - 1));

            using var image = new Image<L8>(pixels.Length, 1);
            for (int x = 0; x < pixels.Length; x++)
            {
                image[x, 0] = new L8(pixels[x]);
            }

            byte[] expected =
               Enumerable.Range(0, pixels.Length)
               .Select(e => (byte)(e * step))
               .ToArray();

            // Act
            image.Mutate(x => x.AutoLevel());

            byte[] actual = image.GetPixelRowSpan(0).ToArray().Select(e => e.PackedValue).ToArray();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AutoLevel_WhenTwoRows_StretchesBetweenMinAndMax()
        {
            // Arrange
            byte[] pixels = new byte[]
            {
                100, 200
            };

            using var image = new Image<L8>(1, 2);
            image[0, 0] = new L8(pixels[0]);
            image[0, 1] = new L8(pixels[1]);

            // Act
            image.Mutate(x => x.AutoLevel());

            // Assert
            Assert.Equal(0, image[0, 0].PackedValue);
            Assert.Equal(byte.MaxValue, image[0, 1].PackedValue);
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.HistogramEqImage, PixelTypes.L8)]
        public void AutoLevel_CompareToReferenceOutput<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.Mutate(x => x.AutoLevel());
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ValidatorComparer, provider);
        }
    }
}

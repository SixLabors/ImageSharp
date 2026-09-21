// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1ImagePyramidTests
{
    [Theory]
    [InlineData(64, 64, 3)]
    [InlineData(512, 512, 6)]
    [InlineData(48, 24, 1)]
    [InlineData(16, 16, 1)]
    [InlineData(1920, 1080, 7)]
    public void LevelCountFollowsTheShorterSide(int width, int height, int expected)
        => Assert.Equal(expected, Av1ImagePyramid.GetMaximumLevelCount(width, height));

    [Theory]
    [InlineData(64, 64)]
    [InlineData(512, 512)]
    [InlineData(128, 96)]
    public void LevelGeometryHalvesEachExtent(int width, int height)
    {
        using Av1ImagePyramid pyramid = new(Configuration.Default.MemoryAllocator, width, height);
        for (int level = 0; level < pyramid.LevelCount; level++)
        {
            Av1ImagePyramid.Level geometry = pyramid.GetLevel(level);
            Assert.Equal(width >> level, geometry.Width);
            Assert.Equal(height >> level, geometry.Height);

            // The reference aligns the stride so that the first coded sample of every row shares the
            // alignment of the level, which means the stride is a multiple of the alignment.
            Assert.Equal(0, geometry.Stride % 32);
            Assert.True(geometry.Stride >= geometry.Width + (2 * Av1ImagePyramid.Padding));
        }
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(128, 96)]
    public void FillProducesHalvedLevelsWithReplicatedBorders(int width, int height)
    {
        int sourceStride = width + 7;
        byte[] source = new byte[sourceStride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                source[(y * sourceStride) + x] = (byte)(((x * 29) + (y * 71) + ((x * y) * 13)) & byte.MaxValue);
            }
        }

        using Av1ImagePyramid pyramid = new(Configuration.Default.MemoryAllocator, width, height);
        int filled = pyramid.Fill(source, sourceStride, pyramid.LevelCount);
        Assert.Equal(pyramid.LevelCount, filled);
        Assert.Equal(pyramid.LevelCount, pyramid.FilledLevelCount);

        // The first level holds the frame itself.
        Av1ImagePyramid.Level first = pyramid.GetLevel(0);
        Span<byte> firstSamples = pyramid.GetSamples(0);
        for (int y = 0; y < height; y++)
        {
            Assert.Equal(
                source.AsSpan(y * sourceStride, width).ToArray(),
                firstSamples.Slice(first.Origin + (y * first.Stride), width).ToArray());
        }

        // Every level repeats its end samples into its border on all four sides.
        for (int level = 0; level < pyramid.LevelCount; level++)
        {
            Av1ImagePyramid.Level geometry = pyramid.GetLevel(level);
            Span<byte> samples = pyramid.GetSamples(level);
            for (int y = 0; y < geometry.Height; y++)
            {
                int row = geometry.Origin + (y * geometry.Stride);
                for (int pad = 1; pad <= Av1ImagePyramid.Padding; pad++)
                {
                    Assert.Equal(samples[row], samples[row - pad]);
                    Assert.Equal(samples[row + geometry.Width - 1], samples[row + geometry.Width - 1 + pad]);
                }
            }

            int firstRow = geometry.Origin;
            int lastRow = geometry.Origin + ((geometry.Height - 1) * geometry.Stride);
            for (int pad = 1; pad <= Av1ImagePyramid.Padding; pad++)
            {
                for (int x = -Av1ImagePyramid.Padding; x < geometry.Width + Av1ImagePyramid.Padding; x++)
                {
                    Assert.Equal(samples[firstRow + x], samples[firstRow + x - (pad * geometry.Stride)]);
                    Assert.Equal(samples[lastRow + x], samples[lastRow + x + (pad * geometry.Stride)]);
                }
            }
        }

        // Each level below the first is the halved form of the level above it.
        for (int level = 1; level < pyramid.LevelCount; level++)
        {
            Av1ImagePyramid.Level previous = pyramid.GetLevel(level - 1);
            Av1ImagePyramid.Level current = pyramid.GetLevel(level);
            byte[] expected = new byte[current.Stride * current.Height];
            Av1PlaneDownsamplerOracle.Halve(
                pyramid.GetSamples(level - 1)[previous.Origin..],
                previous.Stride,
                current.Width << 1,
                current.Height << 1,
                expected,
                current.Stride);

            Span<byte> samples = pyramid.GetSamples(level)[current.Origin..];
            for (int y = 0; y < current.Height; y++)
            {
                Assert.Equal(
                    expected.AsSpan(y * current.Stride, current.Width).ToArray(),
                    samples.Slice(y * current.Stride, current.Width).ToArray());
            }
        }
    }

    [Fact]
    public void FillIsIdempotentForAlreadyFilledLevels()
    {
        const int Width = 64;
        const int Height = 64;
        byte[] source = new byte[Width * Height];
        for (int index = 0; index < source.Length; index++)
        {
            source[index] = (byte)(index & byte.MaxValue);
        }

        using Av1ImagePyramid pyramid = new(Configuration.Default.MemoryAllocator, Width, Height);
        Assert.Equal(2, pyramid.Fill(source, Width, 2));

        Av1ImagePyramid.Level second = pyramid.GetLevel(1);
        byte[] afterTwo = pyramid.GetSamples(1).Slice(second.Origin, second.Stride * second.Height).ToArray();

        // A second request for the same count must not rebuild what is already there.
        Assert.Equal(2, pyramid.Fill(source, Width, 2));
        Assert.Equal(afterTwo, pyramid.GetSamples(1).Slice(second.Origin, afterTwo.Length).ToArray());

        // A larger request extends the pyramid without disturbing the filled levels.
        Assert.Equal(3, pyramid.Fill(source, Width, 3));
        Assert.Equal(afterTwo, pyramid.GetSamples(1).Slice(second.Origin, afterTwo.Length).ToArray());
    }
}

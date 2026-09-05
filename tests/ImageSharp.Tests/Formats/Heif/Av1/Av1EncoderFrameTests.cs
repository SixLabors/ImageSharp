// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

public class Av1EncoderFrameTests
{
    private const int EightBit = (int)Av1BitDepth.EightBit;
    private const int TenBit = (int)Av1BitDepth.TenBit;
    private const int TwelveBit = (int)Av1BitDepth.TwelveBit;
    private const int Yuv400 = (int)Av1ColorFormat.Yuv400;
    private const int Yuv420 = (int)Av1ColorFormat.Yuv420;
    private const int Yuv422 = (int)Av1ColorFormat.Yuv422;
    private const int Yuv444 = (int)Av1ColorFormat.Yuv444;

    [Theory]
    [InlineData(EightBit, false, false)]
    [InlineData(EightBit, false, true)]
    [InlineData(EightBit, true, false)]
    [InlineData(EightBit, true, true)]
    [InlineData(TenBit, false, false)]
    [InlineData(TenBit, false, true)]
    [InlineData(TenBit, true, false)]
    [InlineData(TenBit, true, true)]
    [InlineData(TwelveBit, false, false)]
    [InlineData(TwelveBit, false, true)]
    [InlineData(TwelveBit, true, false)]
    [InlineData(TwelveBit, true, true)]
    public void RectangularIntraReferencesExtendTheLastAvailableSample(int bitDepthValue, bool transpose, bool extensionAvailable)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        if (bitDepth == Av1BitDepth.EightBit)
        {
            AssertRectangularIntraReferences<byte, Av1IntraSuperblockEncoder.ByteOperator>(bitDepth, transpose, extensionAvailable);
        }
        else
        {
            AssertRectangularIntraReferences<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(bitDepth, transpose, extensionAvailable);
        }
    }

    private static void AssertRectangularIntraReferences<TSample, TOperator>(
        Av1BitDepth bitDepth,
        bool transpose,
        bool extensionAvailable)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        int width = transpose ? 16 : 4;
        int height = transpose ? 4 : 16;
        int scale = 1 << (bitDepth.GetBitCount() - 8);
        using Buffer2D<TSample> plane = Configuration.Default.MemoryAllocator.Allocate2D<TSample>(33, 33);
        for (int i = 0; i < 32; i++)
        {
            plane.DangerousGetRowSpan(0)[i + 1] = TOperator.CreateSample((10 + i) * scale);
            plane.DangerousGetRowSpan(i + 1)[0] = TOperator.CreateSample((50 + i) * scale);
        }

        plane.DangerousGetRowSpan(0)[0] = TOperator.CreateSample(100 * scale);

        // Native reconintra.c extends a four-sample edge through its four-sample neighbor, then
        // repeats sample seven to cover the twenty samples required by a 4x16 directional ray.
        // These explicit offsets also distinguish unavailable neighbors from available extension.
        int[] shortEdge = extensionAvailable
            ? [0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7]
            : [0, 1, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3];

        int[] longEdge = extensionAvailable
            ? [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19]
            : [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 15, 15, 15, 15];

        int[] expectedAbove = transpose ? longEdge : shortEdge;
        int[] expectedLeft = transpose ? shortEdge : longEdge;
        TSample poison = TOperator.CreateSample((1 << bitDepth.GetBitCount()) - 1);
        TSample[] above = new TSample[23];
        TSample[] left = new TSample[23];
        above.AsSpan().Fill(poison);
        left.AsSpan().Fill(poison);

        // The exact-sized interior includes the corner and twenty projected samples. Sentinel samples
        // on either side detect writes outside the reference view, including the former 2*long-edge span.
        Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator>.PrepareReferenceSamples(
            plane.GetRegion(),
            new Point(1, 1),
            width,
            height,
            true,
            true,
            extensionAvailable,
            extensionAvailable,
            bitDepth,
            above.AsSpan(1, 21),
            left.AsSpan(1, 21));

        Assert.Equal(poison, above[0]);
        Assert.Equal(poison, left[0]);
        Assert.Equal(poison, above[^1]);
        Assert.Equal(poison, left[^1]);
        Assert.Equal(TOperator.CreateSample(100 * scale), above[1]);
        Assert.Equal(TOperator.CreateSample(100 * scale), left[1]);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(TOperator.CreateSample((10 + expectedAbove[i]) * scale), above[i + 2]);
            Assert.Equal(TOperator.CreateSample((50 + expectedLeft[i]) * scale), left[i + 2]);
        }
    }

    [Fact]
    public void EncodeUsesMultipleTilesWhenSingleTileWidthLimitIsExceeded()
    {
        const int Width = Av1Constants.MaxTileWidth + 1;
        const int SuperblockSize = 1 << (Av1Constants.MaxSuperBlockSizeLog2 - 1);
        const int Height = SuperblockSize;
        int superblockColumns = (Width + SuperblockSize - 1) / SuperblockSize;
        int secondTileStart = ((superblockColumns + 1) / 2) * SuperblockSize;
        using Image<L8> source = new(Width, Height, new L8(128));
        source[0, 0] = new L8(1);
        source[secondTileStart - 1, 0] = new L8(17);
        source[secondTileStart, 0] = new L8(241);
        source[Width - 1, Height - 1] = new L8(255);
        using MemoryStream stream = new();
        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400);

        Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 0,
            effort: 0);

        using Av1Decoder decoder = new(Configuration.Default);
        using Image<L8> decoded = decoder.Decode<L8>(stream.ToArray());

        Assert.Equal(2, decoder.FrameHeader.TilesInfo.TileColumnCount);
        Assert.Equal(1, decoder.FrameHeader.TilesInfo.TileRowCount);
        Assert.Equal(source.Size, decoded.Size);
        Assert.Equal(source[0, 0], decoded[0, 0]);
        Assert.Equal(source[secondTileStart - 1, 0], decoded[secondTileStart - 1, 0]);
        Assert.Equal(source[secondTileStart, 0], decoded[secondTileStart, 0]);
        Assert.Equal(source[Width - 1, Height - 1], decoded[Width - 1, Height - 1]);
    }

    [Theory]
    [InlineData(8, 8, false, EightBit, Yuv400)]
    [InlineData(8, 8, true, EightBit, Yuv400)]
    [InlineData(16, 16, false, EightBit, Yuv400)]
    [InlineData(16, 16, true, EightBit, Yuv400)]
    [InlineData(8, 8, false, TenBit, Yuv400)]
    [InlineData(8, 8, true, TenBit, Yuv400)]
    [InlineData(8, 8, false, TwelveBit, Yuv400)]
    [InlineData(8, 8, true, TwelveBit, Yuv400)]
    [InlineData(16, 16, false, EightBit, Yuv420)]
    [InlineData(16, 16, true, EightBit, Yuv420)]
    [InlineData(13, 11, true, EightBit, Yuv420)]
    [InlineData(16, 16, false, TenBit, Yuv420)]
    [InlineData(16, 16, true, TenBit, Yuv420)]
    [InlineData(16, 16, false, TwelveBit, Yuv420)]
    [InlineData(16, 16, true, TwelveBit, Yuv420)]
    [InlineData(16, 16, false, EightBit, Yuv422)]
    [InlineData(16, 16, true, EightBit, Yuv422)]
    [InlineData(13, 11, true, EightBit, Yuv422)]
    [InlineData(16, 16, false, TenBit, Yuv422)]
    [InlineData(16, 16, true, TenBit, Yuv422)]
    [InlineData(16, 16, false, TwelveBit, Yuv422)]
    [InlineData(16, 16, true, TwelveBit, Yuv422)]
    [InlineData(16, 16, false, EightBit, Yuv444)]
    [InlineData(16, 16, true, EightBit, Yuv444)]
    [InlineData(13, 11, true, EightBit, Yuv444)]
    [InlineData(16, 16, false, TenBit, Yuv444)]
    [InlineData(16, 16, true, TenBit, Yuv444)]
    [InlineData(16, 16, false, TwelveBit, Yuv444)]
    [InlineData(16, 16, true, TwelveBit, Yuv444)]
    public void EncodeWritesReducedStillPictureConsumedByProductionDecoder(int width, int height, bool hasGradient, int bitDepthValue, int colorFormatValue)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        using Image<Rgba32> source = new(width, height);
        for (int y = 0; y < height; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                if (colorFormat == Av1ColorFormat.Yuv400)
                {
                    byte value = hasGradient ? (byte)((x * 13) + (y * 17)) : (byte)128;
                    row[x] = new Rgba32(value, value, value);
                }
                else
                {
                    byte red = hasGradient ? (byte)((x * 13) + (y * 17)) : (byte)192;
                    byte green = hasGradient ? (byte)((x * 7) + (y * 5)) : (byte)64;
                    byte blue = hasGradient ? (byte)((x * 3) + (y * 11)) : (byte)32;
                    row[x] = new Rgba32(red, green, blue);
                }
            }
        }

        ObuColorConfig colorConfig = CreateColorConfig(bitDepth, colorFormat);
        using MemoryStream stream = new();
        ObuSequenceHeader encodedHeader = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 37,
            effort: 5);

        byte[] payload = stream.ToArray();
        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        string contentName = hasGradient ? "gradient" : "constant";
        int bitCount = bitDepth.GetBitCount();
        string colorName = colorFormat.ToString()[3..];
        string fileName = $"encoder-frame-{width}x{height}-{bitCount}b-{colorName}-{contentName}.obu";
        File.WriteAllBytes(Path.Combine(outputDirectory, fileName), payload);

        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);

        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
        ObuSequenceProfile expectedProfile = bitDepth == Av1BitDepth.TwelveBit || colorFormat == Av1ColorFormat.Yuv422
            ? ObuSequenceProfile.Professional
            : colorFormat == Av1ColorFormat.Yuv444
                ? ObuSequenceProfile.High
                : ObuSequenceProfile.Main;

        Assert.Equal(expectedProfile, encodedHeader.SequenceProfile);
        Assert.True(encodedHeader.IsReducedStillPictureHeader);

        Rgba32 first = decoded[0, 0];
        Assert.Equal(byte.MaxValue, first.A);
        if (colorFormat == Av1ColorFormat.Yuv400)
        {
            Assert.Equal(first.R, first.G);
            Assert.Equal(first.R, first.B);
        }
        else
        {
            Rgba32 center = decoded[width / 2, height / 2];
            Assert.True(center.R != center.G || center.G != center.B);
        }

        if (hasGradient)
        {
            Assert.NotEqual(first, decoded[width - 1, height - 1]);
        }
        else
        {
            if (colorFormat == Av1ColorFormat.Yuv400)
            {
                Assert.InRange(first.R, 120, 136);

                for (int y = 0; y < height; y++)
                {
                    foreach (Rgba32 pixel in decoded.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y))
                    {
                        Assert.Equal(first, pixel);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EncodeSequenceFrameWritesNonReducedHeaderConsumedByProductionDecoder(bool encodeAlpha)
    {
        const int Width = 16;
        const int Height = 16;
        using Image<Rgba32> source = new(Width, Height, new Rgba32(48, 96, 192));
        using MemoryStream stream = new();
        ObuColorConfig colorConfig = encodeAlpha
            ? CreateColorConfig(Av1BitDepth.EightBit)
            : CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420);

        using Av1FrameEncoder.SequenceEncoder encoder = encodeAlpha
            ? Av1FrameEncoder.CreateAlphaSequenceEncoder(
                Configuration.Default,
                Width,
                Height,
                colorConfig,
                qIndex: 37,
                effort: 5)
            : Av1FrameEncoder.CreateColorSequenceEncoder(
                Configuration.Default,
                Width,
                Height,
                colorConfig,
                qIndex: 37,
                effort: 5);

        encoder.EncodeKeyFrame(source.Frames.RootFrame, stream);
        ObuSequenceHeader encodedHeader = encoder.SequenceHeader;
        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        ObuSequenceHeader decodedHeader = decoder.SequenceHeader;

        Assert.False(encodedHeader.IsStillPicture);
        Assert.False(encodedHeader.IsReducedStillPictureHeader);
        Assert.Equal(encodeAlpha, encodedHeader.ColorConfig.IsMonochrome);
        Assert.NotNull(decodedHeader);
        Assert.False(decodedHeader.IsStillPicture);
        Assert.False(decodedHeader.IsReducedStillPictureHeader);
        Assert.Equal(new Size(Width, Height), decoded.Size);
    }

    /// <summary>
    /// Verifies dependent color samples with odd visible dimensions and motion across subsampled chroma phases.
    /// </summary>
    [Theory]
    [InlineData(EightBit, Yuv420, 8)]
    [InlineData(TenBit, Yuv420, 8)]
    [InlineData(TwelveBit, Yuv420, 8)]
    [InlineData(EightBit, Yuv420, 9)]
    [InlineData(TenBit, Yuv420, 9)]
    [InlineData(TwelveBit, Yuv420, 9)]
    [InlineData(EightBit, Yuv422, 9)]
    [InlineData(TenBit, Yuv422, 9)]
    [InlineData(TwelveBit, Yuv422, 9)]
    [InlineData(EightBit, Yuv444, 9)]
    [InlineData(TenBit, Yuv444, 9)]
    [InlineData(TwelveBit, Yuv444, 9)]
    public void SequenceEncoderPreservesNativeColorPlanesWithSubpixelMotion(int bitDepthValue, int colorFormatValue, int effort)
    {
        const int Width = 23;
        const int Height = 19;
        const int QIndex = 17;
        const int ByteToUInt16Scale = ushort.MaxValue / byte.MaxValue;
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuColorConfig colorConfig = CreateColorConfig(bitDepth, colorFormat);
        ReadOnlySpan<int> period = [0, 28, 40, 28, 0, -28, -40, -12];
        using Image<Rgb48> source = new(Width, Height);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default, Width, Height, colorConfig, QIndex, effort);

        string outputDirectory = TestEnvironment.CreateOutputDirectory("Heif", "Av1", nameof(this.SequenceEncoderPreservesNativeColorPlanesWithSubpixelMotion));
        string outputName = $"{bitDepth.GetBitCount()}-{colorFormat}-effort{effort}";
        using FileStream output = File.Create(Path.Combine(outputDirectory, outputName + ".obu"));
        using BinaryWriter rawOutput = new(File.Create(Path.Combine(outputDirectory, outputName + ".managed.yuv")));
        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        for (int frameIndex = 0; frameIndex < 2; frameIndex++)
        {
            // The second source translates all three channels by one luma sample on each axis. Chroma is
            // converted independently by the production converter, so 4:2:0 and 4:2:2 cannot hide behind
            // constant neutral planes. Odd dimensions also exercise each plane's visible-edge clipping.
            for (int y = 0; y < Height; y++)
            {
                Span<Rgb48> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                int referenceY = Math.Min(y + frameIndex, Height - 1);
                for (int x = 0; x < Width; x++)
                {
                    int referenceX = Math.Min(x + frameIndex, Width - 1);
                    row[x] = new Rgb48(
                        (ushort)((128 + period[referenceX % period.Length]) * ByteToUInt16Scale),
                        (ushort)((128 + period[referenceY % period.Length]) * ByteToUInt16Scale),
                        (ushort)((128 + period[(referenceX + referenceY) % period.Length]) * ByteToUInt16Scale));
                }
            }

            sample.SetLength(0);
            if (frameIndex == 0)
            {
                encoder.EncodeKeyFrame(source.Frames.RootFrame, sample);
            }
            else
            {
                encoder.EncodeInterFrame(source.Frames.RootFrame, sample);
            }

            sample.Position = 0;
            sample.CopyTo(output);
            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            Av1FrameBuffer<byte> decoded = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Assert.Equal(Width, decoded.Width);
            Assert.Equal(Height, decoded.Height);
            Assert.Equal(bitDepth, decoded.BitDepth);
            for (int planeIndex = 0; planeIndex < 3; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subsamplingX = plane == Av1Plane.Y || !colorConfig.SubSamplingX ? 0 : 1;
                int subsamplingY = plane == Av1Plane.Y || !colorConfig.SubSamplingY ? 0 : 1;
                int planeHeight = (Height + subsamplingY) >> subsamplingY;
                if (bitDepth == Av1BitDepth.EightBit)
                {
                    Buffer2DRegion<byte> planeSamples = decoded.DeriveBlockPointer(plane, subsamplingX, subsamplingY);
                    for (int y = 0; y < planeHeight; y++)
                    {
                        rawOutput.Write(planeSamples.DangerousGetRowSpan(y));
                    }
                }
                else
                {
                    for (int y = 0; y < planeHeight; y++)
                    {
                        foreach (ushort value in decoded.GetHighBitDepthRowSpan(plane, y, subsamplingX, subsamplingY))
                        {
                            // Raw high-bit-depth output uses explicit little-endian samples on every host.
                            rawOutput.Write(value);
                        }
                    }
                }
            }
        }

        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
        Assert.Equal(ObuFrameType.InterFrame, frameHeader.FrameType);
        Assert.Equal(Av1InterpolationFilter.Switchable, frameHeader.InterpolationFilter);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        bool hasMotion = false;
        bool hasFractionalChromaMotion = false;
        foreach (Av1BlockModeInfo mode in frameInfo.GetModeInfos(Point.Empty, frameInfo.GetModeInfoCount(Point.Empty)))
        {
            if (mode.ReferenceFrames[0] == Av1ReferenceFrameType.Last)
            {
                Av1MotionVector vector = mode.MotionVectors[0];
                hasMotion |= vector.Column != 0 || vector.Row != 0;

                // A subsampled chroma phase repeats every two luma pixels, or sixteen Q3 motion units.
                int chromaPhaseMask = (Av1MotionVector.SubpixelScale << 1) - 1;
                hasFractionalChromaMotion |=
                    (colorConfig.SubSamplingX && (vector.Column & chromaPhaseMask) != 0) ||
                    (colorConfig.SubSamplingY && (vector.Row & chromaPhaseMask) != 0);
            }
        }

        Assert.True(hasMotion);
        if (colorConfig.SubSamplingX || colorConfig.SubSamplingY)
        {
            Assert.True(hasFractionalChromaMotion);
        }
    }

    /// <summary>
    /// Verifies retained reference reconstruction and effort-dependent filter signaling through production sequence decoding.
    /// </summary>
    [Theory]
    [InlineData(5, false, false)]
    [InlineData(7, false, false)]
    [InlineData(8, true, false)]
    [InlineData(9, true, true)]
    public void SequenceEncoderUsesRetainedReconstructionForInterFrame(int effort, bool switchableFilters, bool dualFilters)
    {
        const int Width = 16;
        const int Height = 16;
        Rgba32 sourceColor = new(48, 96, 192);
        using Image<Rgba32> source = new(Width, Height, sourceColor);
        using MemoryStream firstSample = new();
        using MemoryStream secondSample = new();
        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            Width,
            Height,
            colorConfig,
            qIndex: 37,
            effort);

        encoder.EncodeKeyFrame(source.Frames.RootFrame, firstSample);
        encoder.EncodeInterFrame(source.Frames.RootFrame, secondSample);

        // Retain the exact two-sample elementary stream for independent reference-decoder acceptance.
        string outputDirectory = TestEnvironment.CreateOutputDirectory("Heif", "Av1", nameof(this.SequenceEncoderUsesRetainedReconstructionForInterFrame));
        using (FileStream output = File.Create(Path.Combine(outputDirectory, $"effort-{effort}.obu")))
        {
            firstSample.Position = 0;
            firstSample.CopyTo(output);
            secondSample.Position = 0;
            secondSample.CopyTo(output);
        }

        using Av1Decoder decoder = new(Configuration.Default);
        using ImageFrame<Rgba32> decodedFirst = decoder.DecodeSequenceFrame<Rgba32>(
            firstSample.ToArray(),
            null,
            null);

        using ImageFrame<Rgba32> decodedSecond = decoder.DecodeSequenceFrame<Rgba32>(
            secondSample.ToArray(),
            null,
            null);

        ObuFrameHeader frameHeader = decoder.FrameHeader;
        Assert.Equal(ObuFrameType.InterFrame, frameHeader.FrameType);
        Assert.False(frameHeader.SegmentationParameters.Enabled);
        Assert.False(frameHeader.AllowScreenContentTools);
        Assert.False(frameHeader.ForceIntegerMotionVector);
        Assert.Equal(effort >= 8, frameHeader.AllowHighPrecisionMotionVector);
        Assert.Equal(37, frameHeader.QuantizationParameters.BaseQIndex);
        Assert.Equal(switchableFilters ? Av1InterpolationFilter.Switchable : Av1InterpolationFilter.Regular, frameHeader.InterpolationFilter);
        Assert.Equal(dualFilters, decoder.SequenceHeader.EnableDualFilter);
        for (int y = 0; y < Height; y++)
        {
            Assert.Equal(
                decodedFirst.PixelBuffer.DangerousGetRowSpan(y),
                decodedSecond.PixelBuffer.DangerousGetRowSpan(y));
        }
    }

    [Fact]
    public void SequenceEncoderWritesSelectedGlobalTranslation()
    {
        const int Width = 64;
        const int Height = 64;
        const int HorizontalOffset = 4;
        using Image<Rgba32> first = new(Width, Height);
        using Image<Rgba32> second = new(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            Span<Rgba32> firstRow = first.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                byte value = (byte)(((x * 37) + (y * 53) + ((x * y) * 11)) & byte.MaxValue);
                firstRow[x] = new Rgba32(value, value, value);
            }
        }

        for (int y = 0; y < Height; y++)
        {
            ReadOnlySpan<Rgba32> firstRow = first.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<Rgba32> secondRow = second.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                secondRow[x] = firstRow[Math.Min(x + HorizontalOffset, Width - 1)];
            }
        }

        using MemoryStream firstSample = new();
        using MemoryStream secondSample = new();
        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            Width,
            Height,
            colorConfig,
            qIndex: 4,
            effort: 6);

        encoder.EncodeKeyFrame(first.Frames.RootFrame, firstSample);
        encoder.EncodeInterFrame(second.Frames.RootFrame, secondSample);

        using Av1Decoder decoder = new(Configuration.Default);
        using ImageFrame<Rgba32> decodedFirst = decoder.DecodeSequenceFrame<Rgba32>(
            firstSample.ToArray(),
            null,
            null);

        using ImageFrame<Rgba32> decodedSecond = decoder.DecodeSequenceFrame<Rgba32>(
            secondSample.ToArray(),
            null,
            null);

        ObuFrameHeader frameHeader = decoder.FrameHeader;
        Av1GlobalMotionParameters globalMotion = frameHeader.GetGlobalMotionParameters()[0];
        Av1MotionVector vector = globalMotion.GetMotionVector(
            frameHeader.AllowHighPrecisionMotionVector,
            Av1BlockSize.Block8x8,
            default,
            frameHeader.ForceIntegerMotionVector);

        Assert.Equal(Av1GlobalMotionType.RotationZoom, globalMotion.Type);
        Assert.False(frameHeader.AllowScreenContentTools);
        Assert.False(frameHeader.ForceIntegerMotionVector);
        Assert.Equal(0, vector.Row);
        Assert.Equal(HorizontalOffset * 8, vector.Column);
        Assert.Equal(first.Size, decodedFirst.Size);
        Assert.Equal(second.Size, decodedSecond.Size);
    }

    [Theory]
    [InlineData(false, EightBit)]
    [InlineData(false, TenBit)]
    [InlineData(false, TwelveBit)]
    [InlineData(true, EightBit)]
    [InlineData(true, TenBit)]
    [InlineData(true, TwelveBit)]
    public void SequenceEncoderConstructionFailureReturnsEveryAllocation(bool encodeAlpha, int bitDepthValue)
    {
        ObuColorConfig colorConfig = CreateColorConfig(
            (Av1BitDepth)bitDepthValue,
            encodeAlpha ? Av1ColorFormat.Yuv400 : Av1ColorFormat.Yuv420);

        Configuration configuration = Configuration.Default.Clone();
        TestMemoryAllocator successfulAllocator = new();
        successfulAllocator.EnableNonThreadSafeLogging();
        configuration.MemoryAllocator = successfulAllocator;
        using (Av1FrameEncoder.SequenceEncoder encoder = encodeAlpha
            ? Av1FrameEncoder.CreateAlphaSequenceEncoder(configuration, 32, 32, colorConfig, 17, 9)
            : Av1FrameEncoder.CreateColorSequenceEncoder(configuration, 32, 32, colorConfig, 17, 9))
        {
            Assert.NotEmpty(successfulAllocator.AllocationLog);
        }

        Assert.Equal(successfulAllocator.AllocationLog.Count, successfulAllocator.ReturnLog.Count);
        for (int failureIndex = 0; failureIndex < successfulAllocator.AllocationLog.Count; failureIndex++)
        {
            FailingSequenceAllocator allocator = new(failureIndex);
            configuration.MemoryAllocator = allocator;

            // Fail each real allocator request, including those made inside nested constructors. A constructor
            // that throws never reaches the caller's using statement, so its completed owners must unwind there.
            InvalidMemoryOperationException exception = Assert.Throws<InvalidMemoryOperationException>(() =>
            {
                using Av1FrameEncoder.SequenceEncoder encoder = encodeAlpha
                    ? Av1FrameEncoder.CreateAlphaSequenceEncoder(configuration, 32, 32, colorConfig, 17, 9)
                    : Av1FrameEncoder.CreateColorSequenceEncoder(configuration, 32, 32, colorConfig, 17, 9);
            });

            Assert.Equal("Sequence allocation failure.", exception.Message);
            Assert.Equal(failureIndex, allocator.AllocationLog.Count);
            Assert.All(
                allocator.AllocationLog,
                allocation => Assert.Single(allocator.ReturnLog, returned => returned.AllocationId == allocation.AllocationId));

            Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        }
    }

    [Theory]
    [InlineData(false, EightBit, Yuv420, 384)]
    [InlineData(false, TwelveBit, Yuv444, 288)]
    [InlineData(true, EightBit, Yuv400, 192)]
    [InlineData(true, TwelveBit, Yuv400, 192)]
    public void SequenceEncoderReusesAllocatorOwnedRowStorage(
        bool encodeAlpha,
        int bitDepthValue,
        int colorFormatValue,
        int expectedRowStorageLength)
    {
        const int Width = 64;
        const int Height = 64;
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        using Image<Rgba64> source = new(
            Width,
            Height,
            new Rgba64(ushort.MaxValue, 32768, 16384, 49152));

        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuColorConfig colorConfig = CreateColorConfig(bitDepth, colorFormat);
        TestMemoryAllocator.AllocationRequest rowStorage;
        int allocationCount;
        using (Av1FrameEncoder.SequenceEncoder encoder = encodeAlpha
            ? Av1FrameEncoder.CreateAlphaSequenceEncoder(
                configuration,
                Width,
                Height,
                colorConfig,
                qIndex: 37,
                effort: 6)
            : Av1FrameEncoder.CreateColorSequenceEncoder(
                configuration,
                Width,
                Height,
                colorConfig,
                qIndex: 37,
                effort: 6))
        {
            rowStorage = Assert.Single(
                allocator.AllocationLog,
                allocation => allocation.ElementType == typeof(float));

            allocationCount = allocator.AllocationLog.Count;
            using MemoryStream output = new(256 * 1024);
            encoder.EncodeKeyFrame(source.Frames.RootFrame, output);
            encoder.EncodeInterFrame(source.Frames.RootFrame, output);

            // Fixed sequence geometry lets libaom retain its frame-sized compressor data. The ImageSharp
            // sequence encoder must likewise perform every sample conversion and coding pass without another rent.
            Assert.Equal(allocationCount, allocator.AllocationLog.Count);
        }

        Assert.Equal(expectedRowStorageLength, rowStorage.Length);
        Assert.Contains(
            allocator.ReturnLog,
            returned => returned.AllocationId == rowStorage.AllocationId);
    }

    [Theory]
    [InlineData(TenBit, 8, 8, 0)]
    [InlineData(TwelveBit, 8, 8, 0)]
    [InlineData(TenBit, 24, 16, 9)]
    [InlineData(TwelveBit, 24, 16, 9)]
    [InlineData(TenBit, 16, 24, 10)]
    [InlineData(TwelveBit, 16, 24, 10)]
    public void LosslessHighBitDepthEncodingPreservesNativePlanes(int bitDepthValue, int width, int height, int effort)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        using Image<Rgb48> source = new(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<Rgb48> pixels = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < width; column++)
            {
                pixels[column] = new Rgb48(
                    (ushort)(((column * 7001) + (row * 997)) & ushort.MaxValue),
                    (ushort)(((row * 6007) + (column * 1231)) & ushort.MaxValue),
                    (ushort)(((column * 4001) + (row * 3001)) & ushort.MaxValue));
            }
        }

        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            ColorPrimaries = ObuColorPrimaries.Bt709,
            TransferCharacteristics = ObuTransferCharacteristics.Srgb,
            MatrixCoefficients = ObuMatrixCoefficients.Identity,
            ColorRange = true,
            BitDepth = bitDepth
        };

        using Av1EncoderFrameBuffer<ushort> expected = new(
            Configuration.Default,
            width,
            height,
            bitDepth.GetBitCount(),
            Av1ColorFormat.Yuv444,
            1,
            1);

        Av1FrameEncoder.PrepareSource(
            Configuration.Default,
            source.Frames.RootFrame,
            expected.Frame,
            colorConfig);

        using MemoryStream stream = new();
        Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 0,
            effort);

        byte[] payload = stream.ToArray();
        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        string outputName = $"encoder-frame-{width}x{height}-{bitDepth.GetBitCount()}b-444-lossless-effort{effort}";
        File.WriteAllBytes(
            Path.Combine(outputDirectory, outputName + ".obu"),
            payload);

        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> actual = decoder.DecodeFrameBuffer(payload, null, null, out _);
        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);

        Assert.Equal(width, actual.Width);
        Assert.Equal(height, actual.Height);
        Assert.True(frameHeader.CodedLossless);
        Assert.True(frameHeader.AllLossless);
        Assert.Equal(Av1TransformMode.Only4x4, frameHeader.TransformMode);

        // Lossless native planes are the oracle for external decoding, not the packed RGB conversion on return.
        // UInt16 raw samples are explicitly little-endian even when these tests run on a different host byte order.
        using BinaryWriter rawOutput = new(File.Create(Path.Combine(outputDirectory, outputName + ".source.yuv")));
        foreach (Av1Plane plane in new[] { Av1Plane.Y, Av1Plane.U, Av1Plane.V })
        {
            Buffer2DRegion<ushort> expectedPlane = expected.Frame.View.GetPlane(plane);
            for (int row = 0; row < height; row++)
            {
                ReadOnlySpan<ushort> expectedRow = expectedPlane.DangerousGetRowSpan(row);
                Assert.Equal(expectedRow, actual.GetHighBitDepthRowSpan(plane, row, 0, 0));
                foreach (ushort sample in expectedRow)
                {
                    rawOutput.Write(sample);
                }
            }
        }
    }

    /// <summary>
    /// Verifies that live partition search preserves lossless syntax across clipped parent nodes and superblocks.
    /// </summary>
    [Theory]
    [InlineData(48, 24, 9)]
    [InlineData(24, 48, 9)]
    [InlineData(80, 24, 9)]
    [InlineData(24, 80, 9)]
    [InlineData(96, 24, 10)]
    [InlineData(24, 96, 10)]
    public void EncodeLosslessPartitionSearchAcrossClippedSuperblocks(int width, int height, int effort)
    {
        ReadOnlySpan<int> period = [0, 28, 40, 28, 0, -28, -40, -12];
        using Image<L8> source = new(width, height);
        for (int y = 0; y < height; y++)
        {
            Span<L8> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                row[x] = new L8((byte)(128 + period[x % period.Length] + period[y % period.Length]));
            }
        }

        // The repeated surface favors larger early leaves. Later clipped parents must still split from their
        // own geometry instead of reading a stale position in the original fixed-eight partition preorder.
        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400);
        colorConfig.ColorRange = true;
        using MemoryStream stream = new();
        Av1FrameEncoder.Encode(Configuration.Default, source.Frames.RootFrame, stream, colorConfig, qIndex: 0, effort);
        byte[] payload = stream.ToArray();
        string outputDirectory = TestEnvironment.CreateOutputDirectory("Heif", "Av1", nameof(this.EncodeLosslessPartitionSearchAcrossClippedSuperblocks));
        string outputName = $"{width}x{height}-effort{effort}";
        File.WriteAllBytes(Path.Combine(outputDirectory, outputName + ".obu"), payload);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decoded = decoder.DecodeFrameBuffer(payload, null, null, out _);
        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
        Buffer2DRegion<byte> actual = decoded.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        using FileStream rawOutput = File.Create(Path.Combine(outputDirectory, outputName + ".source.yuv"));
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> expectedRow = MemoryMarshal.AsBytes(source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y));
            Assert.Equal(expectedRow, actual.DangerousGetRowSpan(y));
            rawOutput.Write(expectedRow);
        }
    }

    [Fact]
    public void EncodeEffortNineSelectsSubEightPartition()
    {
        const int Size = 16;
        using Image<Rgba32> source = new(Size, Size);
        for (int y = 0; y < Size; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Size; x++)
            {
                // The bottom-right 8x8 uses horizontal prediction on its left half and vertical prediction
                // on its right half. Twelve source values keep a parent palette from reproducing both halves.
                byte value;
                if (x < 8 && y < 8)
                {
                    value = 128;
                }
                else if (y < 8)
                {
                    value = (byte)(16 + ((x - 8) * 20));
                }
                else
                {
                    value = x < 12
                        ? (byte)(176 + ((y - 8) * 9))
                        : (byte)(16 + ((x - 8) * 20));
                }

                row[x] = new Rgba32(value, value, value);
            }
        }

        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400),
            qIndex: 4,
            effort: 9);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        Point[] leafPositions =
        [
            new(2, 2),
            new(3, 2),
            new(2, 3),
            new(3, 3)
        ];

        foreach (Point leafPosition in leafPositions)
        {
            Assert.Equal(
                Av1BlockSize.Block4x8,
                frameInfo.GetModeInfoAt(leafPosition).BlockSize);
        }

        Assert.Equal(new Size(Size, Size), decoded.Size);
    }

    [Fact]
    public void EncodeEffortNineSelectsSixteenBySixteenVerticalPartition()
    {
        const int Size = 32;
        using Image<Rgba32> source = new(Size, Size);
        for (int y = 0; y < Size; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Size; x++)
            {
                byte value = 128;
                if (x == 15 && y >= 16)
                {
                    value = (byte)(24 + ((y - 16) * 13));
                }
                else if (y == 15 && x >= 16)
                {
                    value = (byte)(16 + ((x - 16) * 15));
                }
                else if (x >= 16 && y >= 16)
                {
                    // The left 8x16 half repeats its external left edge, while the right half repeats
                    // its external top edge. One 16x16 predictor cannot reproduce both surfaces.
                    value = x < 24
                        ? (byte)(24 + ((y - 16) * 13))
                        : (byte)(16 + ((x - 16) * 15));
                }

                row[x] = new Rgba32(value, value, value);
            }
        }

        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400),
            qIndex: 4,
            effort: 9);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        for (int modeInfoY = 4; modeInfoY < 8; modeInfoY++)
        {
            for (int modeInfoX = 4; modeInfoX < 8; modeInfoX++)
            {
                Assert.Equal(
                    Av1BlockSize.Block8x16,
                    frameInfo.GetModeInfoAt(new Point(modeInfoX, modeInfoY)).BlockSize);
            }
        }

        Assert.Equal(new Size(Size, Size), decoded.Size);
    }

    [Fact]
    public void EncodeEffortTenSelectsThirtyTwoByThirtyTwoBlocks()
    {
        const int Size = 32;
        using Image<Rgba32> source = new(Size, Size);
        for (int y = 0; y < Size; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Size; x++)
            {
                row[x] = new Rgba32(128, 128, 128);
            }
        }

        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400),
            qIndex: 4,
            effort: 10);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        for (int modeInfoY = 0; modeInfoY < 8; modeInfoY++)
        {
            for (int modeInfoX = 0; modeInfoX < 8; modeInfoX++)
            {
                Assert.Equal(
                    Av1BlockSize.Block32x32,
                    frameInfo.GetModeInfoAt(new Point(modeInfoX, modeInfoY)).BlockSize);
            }
        }

        Assert.Equal(new Size(Size, Size), decoded.Size);
    }

    [Theory]
    [InlineData(Yuv400)]
    [InlineData(Yuv444)]
    public void EncodeEffortTenSelectsSixtyFourBySixtyFourBlock(int colorFormatValue)
    {
        const int Size = 64;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        using Image<Rgba32> source = new(Size, Size);
        for (int y = 0; y < Size; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Size; x++)
            {
                row[x] = new Rgba32(180, 64, 220);
            }
        }

        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(Av1BitDepth.EightBit, colorFormat),
            qIndex: 4,
            effort: 10);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        for (int modeInfoY = 0; modeInfoY < 16; modeInfoY++)
        {
            for (int modeInfoX = 0; modeInfoX < 16; modeInfoX++)
            {
                Assert.Equal(
                    Av1BlockSize.Block64x64,
                    frameInfo.GetModeInfoAt(new Point(modeInfoX, modeInfoY)).BlockSize);
            }
        }

        Assert.Equal(new Size(Size, Size), decoded.Size);
    }

    [Theory]
    [InlineData(Yuv400)]
    [InlineData(Yuv444)]
    public void EncodeEffortTenSelectsOneHundredTwentyEightByOneHundredTwentyEightBlock(int colorFormatValue)
    {
        const int Size = 128;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        using Image<Rgba32> source = new(Size, Size);
        for (int y = 0; y < Size; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Size; x++)
            {
                row[x] = new Rgba32(180, 64, 220);
            }
        }

        using MemoryStream stream = new();
        ObuSequenceHeader sequenceHeader = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(Av1BitDepth.EightBit, colorFormat),
            qIndex: 4,
            effort: 10);

        Assert.True(sequenceHeader.Use128x128Superblock);
        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        for (int modeInfoY = 0; modeInfoY < 32; modeInfoY++)
        {
            for (int modeInfoX = 0; modeInfoX < 32; modeInfoX++)
            {
                Assert.Equal(
                    Av1BlockSize.Block128x128,
                    frameInfo.GetModeInfoAt(new Point(modeInfoX, modeInfoY)).BlockSize);
            }
        }

        Assert.Equal(new Size(Size, Size), decoded.Size);
    }

    [Fact]
    public void EncodeEffortTenSearchesHighBitDepthOneHundredTwentyEightRoot()
    {
        const int Size = 128;
        using Image<Rgba32> source = new(Size, Size);
        for (int y = 0; y < Size; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Size; x++)
            {
                row[x] = new Rgba32(180, 64, 220);
            }
        }

        using MemoryStream stream = new();
        ObuSequenceHeader sequenceHeader = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(Av1BitDepth.TwelveBit, Av1ColorFormat.Yuv444),
            qIndex: 4,
            effort: 10);

        Assert.True(sequenceHeader.Use128x128Superblock);
        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Assert.Equal(new Size(Size, Size), decoded.Size);
    }

    [Theory]
    [InlineData(EightBit)]
    [InlineData(TenBit)]
    [InlineData(TwelveBit)]
    public void EncodeAlphaWritesMonochromeReducedStillPicture(int bitDepthValue)
    {
        const int Width = 16;
        const int Height = 16;
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        using Image<Rgba64> source = new(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            Span<Rgba64> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                ushort alpha = (ushort)(((x + y) * ushort.MaxValue) / (Width + Height - 2));
                row[x] = new Rgba64(ushort.MaxValue, 0, 0, alpha);
            }
        }

        using MemoryStream stream = new();
        ObuSequenceHeader encodedHeader = Av1FrameEncoder.EncodeAlpha(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(bitDepth),
            qIndex: 37,
            effort: 5);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba64> decoded = decoder.Decode<Rgba64>(payload);

        Assert.True(encodedHeader.ColorConfig.IsMonochrome);
        Assert.Equal(
            bitDepth == Av1BitDepth.TwelveBit ? ObuSequenceProfile.Professional : ObuSequenceProfile.Main,
            encodedHeader.SequenceProfile);

        Assert.Equal(new Size(Width, Height), decoded.Size);
        Assert.True(decoded[0, 0].R < decoded[Width - 1, Height - 1].R);
        Assert.Equal(decoded[0, 0].R, decoded[0, 0].G);
        Assert.Equal(decoded[0, 0].R, decoded[0, 0].B);
        Assert.Equal(ushort.MaxValue, decoded[0, 0].A);

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(
            Path.Combine(outputDirectory, $"encoder-alpha-{Width}x{Height}-{bitDepth.GetBitCount()}b.obu"),
            payload);
    }

    [Fact]
    public void AlphaConversionUsesOnePooledRowAndPreservesTwelveBitPrecision()
    {
        const int Width = 19;
        const int Border = Av1EncoderFrame<ushort>.LumaBorder;
        using Image<Rgba64> image = new(Width, 1);
        ushort[] expected = new ushort[Width];
        for (int x = 0; x < Width; x++)
        {
            ushort alpha = (ushort)((x * (long)ushort.MaxValue) / (Width - 1));
            image[x, 0] = new Rgba64(0, 0, 0, alpha);
            expected[x] = (ushort)(((alpha * 4095L) + (ushort.MaxValue / 2)) / ushort.MaxValue);
        }

        using Av1EncoderFrameBuffer<ushort> frameBuffer = new(
            Configuration.Default,
            Width,
            1,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0);

        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        HeifPlanarAlphaEncoder.Convert<
            Rgba64,
            Av1EncoderFrame<ushort>.PlanarView,
            ushort,
            HeifUShortSampleConverter>(
            configuration,
            image.Frames.RootFrame,
            frameBuffer.Frame.View);

        frameBuffer.Frame.ExtendBorders();

        AssertReplicatedSingleRow(frameBuffer.Luma, Border, expected);
        TestMemoryAllocator.AllocationRequest allocation = Assert.Single(allocator.AllocationLog);
        Assert.Equal(typeof(float), allocation.ElementType);
        Assert.Equal(Width * 3, allocation.Length);
        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    [Theory]
    [InlineData(EightBit, Yuv400, 0x1F, 0x1C)]
    [InlineData(TenBit, Yuv420, 0x1F, 0x4C)]
    [InlineData(TenBit, Yuv444, 0x3F, 0x40)]
    [InlineData(TwelveBit, Yuv422, 0x5F, 0x68)]
    public void CodecConfigurationWritesFixedHeaderFromEncodedSequenceHeader(
        int bitDepthValue,
        int colorFormatValue,
        byte expectedProfileAndLevel,
        byte expectedColorFlags)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        using Image<Rgba32> source = new(8, 8);
        using MemoryStream stream = new();
        ObuSequenceHeader sequenceHeader = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(bitDepth, colorFormat),
            qIndex: 37,
            effort: 5);

        Av1CodecConfiguration configuration = new(sequenceHeader);
        byte[] fixedHeader = new byte[Av1CodecConfiguration.FixedHeaderSize];
        configuration.WriteFixedHeader(fixedHeader);

        Assert.Equal([0x81, expectedProfileAndLevel, expectedColorFlags, 0x00], fixedHeader);
        Av1CodecConfiguration parsed = new(fixedHeader, new DecoderOptions());
        Assert.True(configuration.HasMatchingImageConfiguration(parsed));
        parsed.Validate(sequenceHeader);
    }

    [Fact]
    public void ScreenContentDetectorMatchesLibaomFeatureThresholds()
    {
        const int width = 160;
        const int height = 16;
        using Av1EncoderFrameBuffer<byte> byteFrame = new(
            Configuration.Default,
            width,
            height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        for (int row = 0; row < height; row++)
        {
            Span<byte> samples = byteFrame.Frame.View.GetLumaRowSpan(row)[..width];
            samples.Fill(96);
            for (int column = 0; column < 16; column++)
            {
                samples[column] = column < 8 ? (byte)32 : (byte)224;
            }
        }

        // One qualifying block is exactly ten percent of this frame, and the reference threshold is strict.
        Assert.False(Av1ScreenContentDetector.IsPaletteLikely(byteFrame.Frame));
        Av1ScreenContentDetector.Detect(
            byteFrame.Frame,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        Assert.False(allowScreenContentTools);
        Assert.False(allowIntraBlockCopy);
        for (int row = 0; row < height; row++)
        {
            Span<byte> samples = byteFrame.Frame.View.GetLumaRowSpan(row);
            for (int column = 16; column < 32; column++)
            {
                samples[column] = column < 24 ? (byte)48 : (byte)208;
            }
        }

        Assert.True(Av1ScreenContentDetector.IsPaletteLikely(byteFrame.Frame));
        Av1ScreenContentDetector.Detect(
            byteFrame.Frame,
            out allowScreenContentTools,
            out allowIntraBlockCopy);

        Assert.True(allowScreenContentTools);
        Assert.True(allowIntraBlockCopy);
        using Av1EncoderFrameBuffer<ushort> highBitDepthFrame = new(
            Configuration.Default,
            16,
            16,
            10,
            Av1ColorFormat.Yuv400,
            0,
            0);

        for (int row = 0; row < 16; row++)
        {
            Span<ushort> samples = highBitDepthFrame.Frame.View.GetLumaRowSpan(row);
            for (int column = 0; column < 16; column++)
            {
                samples[column] = column < 8 ? (ushort)128 : (ushort)131;
            }
        }

        Assert.False(Av1ScreenContentDetector.IsPaletteLikely(highBitDepthFrame.Frame));
        Av1ScreenContentDetector.Detect(
            highBitDepthFrame.Frame,
            out allowScreenContentTools,
            out allowIntraBlockCopy);

        Assert.False(allowScreenContentTools);
        Assert.False(allowIntraBlockCopy);
        for (int row = 0; row < 16; row++)
        {
            Span<ushort> samples = highBitDepthFrame.Frame.View.GetLumaRowSpan(row);
            samples[8..16].Fill(640);
        }

        Assert.True(Av1ScreenContentDetector.IsPaletteLikely(highBitDepthFrame.Frame));
        Av1ScreenContentDetector.Detect(
            highBitDepthFrame.Frame,
            out allowScreenContentTools,
            out allowIntraBlockCopy);

        Assert.True(allowScreenContentTools);
        Assert.True(allowIntraBlockCopy);
        for (int row = 0; row < 16; row++)
        {
            Span<ushort> samples = highBitDepthFrame.Frame.View.GetLumaRowSpan(row);
            for (int column = 0; column < 16; column++)
            {
                samples[column] = (ushort)((column % 5) * 200);
            }
        }

        Assert.False(Av1ScreenContentDetector.IsPaletteLikely(highBitDepthFrame.Frame));
        Av1ScreenContentDetector.Detect(
            highBitDepthFrame.Frame,
            out allowScreenContentTools,
            out allowIntraBlockCopy);

        Assert.False(allowScreenContentTools);
        Assert.False(allowIntraBlockCopy);
    }

    [Fact]
    public void ScreenContentDetectorMatchesLibaomIntraBlockCopyVarianceThreshold()
    {
        const int Width = 16;
        const int Height = 16;
        using Av1EncoderFrameBuffer<byte> frame = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<byte> luma = frame.Frame.View.GetPlane(Av1Plane.Y);
        for (int row = 0; row < Height; row++)
        {
            luma.DangerousGetRowSpan(row).Fill(96);
        }

        // A single delta of eleven leaves total variance below half a sample after per-pixel rounding.
        luma.DangerousGetRowSpan(0)[0] = 107;
        Av1ScreenContentDetector.Detect(
            frame.Frame,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        Assert.True(allowScreenContentTools);
        Assert.False(allowIntraBlockCopy);

        // Raising that delta to twelve crosses the exact integer rounding boundary used by libaom.
        luma.DangerousGetRowSpan(0)[0] = 108;
        Av1ScreenContentDetector.Detect(
            frame.Frame,
            out allowScreenContentTools,
            out allowIntraBlockCopy);

        Assert.True(allowScreenContentTools);
        Assert.True(allowIntraBlockCopy);
    }

    [Fact]
    public void EncodeActivatesScreenContentTools()
    {
        const int width = 16;
        const int height = 16;
        using Image<Rgba32> source = new(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<Rgba32> pixels = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < width; column++)
            {
                pixels[column] = (((column >> 2) + (row >> 2)) & 1) == 0
                    ? new Rgba32(224, 32, 32)
                    : new Rgba32(32, 32, 224);
            }
        }

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv444);
        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 37,
            effort: 5);

        byte[] payload = stream.ToArray();
        Av1BitStreamReader reader = new(payload);
        Av1TileDecoderStub tileReader = new();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref reader, payload.Length, () => tileReader);
        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(obuReader.FrameHeader);
        Assert.True(frameHeader.AllowScreenContentTools);
        Assert.True(frameHeader.AllowIntraBlockCopy);
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Assert.Equal(new Size(width, height), decoded.Size);

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, "encoder-frame-16x16-8b-444-palette.obu"), payload);
    }

    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(1, false, false, false)]
    [InlineData(2, false, false, false)]
    [InlineData(3, false, false, false)]
    [InlineData(4, true, false, false)]
    [InlineData(5, true, true, false)]
    [InlineData(6, true, true, true)]
    [InlineData(7, true, true, true)]
    [InlineData(8, true, true, true)]
    [InlineData(10, true, true, true)]
    public void EncodeEffortControlsSearchFeatures(
        int effort,
        bool enableFilterIntra,
        bool enableScreenContentTools,
        bool selectTransformSize)
    {
        const int width = 16;
        const int height = 16;
        using Image<Rgba32> source = new(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<Rgba32> pixels = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < width; column++)
            {
                pixels[column] = (((column >> 2) + (row >> 2)) & 1) == 0
                    ? new Rgba32(224, 32, 32)
                    : new Rgba32(32, 32, 224);
            }
        }

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv444);
        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 37,
            effort);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        Assert.Equal(enableFilterIntra, sequenceHeader.EnableFilterIntra);
        Assert.Equal(enableScreenContentTools, frameHeader.AllowScreenContentTools);
        Assert.Equal(enableScreenContentTools, frameHeader.AllowIntraBlockCopy);
        Assert.Equal(
            selectTransformSize ? Av1TransformMode.Select : Av1TransformMode.Largest,
            frameHeader.TransformMode);
        Assert.Equal(new Size(width, height), decoded.Size);

        int modeCount = 0;
        foreach (Av1BlockModeInfo modeInfo in frameInfo.GetSuperblock(Point.Empty).GetModeInfos())
        {
            modeCount++;
            if (effort == 0)
            {
                Assert.Equal(Av1PredictionMode.DC, modeInfo.YMode);
                Assert.Equal(Av1ChromaPredictionMode.DC, modeInfo.UvMode);
            }

            if (effort <= 1)
            {
                Assert.Equal(0, modeInfo.GetAngleDelta(Av1Plane.Y));
                Assert.Equal(0, modeInfo.GetAngleDelta(Av1Plane.U));
            }

            if (effort < 4)
            {
                Assert.False(modeInfo.UseFilterIntra);
            }

            if (effort < 5)
            {
                Assert.False(modeInfo.UseIntraBlockCopy);
                Assert.Equal(0, modeInfo.GetPaletteSize(Av1Plane.Y));
                Assert.Equal(0, modeInfo.GetPaletteSize(Av1Plane.U));
            }
        }

        Assert.NotEqual(0, modeCount);

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, $"encoder-frame-16x16-8b-444-effort-{effort}.obu"), payload);
    }

    [Fact]
    public void EncodeEffortSixSelectsFourByFourLumaTransforms()
    {
        const int Width = 16;
        const int Height = 16;
        using Image<Rgba32> source = new(Width, Height);
        for (int row = 0; row < Height; row++)
        {
            Span<Rgba32> pixels = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < Width; column++)
            {
                byte value = (byte)(16 + ((((row >> 2) * 4) + (column >> 2)) * 14));
                pixels[column] = new Rgba32(value, value, value);
            }
        }

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400);
        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 37,
            effort: 6);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<L8> decoded = decoder.Decode<L8>(payload);
        Assert.NotNull(decoder.FrameHeader);
        Assert.Equal(Av1TransformMode.Select, decoder.FrameHeader.TransformMode);
        Assert.NotNull(decoder.FrameInfo);
        bool foundSplitTransform = false;
        foreach (Av1BlockModeInfo modeInfo in decoder.FrameInfo.GetSuperblock(Point.Empty).GetModeInfos())
        {
            foundSplitTransform |= modeInfo.GetTransformUnitCount(Av1Plane.Y) == 4;
        }

        Assert.True(foundSplitTransform);
        Assert.Equal(new Size(Width, Height), decoded.Size);

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(
            Path.Combine(outputDirectory, "encoder-frame-16x16-8b-400-transform-size-select.obu"),
            payload);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void EncodeSelectsIntraBlockCopyForRepeatedScreenContent(
        int effort,
        bool selectTransformSize)
    {
        const int Width = 328;
        const int Height = 16;
        const ulong Pattern = 0xD6A5_3C97_E18B_4F20UL;
        using Image<Rgba32> source = new(Width, Height);
        for (int row = 0; row < Height; row++)
        {
            Span<Rgba32> pixels = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < Width; column++)
            {
                int patternIndex = ((row & 7) * 8) + (column & 7);
                pixels[column] = ((Pattern >> patternIndex) & 1) == 0
                    ? new Rgba32(224, 32, 32)
                    : new Rgba32(32, 32, 224);
            }
        }

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv444);
        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 37,
            effort);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Assert.NotNull(decoder.FrameHeader);
        Assert.True(decoder.FrameHeader.AllowScreenContentTools);
        Assert.True(decoder.FrameHeader.AllowIntraBlockCopy);
        Assert.Equal(
            selectTransformSize ? Av1TransformMode.Select : Av1TransformMode.Largest,
            decoder.FrameHeader.TransformMode);
        Assert.NotNull(decoder.FrameInfo);
        Av1SuperblockInfo targetSuperblock = decoder.FrameInfo.GetSuperblock(new Point(5, 0));
        bool usesIntraBlockCopy = false;
        foreach (Av1BlockModeInfo modeInfo in targetSuperblock.GetModeInfos())
        {
            usesIntraBlockCopy |= modeInfo.UseIntraBlockCopy;
        }

        Assert.True(usesIntraBlockCopy);
        Assert.Equal(new Size(Width, Height), decoded.Size);

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        string fileName = effort == 5
            ? "encoder-frame-328x16-8b-444-intrabc.obu"
            : "encoder-frame-328x16-8b-444-intrabc-effort-6.obu";

        File.WriteAllBytes(Path.Combine(outputDirectory, fileName), payload);
    }

    [Fact]
    public void PrepareSourceConvertsRgba32DirectlyIntoBorderedEightBitPlane()
    {
        const int width = 4;
        const int height = 1;
        const int border = Av1EncoderFrame<byte>.LumaBorder;

        using Image<Rgba32> image = new(width, height);
        image[0, 0] = new Rgba32(byte.MaxValue, 0, 0, 0);
        image[1, 0] = new Rgba32(0, byte.MaxValue, 0);
        image[2, 0] = new Rgba32(0, 0, byte.MaxValue);
        image[3, 0] = new Rgba32(byte.MaxValue, byte.MaxValue, byte.MaxValue);

        using Av1EncoderFrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            width,
            height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit);

        Av1FrameEncoder.PrepareSource(Configuration.Default, image.Frames.RootFrame, frameBuffer.Frame, colorConfig);

        byte[] expected = [76, 150, 29, 255];
        AssertReplicatedSingleRow(frameBuffer.Luma, border, expected);
    }

    [Fact]
    public void PrepareSourcePreservesHighBitDepthPrecision()
    {
        const int width = 4;
        const int height = 1;
        const int border = Av1EncoderFrame<ushort>.LumaBorder;

        using Image<Rgba64> image = new(width, height);
        image[0, 0] = new Rgba64(ushort.MaxValue, 0, 0, 0);
        image[1, 0] = new Rgba64(0, ushort.MaxValue, 0, ushort.MaxValue);
        image[2, 0] = new Rgba64(0, 0, ushort.MaxValue, ushort.MaxValue);
        image[3, 0] = new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

        using Av1EncoderFrameBuffer<ushort> frameBuffer = new(
            Configuration.Default,
            width,
            height,
            10,
            Av1ColorFormat.Yuv400,
            0,
            0);

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.TenBit);

        Av1FrameEncoder.PrepareSource(Configuration.Default, image.Frames.RootFrame, frameBuffer.Frame, colorConfig);

        ushort[] expected = [306, 601, 117, 1023];
        AssertReplicatedSingleRow(frameBuffer.Luma, border, expected);
    }

    [Fact]
    public void ExtendBordersReplicatesEveryPhysicalPlaneEdge()
    {
        const int visibleWidth = 5;
        const int visibleHeight = 3;
        const int lumaBorder = Av1EncoderFrame<byte>.LumaBorder;
        const int chromaBorder = lumaBorder / 2;

        using Av1EncoderFrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            visibleWidth,
            visibleHeight,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1);

        Buffer2D<byte> luma = frameBuffer.Luma;
        Buffer2D<byte> chromaBlue = Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaBlue);
        Buffer2D<byte> chromaRed = Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaRed);

        FillVisible(luma, lumaBorder, lumaBorder, visibleWidth, visibleHeight, 10);
        FillVisible(chromaBlue, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 80);
        FillVisible(chromaRed, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 120);

        frameBuffer.Frame.ExtendBorders();

        AssertReplicatedPlane(luma, lumaBorder, lumaBorder, visibleWidth, visibleHeight, 10);
        AssertReplicatedPlane(chromaBlue, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 80);
        AssertReplicatedPlane(chromaRed, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 120);
    }

    [Theory]
    [InlineData(5, 3, 0, 0, 160, 136)]
    [InlineData(5, 3, 1, 0, 80, 136)]
    [InlineData(5, 3, 1, 1, 80, 68)]
    [InlineData(1921, 1081, 0, 0, 2080, 1216)]
    [InlineData(1921, 1081, 1, 1, 1040, 608)]
    public void GetPlaneBufferSizeMatchesLibaomLayout(
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int expectedWidth,
        int expectedHeight)
    {
        Size actual = Av1EncoderFrame<byte>.GetPlaneBufferSize(width, height, subsamplingX, subsamplingY);

        Assert.Equal(new Size(expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void FrameBufferUsesOneExactSizeOwnerForAllPlanes()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1EncoderFrameBuffer<byte> frameBuffer = new(
            configuration,
            64,
            64,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(55_296, allocation.Length);
            Assert.Single(frameBuffer.Luma.MemoryGroup);
            Assert.Single(Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaBlue).MemoryGroup);
            Assert.Single(Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaRed).MemoryGroup);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    [Fact]
    public void EncodeReturnsEveryOperationAllocationAndUsesOneLibaomSizedTileReservation()
    {
        const int Width = 64;
        const int Height = 64;
        const int ExpectedTileOutputLength = 60 * 1024;

        using Image<Rgba32> source = new(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                row[x] = new Rgba32(
                    (byte)((x * 3) + y),
                    (byte)(x + (y * 5)),
                    (byte)((x * 7) + (y * 11)));
            }
        }

        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        using MemoryStream storage = new();
        using NonSeekableStream destination = new(storage);

        _ = Av1FrameEncoder.Encode(
            configuration,
            source.Frames.RootFrame,
            destination,
            CreateColorConfig(Av1BitDepth.TwelveBit, Av1ColorFormat.Yuv444),
            qIndex: 37,
            effort: 5);

        Assert.False(destination.CanSeek);
        Assert.NotEqual(0, storage.Length);
        TestMemoryAllocator.AllocationRequest tileOutput = Assert.Single(
            allocator.AllocationLog,
            allocation => allocation.ElementType == typeof(byte) && allocation.Length == ExpectedTileOutputLength);

        Assert.Equal(ExpectedTileOutputLength, tileOutput.Length);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.Equal(
            allocator.AllocationLog.Select(allocation => allocation.AllocationId).Order(),
            allocator.ReturnLog.Select(returned => returned.AllocationId).Order());
    }

    private static ObuColorConfig CreateColorConfig(
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat = Av1ColorFormat.Yuv400)
        => new()
        {
            IsColorDescriptionPresent = true,
            IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
            ColorPrimaries = ObuColorPrimaries.Bt601,
            TransferCharacteristics = ObuTransferCharacteristics.Bt601,
            MatrixCoefficients = ObuMatrixCoefficients.Bt601,
            ColorRange = true,
            SubSamplingX = colorFormat != Av1ColorFormat.Yuv444,
            SubSamplingY = colorFormat == Av1ColorFormat.Yuv400 || colorFormat == Av1ColorFormat.Yuv420,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            BitDepth = bitDepth
        };

    private static void FillVisible(Buffer2D<byte> plane, int originX, int originY, int width, int height, int seed)
    {
        for (int y = 0; y < height; y++)
        {
            Span<byte> row = plane.DangerousGetRowSpan(originY + y);
            for (int x = 0; x < width; x++)
            {
                row[originX + x] = (byte)(seed + (y * width) + x);
            }
        }
    }

    private static void AssertReplicatedPlane(Buffer2D<byte> plane, int originX, int originY, int width, int height, int seed)
    {
        for (int y = 0; y < plane.Height; y++)
        {
            ReadOnlySpan<byte> row = plane.DangerousGetRowSpan(y);
            int sourceY = Math.Clamp(y - originY, 0, height - 1);
            for (int x = 0; x < row.Length; x++)
            {
                int sourceX = Math.Clamp(x - originX, 0, width - 1);
                Assert.Equal((byte)(seed + (sourceY * width) + sourceX), row[x]);
            }
        }
    }

    private static void AssertReplicatedSingleRow<TSample>(
        Buffer2D<TSample> plane,
        int originX,
        ReadOnlySpan<TSample> expected)
        where TSample : unmanaged, IEquatable<TSample>
    {
        for (int y = 0; y < plane.Height; y++)
        {
            ReadOnlySpan<TSample> row = plane.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                int sourceX = Math.Clamp(x - originX, 0, expected.Length - 1);
                Assert.Equal(expected[sourceX], row[x]);
            }
        }
    }

    private sealed class FailingSequenceAllocator : TestMemoryAllocator
    {
        private readonly int failureIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="FailingSequenceAllocator"/> class.
        /// </summary>
        /// <param name="failureIndex">The zero-based allocation request that fails.</param>
        public FailingSequenceAllocator(int failureIndex)
        {
            this.failureIndex = failureIndex;
            this.EnableNonThreadSafeLogging();
        }

        /// <inheritdoc/>
        protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options)
        {
            if (this.AllocationLog.Count == this.failureIndex)
            {
                throw new InvalidMemoryOperationException("Sequence allocation failure.");
            }

            return base.AllocateCore<T>(length, options);
        }
    }
}

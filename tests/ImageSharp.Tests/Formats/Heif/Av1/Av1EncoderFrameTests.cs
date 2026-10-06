// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
    private const int ConstantQuality = (int)Av1RateControlMode.Quality;
    private const int VariableBitRate = (int)Av1RateControlMode.VariableBitRate;
    private const int ConstrainedQuality = (int)Av1RateControlMode.ConstrainedQuality;
    private const int ConstantBitRate = (int)Av1RateControlMode.ConstantBitRate;

    [Fact]
    public void RectangularIntraReferencesExtendTheLastAvailableSample()
    {
        RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)EightBit, false, false, false);
        RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)TenBit, true, true, false);
        RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)EightBit, true, true, true);
        RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)TwelveBit, false, true, true);
    }

    private static void RectangularIntraReferencesExtendTheLastAvailableSampleCase(int bitDepthValue, bool transpose, bool extensionAvailable, bool limitedExtent)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        if (bitDepth == Av1BitDepth.EightBit)
        {
            AssertRectangularIntraReferences<byte, Av1IntraSuperblockEncoder.ByteOperator>(bitDepth, transpose, extensionAvailable, limitedExtent);
        }
        else
        {
            AssertRectangularIntraReferences<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(bitDepth, transpose, extensionAvailable, limitedExtent);
        }
    }

    private static void AssertRectangularIntraReferences<TSample, TOperator>(
        Av1BitDepth bitDepth,
        bool transpose,
        bool extensionAvailable,
        bool limitedExtent)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        int width = transpose ? 16 : 4;
        int height = transpose ? 4 : 16;
        int scale = 1 << (bitDepth.GetBitCount() - 8);
        using IMemoryOwner<TSample> planeOwner = Configuration.Default.MemoryAllocator.Allocate<TSample>(33 * 33);
        Av1PlaneRegion<TSample> plane = new(planeOwner.Memory, 33, new Rectangle(0, 0, 33, 33));
        for (int i = 0; i < 32; i++)
        {
            plane.GetRowSpan(0)[i + 1] = TOperator.CreateSample((10 + i) * scale);
            plane.GetRowSpan(i + 1)[0] = TOperator.CreateSample((50 + i) * scale);
        }

        plane.GetRowSpan(0)[0] = TOperator.CreateSample(100 * scale);

        // The reference extends a four-sample edge through its four-sample neighbor, then
        // repeats sample seven to cover the twenty samples required by a 4x16 directional ray.
        // A clipped frame leaves only two adjacent samples; backing-buffer values beyond the region
        // must not contribute. These explicit offsets distinguish all three extension cases.
        int[] shortEdge = limitedExtent
            ? [0, 1, 2, 3, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5]
            : extensionAvailable
                ? [0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7]
                : [0, 1, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3];

        int[] longEdge = limitedExtent
            ? [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 17, 17]
            : extensionAvailable
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
            plane.GetSubRegion(0, 0, limitedExtent ? width + 3 : 33, limitedExtent ? height + 3 : 33),
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
            speed: HeifEncodingSpeed.Level9);

        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decodedPlanes = decoder.DecodeFrameBuffer(stream.ToArray(), null, null, out _);
        using Image<L8> decoded = new(Configuration.Default, decodedPlanes.Width, decodedPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            decodedPlanes,
            decoded.Bounds,
            decoded.Frames.RootFrame.PixelBuffer.GetRegion(decoded.Bounds),
            decoded.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            decodedPlanes.ColorConfig.ColorRange);

        Assert.Equal(2, decoder.FrameHeader.TilesInfo.TileColumnCount);
        Assert.Equal(1, decoder.FrameHeader.TilesInfo.TileRowCount);
        Assert.Equal(source.Size, decoded.Size);
        Assert.Equal(source[0, 0], decoded[0, 0]);
        Assert.Equal(source[secondTileStart - 1, 0], decoded[secondTileStart - 1, 0]);
        Assert.Equal(source[secondTileStart, 0], decoded[secondTileStart, 0]);
        Assert.Equal(source[Width - 1, Height - 1], decoded[Width - 1, Height - 1]);
    }

    [Fact]
    public void CanceledStillEncodeStopsAtTheFirstSuperblockRow()
    {
        // The still path has no frame-level check of its own, so only the superblock-row check can stop it.
        using Image<L8> source = new(64, 64, new L8(128));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level9, Av1Tuning.Psnr, enableRestoration: false)
        {
            CancellationToken = cancellation.Token
        };

        using MemoryStream stream = new();
        Assert.Throws<OperationCanceledException>(() => Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400),
            qIndex: 37,
            options));
    }

    [Theory]
    [InlineData(16, 16, EightBit, Yuv400)]
    [InlineData(13, 11, EightBit, Yuv420)]
    [InlineData(16, 16, TwelveBit, Yuv444)]
    public void EncodeWritesReducedStillPictureConsumedByProductionDecoder(int width, int height, int bitDepthValue, int colorFormatValue)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        using Image<Rgba32> source = LoadCrop<Rgba32>(TestImages.Png.CalliphoraPartial, new Rectangle(150, 120, width, height));

        ObuColorConfig colorConfig = CreateColorConfig(bitDepth, colorFormat);
        using MemoryStream stream = new();
        ObuSequenceHeader encodedHeader = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 37,
            speed: HeifEncodingSpeed.Level0);

        byte[] payload = stream.ToArray();

        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decodedPlanes = decoder.DecodeFrameBuffer(payload, null, null, out _);
        using Image<Rgba32> decoded = new(Configuration.Default, decodedPlanes.Width, decodedPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            decodedPlanes,
            decoded.Bounds,
            decoded.Frames.RootFrame.PixelBuffer.GetRegion(decoded.Bounds),
            decoded.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            decodedPlanes.ColorConfig.ColorRange);

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

        Assert.NotEqual(first, decoded[width - 1, height - 1]);
    }

    [Theory]
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
                speed: HeifEncodingSpeed.Level0)
            : Av1FrameEncoder.CreateColorSequenceEncoder(
                Configuration.Default,
                Width,
                Height,
                colorConfig,
                qIndex: 37,
                speed: HeifEncodingSpeed.Level0);

        encoder.EncodeKeyFrame(source.Frames.RootFrame, stream);
        ObuSequenceHeader encodedHeader = encoder.SequenceHeader;
        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decodedPlanes = decoder.DecodeFrameBuffer(payload, null, null, out _);
        using Image<Rgba32> decoded = new(Configuration.Default, decodedPlanes.Width, decodedPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            decodedPlanes,
            decoded.Bounds,
            decoded.Frames.RootFrame.PixelBuffer.GetRegion(decoded.Bounds),
            decoded.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            decodedPlanes.ColorConfig.ColorRange);

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
    [InlineData(EightBit, Yuv420, HeifEncodingSpeed.Level9)]
    [InlineData(TenBit, Yuv444, HeifEncodingSpeed.Level0)]
    public void SequenceEncoderPreservesNativeColorPlanesWithSubpixelMotion(int bitDepthValue, int colorFormatValue, HeifEncodingSpeed speed)
        => VerifySequenceEncoderColorPlanes(bitDepthValue, colorFormatValue, speed, 23, 19);

    [Theory]
    [InlineData(HeifEncodingSpeed.Level6, 3)]
    [InlineData(HeifEncodingSpeed.Level9, 3)]
    [InlineData(HeifEncodingSpeed.Level6, 1)]
    [InlineData(HeifEncodingSpeed.Level9, 1)]
    public void SequenceEncoderPlacesKeyFramesAtTheInterval(HeifEncodingSpeed speed, int interval)
    {
        // Without lookahead the interval alone places key frames: every frame whose index is a multiple of it.
        using Image<Rgb24> frames = CreatePanningSequence(7);
        Av1EncoderOptions options = CreateSequenceOptions(speed, lagInFrames: 0, keyFrameInterval: interval);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            frames.Width,
            frames.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            100,
            options);

        // Each key frame restarts the frame count, so the order hint counts from it.
        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        encoder.EncodeKeyFrame(frames.Frames[0], sample);
        decoder.DecodeSequenceReference(sample.ToArray(), null, null);
        for (int i = 1; i < frames.Frames.Count; i++)
        {
            sample.SetLength(0);
            bool keyFrame = encoder.EncodeNextFrame(frames.Frames[i], sample, forceKeyFrame: false);
            decoder.DecodeSequenceReference(sample.ToArray(), null, null);

            Assert.Equal(i % interval == 0, keyFrame);
            Assert.Equal((uint)(i % interval), decoder.FrameHeader.OrderHint);
        }
    }

    [Theory]
    [InlineData(HeifEncodingSpeed.Level6)]
    [InlineData(HeifEncodingSpeed.Level9)]
    public void SequenceEncoderCodesAForcedKeyFrame(HeifEncodingSpeed speed)
    {
        using Image<Rgb24> frames = CreatePanningSequence(3);
        Av1EncoderOptions options = CreateSequenceOptions(speed, lagInFrames: 0, keyFrameInterval: 9999);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            frames.Width,
            frames.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            100,
            options);

        using MemoryStream stream = new();
        encoder.EncodeKeyFrame(frames.Frames[0], stream);

        Assert.False(encoder.EncodeNextFrame(frames.Frames[1], stream, forceKeyFrame: false));
        Assert.True(encoder.EncodeNextFrame(frames.Frames[2], stream, forceKeyFrame: true));
    }

    [Fact]
    public void LookaheadSequenceKeepsKeyFramesWithinTheInterval()
    {
        // The lookahead can place a key frame early at a scene cut, but never further apart than the interval.
        const int Interval = 4;
        using Image<Rgb24> frames = CreatePanningSequence(10);
        Av1EncoderOptions options = CreateSequenceOptions(HeifEncodingSpeed.Level6, lagInFrames: 35, keyFrameInterval: Interval);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            frames.Width,
            frames.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            100,
            options);

        using MemoryStream stream = new();
        long[] sampleEnds = new long[frames.Frames.Count];
        bool[] syncSamples = new bool[frames.Frames.Count];
        encoder.EncodeWithLookahead(frames, 0, frames.Frames.Count, 333333, stream, sampleEnds, syncSamples, CancellationToken.None);

        Assert.True(syncSamples[0]);
        int lastKeyFrame = 0;
        for (int i = 1; i < syncSamples.Length; i++)
        {
            if (syncSamples[i])
            {
                lastKeyFrame = i;
            }

            Assert.True(i - lastKeyFrame < Interval, $"Frame {i} is {i - lastKeyFrame} frames after the last key frame.");
        }
    }

    [Fact]
    public void LookaheadSequenceWithComplexityQuantizationCodesWithoutSegments()
    {
        // Constant quality coding gives each frame no bit target, so complexity adaptive quantization keeps the
        // segments off and every frame still decodes.
        using Image<Rgb24> frames = CreatePanningSequence(5);
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level6, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            LagInFrames = 35,
            KeyFrameMaximumDistance = 9999,
            AdaptiveQuantizationMode = Av1AdaptiveQuantizationMode.Complexity
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            frames.Width,
            frames.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            100,
            options);

        using MemoryStream stream = new();
        long[] sampleEnds = new long[frames.Frames.Count];
        bool[] syncSamples = new bool[frames.Frames.Count];
        encoder.EncodeWithLookahead(frames, 0, frames.Frames.Count, 333333, stream, sampleEnds, syncSamples, CancellationToken.None);

        byte[] data = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        long start = 0;
        foreach (long end in sampleEnds)
        {
            decoder.DecodeSequenceReference(data.AsSpan((int)start, (int)(end - start)), null, null);
            Assert.False(decoder.FrameHeader.SegmentationParameters.Enabled);
            start = end;
        }
    }

    [Theory]
    [InlineData(96, 64, HeifEncodingSpeed.Level8)]
    [InlineData(704, 512, HeifEncodingSpeed.Level9)]
    public void RealtimeSequenceWithCyclicRefreshReconstructsAsDecoded(int width, int height, HeifEncodingSpeed speed)
    {
        // The boosted segments code at their own quantizers, so the decoder must reconstruct every frame exactly as
        // the encoder does. Frames above 640x480 also estimate the source noise every eighth frame.
        using Image<Rgb24> frames = new(width, height);
        for (int frameIndex = 0; frameIndex < 10; frameIndex++)
        {
            ImageFrame<Rgb24> frame = frameIndex == 0 ? frames.Frames.RootFrame : frames.Frames.CreateFrame();
            for (int y = 0; y < height; y++)
            {
                Span<Rgb24> row = frame.PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    // A still gradient with a little noise that changes every frame.
                    int noise = (((x * 7919) + (y * 104729) + (frameIndex * 15485863)) >> 3) & 7;
                    int value = ((x + y) >> 2) + noise;
                    row[x] = new Rgb24((byte)value, (byte)(255 - value), (byte)(value / 2));
                }
            }
        }

        Av1EncoderOptions options = new(speed, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = Av1RateControlMode.ConstantBitRate,
            MinimumQuantizer = 21,
            MaximumQuantizer = 29,
            KeyFrameMaximumDistance = 9999,
            AdaptiveQuantizationMode = Av1AdaptiveQuantizationMode.CyclicRefresh
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            width,
            height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            100,
            options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        bool segmented = false;
        for (int i = 0; i < frames.Frames.Count; i++)
        {
            sample.SetLength(0);
            if (i == 0)
            {
                encoder.EncodeKeyFrame(frames.Frames[0], sample);
            }
            else
            {
                encoder.EncodeNextFrame(frames.Frames[i], sample, forceKeyFrame: false);
            }

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            segmented |= decoder.FrameHeader!.SegmentationParameters.Enabled;
            AssertDecodedLumaMatchesEncoder(encoder, decoder, width, height);
        }

        Assert.True(segmented);
    }

    [Theory]
    [InlineData(HeifEncodingSpeed.Level6)]
    [InlineData(HeifEncodingSpeed.Level8)]
    public void ImageTuneSequenceReconstructsAsDecoded(HeifEncodingSpeed speed)
    {
        // The key frame codes at a lower quantizer than the sequence was created with, and each superblock codes its
        // own delta quantizer, which must start from the quantizer of the frame.
        using Image<Rgb24> image = CreateLayerTestImage();
        bool realtime = speed >= HeifEncodingSpeed.Level7;
        Av1EncoderOptions options = new(speed, Av1Tuning.Iq, enableRestoration: true, allIntra: false)
        {
            RateControlMode = realtime ? Av1RateControlMode.ConstantBitRate : Av1RateControlMode.Quality,
            MinimumQuantizer = realtime ? 36 : 0,
            MaximumQuantizer = realtime ? 44 : 63
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            image.Width,
            image.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            Av1QuantizationLookup.GetQIndex(40),
            options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        for (int i = 0; i < 3; i++)
        {
            sample.SetLength(0);
            if (i == 0)
            {
                encoder.EncodeKeyFrame(image.Frames.RootFrame, sample);
            }
            else
            {
                encoder.EncodeNextFrame(image.Frames.RootFrame, sample, forceKeyFrame: false);
            }

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            Assert.True(decoder.FrameHeader!.DeltaQParameters.IsPresent);
            AssertDecodedLumaMatchesEncoder(encoder, decoder, image.Width, image.Height);
        }
    }

    [Theory]
    [InlineData(HeifEncodingSpeed.Level6)]
    [InlineData(HeifEncodingSpeed.Level8)]
    public void LosslessSequenceCodesInterFramesExactly(HeifEncodingSpeed speed)
    {
        // A lossless inter frame reconstructs its source exactly, as the same frame coded as a lossless key frame does.
        using Image<Rgb24> frames = CreatePanningSequence(4);
        Av1EncoderOptions options = new(speed, Av1Tuning.Psnr, enableRestoration: true, allIntra: false)
        {
            MinimumQuantizer = 0,
            MaximumQuantizer = 0,
            KeyFrameMaximumDistance = 9999
        };

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv444);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default, frames.Width, frames.Height, colorConfig, 0, options);

        using Av1FrameEncoder.SequenceEncoder keyFrameEncoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default, frames.Width, frames.Height, colorConfig, 0, options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        using MemoryStream keyFrameSample = new();
        for (int i = 0; i < frames.Frames.Count; i++)
        {
            sample.SetLength(0);
            if (i == 0)
            {
                encoder.EncodeKeyFrame(frames.Frames[0], sample);
            }
            else
            {
                Assert.False(encoder.EncodeNextFrame(frames.Frames[i], sample, forceKeyFrame: false));
            }

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            Assert.True(decoder.FrameHeader!.CodedLossless);
            AssertDecodedLumaMatchesEncoder(encoder, decoder, frames.Width, frames.Height);

            keyFrameSample.SetLength(0);
            keyFrameEncoder.EncodeKeyFrame(frames.Frames[i], keyFrameSample);
            int slot = BitOperations.TrailingZeroCount(decoder.FrameHeader.RefreshFrameFlags);
            Assert.Equal(keyFrameEncoder.CopySlotLuma(0), encoder.CopySlotLuma(slot));
        }
    }

    [Theory]
    [InlineData(VariableBitRate)]
    [InlineData(ConstrainedQuality)]
    [InlineData(ConstantBitRate)]
    public void GoodQualitySequenceUnderBitBudgetReconstructsAsDecoded(int modeValue)
    {
        // Without lookahead a golden group holds at most 32 frames, so frame 32 is the golden update of the second
        // group. The bit-rate modes keep every frame within four quantizer steps of the request. The constrained-quality
        // mode keeps the full range, and these small frames spend so little of the budget that its quality level falls.
        const int Quantizer = 40;
        Av1RateControlMode mode = (Av1RateControlMode)modeValue;
        bool bitRate = mode != Av1RateControlMode.ConstrainedQuality;
        using Image<Rgb24> frames = CreatePanningSequence(33);
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level6, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = mode,
            MinimumQuantizer = bitRate ? Quantizer - 4 : 0,
            MaximumQuantizer = bitRate ? Quantizer + 4 : 63,
            KeyFrameMaximumDistance = 9999
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            frames.Width,
            frames.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            Av1QuantizationLookup.GetQIndex(Quantizer),
            options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        for (int i = 0; i < frames.Frames.Count; i++)
        {
            sample.SetLength(0);
            if (i == 0)
            {
                encoder.EncodeKeyFrame(frames.Frames[0], sample);
            }
            else
            {
                Assert.False(encoder.EncodeNextFrame(frames.Frames[i], sample, forceKeyFrame: false));
            }

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            if (bitRate)
            {
                int baseQIndex = decoder.FrameHeader!.QuantizationParameters.BaseQIndex;
                Assert.InRange(baseQIndex, Av1QuantizationLookup.GetQIndex(Quantizer - 4), Av1QuantizationLookup.GetQIndex(Quantizer + 4));
            }

            AssertDecodedLumaMatchesEncoder(encoder, decoder, frames.Width, frames.Height);
        }
    }

    [Theory]
    [InlineData(VariableBitRate)]
    [InlineData(ConstrainedQuality)]
    [InlineData(ConstantBitRate)]
    public void LookaheadSequenceUnderBitBudgetDecodes(int modeValue)
    {
        // The lookahead codes alternate references hidden and shows them later, so each sample decodes in its own
        // order. The rate model, not the requested quantizer, picks the quantizers, and the noisy key frame overshoots
        // its target so far that it is coded again at a higher quantizer.
        const int Quantizer = 40;
        Av1RateControlMode mode = (Av1RateControlMode)modeValue;
        using Image<Rgb24> frames = CreateNoiseSequence(192, 128, 6);
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level6, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = mode,
            MinimumQuantizer = 0,
            MaximumQuantizer = 63,
            LagInFrames = 35,
            KeyFrameMaximumDistance = 9999
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            frames.Width,
            frames.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            Av1QuantizationLookup.GetQIndex(Quantizer),
            options);

        using MemoryStream stream = new();
        long[] sampleEnds = new long[frames.Frames.Count];
        bool[] syncSamples = new bool[frames.Frames.Count];
        encoder.EncodeWithLookahead(frames, 0, frames.Frames.Count, 333333, stream, sampleEnds, syncSamples, CancellationToken.None);

        byte[] data = stream.ToArray();
        Assert.True(syncSamples[0]);
        Assert.Equal(data.Length, sampleEnds[^1]);
        using Av1Decoder decoder = new(Configuration.Default);
        long start = 0;
        bool rateModelChose = false;
        foreach (long end in sampleEnds)
        {
            Assert.True(end > start);
            decoder.DecodeSequenceReference(data.AsSpan((int)start, (int)(end - start)), null, null);
            if (!decoder.FrameHeader!.ShowExistingFrame)
            {
                rateModelChose |= decoder.FrameHeader.QuantizationParameters.BaseQIndex != Av1QuantizationLookup.GetQIndex(Quantizer);
            }

            start = end;
        }

        Assert.True(rateModelChose);
        Assert.True(encoder.RecodedFrameCount > 0);

        // Every reference slot reconstructs as the encoder holds it, which a recode that left any state of a
        // discarded coding behind would break.
        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            Av1FrameBuffer<byte> decoded = decoder.GetReferenceFrameBuffer(slot);
            if (decoded is not null)
            {
                AssertLumaMatches(encoder.CopySlotLuma(slot), decoded, frames.Width, frames.Height);
            }
        }
    }

    [Theory]
    [InlineData(VariableBitRate)]
    [InlineData(ConstrainedQuality)]
    [InlineData(ConstantQuality)]
    public void RealtimeSequenceInEachModeReconstructsAsDecoded(int modeValue)
    {
        // Real-time coding outside the constant-bitrate mode boosts only the key frame. The variable-bitrate mode
        // keeps every frame within four quantizer steps of the request, and the constant-quality mode codes every
        // ordinary inter frame at the requested quantizer. These small frames spend so little of the budget that the
        // constrained-quality level falls, so that mode is checked for reconstruction only.
        const int Quantizer = 40;
        Av1RateControlMode mode = (Av1RateControlMode)modeValue;
        bool bitRate = mode == Av1RateControlMode.VariableBitRate;
        using Image<Rgb24> frames = CreatePanningSequence(6);
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level8, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = mode,
            MinimumQuantizer = bitRate ? Quantizer - 4 : 0,
            MaximumQuantizer = bitRate ? Quantizer + 4 : 63,
            KeyFrameMaximumDistance = 9999
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            frames.Width,
            frames.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            Av1QuantizationLookup.GetQIndex(Quantizer),
            options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        for (int i = 0; i < frames.Frames.Count; i++)
        {
            sample.SetLength(0);
            if (i == 0)
            {
                encoder.EncodeKeyFrame(frames.Frames[0], sample);
            }
            else
            {
                encoder.EncodeNextFrame(frames.Frames[i], sample, forceKeyFrame: false);
            }

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            int baseQIndex = decoder.FrameHeader!.QuantizationParameters.BaseQIndex;
            if (bitRate)
            {
                Assert.InRange(baseQIndex, Av1QuantizationLookup.GetQIndex(Quantizer - 4), Av1QuantizationLookup.GetQIndex(Quantizer + 4));
            }
            else if (mode == Av1RateControlMode.Quality)
            {
                if (i == 0)
                {
                    Assert.InRange(baseQIndex, 1, Av1QuantizationLookup.GetQIndex(Quantizer));
                }
                else
                {
                    Assert.Equal(Av1QuantizationLookup.GetQIndex(Quantizer), baseQIndex);
                }
            }

            AssertDecodedLumaMatchesEncoder(encoder, decoder, frames.Width, frames.Height);
        }
    }

    [Theory]
    [InlineData(VariableBitRate)]
    [InlineData(ConstrainedQuality)]
    [InlineData(ConstantBitRate)]
    public void LayeredImageUnderBitBudgetReconstructsAsDecoded(int modeValue)
    {
        // libavif narrows the quantizer range of each layer in the bit-rate modes and sets the quality level of each
        // layer in the constrained-quality mode, and every layer still predicts only from the layer before it.
        Av1RateControlMode mode = (Av1RateControlMode)modeValue;
        bool bitRate = mode != Av1RateControlMode.ConstrainedQuality;
        int[] quantizers = [55, 40, 20];
        using Image<Rgb24> image = CreateLayerTestImage();
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level6, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = mode,
            MinimumQuantizer = bitRate ? quantizers[0] - 4 : 0,
            MaximumQuantizer = bitRate ? quantizers[0] + 4 : 63,
            LayerCount = quantizers.Length
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            image.Width,
            image.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            Av1QuantizationLookup.GetQIndex(quantizers[0]),
            options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        foreach (int quantizer in quantizers)
        {
            sample.SetLength(0);
            encoder.EncodeLayer(
                image.Frames.RootFrame,
                sample,
                Av1QuantizationLookup.GetQIndex(quantizer),
                bitRate ? quantizer - 4 : 0,
                bitRate ? quantizer + 4 : 63);

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            if (bitRate)
            {
                int baseQIndex = decoder.FrameHeader!.QuantizationParameters.BaseQIndex;
                Assert.InRange(baseQIndex, Av1QuantizationLookup.GetQIndex(quantizer - 4), Av1QuantizationLookup.GetQIndex(quantizer + 4));
            }

            AssertDecodedLumaMatchesEncoder(encoder, decoder, image.Width, image.Height);
        }
    }

    [Theory]
    [InlineData(HeifEncodingSpeed.Level3)]
    [InlineData(HeifEncodingSpeed.Level6)]
    [InlineData(HeifEncodingSpeed.Level8)]
    public void LayeredImageReconstructsAsDecoded(HeifEncodingSpeed speed)
    {
        // Each layer after the first predicts only from the layer before it, in slot 0, and replaces it there. A
        // constant-quality layer codes at its own quantizer.
        using Image<Rgb24> image = CreateLayerTestImage();
        int width = image.Width;
        int height = image.Height;
        bool realtime = speed >= HeifEncodingSpeed.Level7;
        Av1EncoderOptions options = new(speed, Av1Tuning.Iq, enableRestoration: true, allIntra: false)
        {
            RateControlMode = realtime ? Av1RateControlMode.ConstantBitRate : Av1RateControlMode.Quality,
            MinimumQuantizer = realtime ? 51 : 0,
            MaximumQuantizer = realtime ? 59 : 63,
            LayerCount = 3,
            UsesFixedQuantizer = !realtime
        };

        int[] quantizers = [55, 40, 20];
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            width,
            height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            Av1QuantizationLookup.GetQIndex(quantizers[0]),
            options);

        Assert.False(encoder.SequenceHeader.IsStillPicture);
        Assert.False(encoder.SequenceHeader.Use128x128Superblock);

        // Operating point i decodes spatial layers 0 to 2 - i of the single temporal layer.
        Assert.Equal([0x701U, 0x301U, 0x101U], encoder.SequenceHeader.OperatingPoint.Select(point => point.Idc));

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        for (int layer = 0; layer < quantizers.Length; layer++)
        {
            int quantizer = quantizers[layer];
            sample.SetLength(0);
            encoder.EncodeLayer(
                image.Frames.RootFrame,
                sample,
                Av1QuantizationLookup.GetQIndex(quantizer),
                realtime ? quantizer - 4 : 0,
                realtime ? quantizer + 4 : 63);

            // Only the first layer starts the temporal unit with a temporal delimiter. Every later layer starts with
            // its frame, whose header carries the extension byte with the layer.
            byte[] bytes = sample.ToArray();
            Assert.Equal(layer == 0 ? 0x12 : 0x36, bytes[0]);
            decoder.DecodeSequenceReference(bytes, null, null);
            ObuFrameHeader frameHeader = decoder.FrameHeader!;
            Assert.Equal(layer, frameHeader.SpatialId);
            int baseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
            if (layer == 0)
            {
                Assert.Equal(ObuFrameType.KeyFrame, frameHeader.FrameType);
            }
            else
            {
                Assert.Equal(ObuFrameType.InterFrame, frameHeader.FrameType);
                Assert.Equal(1U, frameHeader.RefreshFrameFlags);
                Assert.All(frameHeader.GetReferenceFrameIndices().ToArray(), slot => Assert.Equal(0U, slot));
            }

            if (realtime)
            {
                Assert.InRange(baseQIndex, Av1QuantizationLookup.GetQIndex(quantizer - 4), Av1QuantizationLookup.GetQIndex(quantizer + 4));
            }
            else
            {
                Assert.Equal(Av1QuantizationLookup.GetQIndex(quantizer), baseQIndex);
            }

            AssertDecodedLumaMatchesEncoder(encoder, decoder, width, height);
        }

        Assert.Throws<InvalidOperationException>(() => encoder.EncodeLayer(image.Frames.RootFrame, sample, 100, 0, 63));
    }

    [Theory]
    [InlineData(HeifEncodingSpeed.Level3)]
    [InlineData(HeifEncodingSpeed.Level6)]
    [InlineData(HeifEncodingSpeed.Level8)]
    public void ScaledLayeredImageReconstructsAsDecoded(HeifEncodingSpeed speed)
    {
        // The second layer is smaller than the first and predicts it scaled down, and the last layer is larger than the
        // second and predicts it scaled up. Each layer codes and reconstructs at its own size.
        using Image<Rgb24> image = CreateLayerTestImage();
        bool realtime = speed >= HeifEncodingSpeed.Level7;
        Av1EncoderOptions options = new(speed, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = realtime ? Av1RateControlMode.ConstantBitRate : Av1RateControlMode.Quality,
            MinimumQuantizer = realtime ? 46 : 0,
            MaximumQuantizer = realtime ? 54 : 63,
            LayerCount = 3,
            UsesFixedQuantizer = !realtime
        };

        int[] quantizers = [50, 40, 25];
        (int Numerator, int Denominator)[] scales = [(3, 4), (1, 2), (1, 1)];
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            image.Width,
            image.Height,
            CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv420),
            Av1QuantizationLookup.GetQIndex(quantizers[0]),
            options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        for (int layer = 0; layer < quantizers.Length; layer++)
        {
            int quantizer = quantizers[layer];
            (int numerator, int denominator) = scales[layer];
            sample.SetLength(0);
            encoder.EncodeLayer(
                image.Frames.RootFrame,
                sample,
                Av1QuantizationLookup.GetQIndex(quantizer),
                realtime ? quantizer - 4 : 0,
                realtime ? quantizer + 4 : 63,
                numerator,
                denominator);

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            ObuFrameHeader frameHeader = decoder.FrameHeader!;
            int width = ((image.Width * numerator) + denominator - 1) / denominator;
            int height = ((image.Height * numerator) + denominator - 1) / denominator;
            Assert.Equal(layer, frameHeader.SpatialId);
            Assert.Equal(width, frameHeader.FrameSize.FrameWidth);
            Assert.Equal(height, frameHeader.FrameSize.FrameHeight);
            AssertDecodedLumaMatchesEncoder(encoder, decoder, width, height);
        }
    }

    /// <summary>
    /// Creates a 96x64 gradient with fine texture for the layer and delta quantizer tests.
    /// </summary>
    /// <returns>The image.</returns>
    private static Image<Rgb24> CreateLayerTestImage()
    {
        Image<Rgb24> image = new(96, 64);
        for (int y = 0; y < image.Height; y++)
        {
            Span<Rgb24> row = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < image.Width; x++)
            {
                int value = ((x * 5) + (y * 3) + (((x * 7919) ^ (y * 104729)) & 15)) & 0xFF;
                row[x] = new Rgb24((byte)value, (byte)(255 - value), (byte)(value / 2));
            }
        }

        return image;
    }

    /// <summary>
    /// Asserts that the luma a decoder reconstructed for the last frame equals the luma the encoder keeps in the
    /// first reference slot the frame refreshed.
    /// </summary>
    /// <param name="encoder">The sequence encoder that coded the frame.</param>
    /// <param name="decoder">The decoder that decoded the frame.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    private static void AssertDecodedLumaMatchesEncoder(Av1FrameEncoder.SequenceEncoder encoder, Av1Decoder decoder, int width, int height)
    {
        int slot = BitOperations.TrailingZeroCount(decoder.FrameHeader!.RefreshFrameFlags);
        AssertLumaMatches(encoder.CopySlotLuma(slot), Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer), width, height);
    }

    private static void AssertLumaMatches(ushort[] expected, Av1FrameBuffer<byte> decoded, int width, int height)
    {
        Av1PlaneRegion<byte> luma = decoded.GetPlaneBuffer(Av1Plane.Y);
        Span<byte> samples = luma.Samples;
        for (int y = 0; y < height; y++)
        {
            // The decoder plane keeps a border, so the visible frame starts at the decoder origin.
            Span<byte> row = samples.Slice(((decoded.OriginY + y) * luma.Stride) + decoded.OriginX, width);
            for (int x = 0; x < width; x++)
            {
                if (expected[(y * width) + x] != row[x])
                {
                    Assert.Fail($"Luma differs at ({x}, {y}).");
                }
            }
        }
    }

    private static Av1EncoderOptions CreateSequenceOptions(HeifEncodingSpeed speed, int lagInFrames, int keyFrameInterval)
    {
        bool constantBitRate = speed >= HeifEncodingSpeed.Level7;
        return new Av1EncoderOptions(speed, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = constantBitRate ? Av1RateControlMode.ConstantBitRate : Av1RateControlMode.Quality,
            MinimumQuantizer = constantBitRate ? 21 : 0,
            MaximumQuantizer = constantBitRate ? 29 : 63,
            LagInFrames = lagInFrames,
            KeyFrameMaximumDistance = keyFrameInterval
        };
    }

    /// <summary>
    /// Creates a sequence of noise frames that cost far more bits than a bit budget gives them, so that coding under
    /// a budget overshoots and codes frames again. The noise is the XxHash32 of the sample position and the frame
    /// index, so every run produces the same frames.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="frameCount">The number of frames.</param>
    /// <returns>The sequence.</returns>
    internal static Image<Rgb24> CreateNoiseSequence(int width, int height, int frameCount)
        => CreateNoiseSequence(Configuration.Default, width, height, frameCount);

    /// <summary>
    /// Creates a sequence of noise frames, as <see cref="CreateNoiseSequence(int, int, int)"/> does, that allocates
    /// and encodes with the given configuration.
    /// </summary>
    /// <param name="configuration">The configuration of the image.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="frameCount">The number of frames.</param>
    /// <returns>The sequence.</returns>
    internal static Image<Rgb24> CreateNoiseSequence(Configuration configuration, int width, int height, int frameCount)
    {
        Image<Rgb24> image = new(configuration, width, height);
        Span<int> position = stackalloc int[3];
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            ImageFrame<Rgb24> frame = frameIndex == 0 ? image.Frames.RootFrame : image.Frames.CreateFrame();
            position[2] = frameIndex;
            for (int y = 0; y < height; y++)
            {
                Span<Rgb24> row = frame.PixelBuffer.DangerousGetRowSpan(y);
                position[1] = y;
                for (int x = 0; x < width; x++)
                {
                    // The top byte of the hash of the column, row and frame is the noise value, from 0 to 255.
                    position[0] = x;
                    int noise = (int)(XxHash32.HashToUInt32(MemoryMarshal.AsBytes(position)) >> 24);

                    // A diagonal gradient that moves one sample per frame, plus the noise. The noise spans the full
                    // sample range, so it hides the gradient, and the sum wraps to stay within one byte.
                    int value = (x + frameIndex + y + noise) & 255;
                    row[x] = new Rgb24((byte)value, (byte)(255 - value), (byte)(value / 2));
                }
            }
        }

        return image;
    }

    private static Image<Rgb24> CreatePanningSequence(int frameCount)
    {
        // A smooth gradient that moves one sample per frame, so no frame looks like a scene cut.
        const int Width = 64;
        const int Height = 48;
        Image<Rgb24> image = new(Width, Height);
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            ImageFrame<Rgb24> frame = frameIndex == 0 ? image.Frames.RootFrame : image.Frames.CreateFrame();
            for (int y = 0; y < Height; y++)
            {
                Span<Rgb24> row = frame.PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < Width; x++)
                {
                    int value = (2 * (x + frameIndex)) + y;
                    row[x] = new Rgb24((byte)value, (byte)(255 - value), (byte)(value / 2));
                }
            }
        }

        return image;
    }

    private static void VerifySequenceEncoderColorPlanes(
        int bitDepthValue,
        int colorFormatValue,
        HeifEncodingSpeed speed,
        int width,
        int height)
    {
        // Odd visible dimensions exercise the visible-edge clipping of every plane, while the three frames retain
        // the precision and subsampling of the native planes through key and inter coding.
        const int QIndex = 17;
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuColorConfig colorConfig = CreateColorConfig(bitDepth, colorFormat);
        using Image<Rgb48> photograph = LoadCrop<Rgb48>(TestImages.Png.CalliphoraPartial, new Rectangle(150, 120, width + 2, height + 2));
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default,
            width,
            height,
            colorConfig,
            QIndex,
            speed);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        bool hasMotion = false;
        bool hasFractionalChromaMotion = false;
        for (int frameIndex = 0; frameIndex < 3; frameIndex++)
        {
            // Each frame moves the window by one luma sample on each axis. Chroma is converted independently by
            // the production converter, so 4:2:0 and 4:2:2 cannot hide behind neutral planes.
            using Image<Rgb48> source = photograph.Clone(context => context.Crop(new Rectangle(frameIndex, frameIndex, width, height)));
            sample.SetLength(0);
            if (frameIndex == 0)
            {
                encoder.EncodeKeyFrame(source.Frames.RootFrame, sample);
            }
            else
            {
                Assert.False(encoder.EncodeNextFrame(source.Frames.RootFrame, sample, forceKeyFrame: false));
            }

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            Assert.True(Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader).EnableIntraEdgeFilter);
            Av1FrameBuffer<byte> decoded = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Assert.Equal(width, decoded.Width);
            Assert.Equal(height, decoded.Height);
            Assert.Equal(bitDepth, decoded.BitDepth);
            Av1FrameInfo decodedFrameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
            foreach (Av1BlockModeInfo mode in decodedFrameInfo.GetModeInfos(Point.Empty, decodedFrameInfo.GetModeInfoCount(Point.Empty)))
            {
                // The same ordinary-intra policy applies in key and inter frames. Read the emitted syntax,
                // rather than infer the skip flag from pixel agreement between encoder and decoder.
                if (mode.ReferenceFrames[0] == Av1ReferenceFrameType.Intra && !mode.UseIntraBlockCopy)
                {
                    Assert.False(mode.Skip);
                }

                if (mode.ReferenceFrames[0] is Av1ReferenceFrameType.Last or Av1ReferenceFrameType.Golden &&
                    mode.ReferenceFrames[1] == Av1ReferenceFrameType.None)
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
        }

        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
        Assert.Equal(ObuFrameType.InterFrame, frameHeader.FrameType);
        Assert.NotEqual(Av1InterpolationFilter.Bilinear, frameHeader.InterpolationFilter);

        Assert.True(hasMotion);
        if (colorConfig.SubSamplingX || colorConfig.SubSamplingY)
        {
            Assert.True(hasFractionalChromaMotion);
        }
    }

    [Fact]
    public void SequenceEncoderRejectsInvalidConversionBeforeAllocatingStorage()
    {
        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.TenBit, Av1ColorFormat.Yuv420);
        colorConfig.MatrixCoefficients = ObuMatrixCoefficients.YCgCoRe;
        Configuration configuration = Configuration.Default.Clone();
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        configuration.MemoryAllocator = allocator;

        // This internal factory receives resolved AV1 settings. The shared converter already rejects a
        // reversible matrix with subsampling; that rejection must occur before any owner can be stranded.
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
                configuration,
                32,
                32,
                colorConfig,
                17,
                speed: HeifEncodingSpeed.Level0);
        });

        Assert.Empty(allocator.AllocationLog);
        Assert.Empty(allocator.ReturnLog);
    }

    [Theory]
    [InlineData(false, EightBit)]
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
            ? Av1FrameEncoder.CreateAlphaSequenceEncoder(configuration, 32, 32, colorConfig, 17, speed: HeifEncodingSpeed.Level0)
            : Av1FrameEncoder.CreateColorSequenceEncoder(configuration, 32, 32, colorConfig, 17, speed: HeifEncodingSpeed.Level0))
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
                    ? Av1FrameEncoder.CreateAlphaSequenceEncoder(configuration, 32, 32, colorConfig, 17, speed: HeifEncodingSpeed.Level0)
                    : Av1FrameEncoder.CreateColorSequenceEncoder(configuration, 32, 32, colorConfig, 17, speed: HeifEncodingSpeed.Level0);
            });

            Assert.Equal("Sequence allocation failure.", exception.Message);
            Assert.Equal(failureIndex, allocator.AllocationLog.Count);
            Assert.All(
                allocator.AllocationLog,
                allocation => Assert.Single(allocator.ReturnLog, returned => returned.HashCodeOfBuffer == allocation.HashCodeOfBuffer));

            Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        }
    }

    [Theory]
    [InlineData(TenBit, 24, 16, HeifEncodingSpeed.Level0)]
    [InlineData(TwelveBit, 21, 77, HeifEncodingSpeed.Level9)]
    public void LosslessHighBitDepthEncodingPreservesNativePlanes(int bitDepthValue, int width, int height, HeifEncodingSpeed speed)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        using Image<Rgb48> source = LoadCrop<Rgb48>(TestImages.Png.Rgba64Bpp, new Rectangle(90, 70, width, height));

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
            1,
            lumaBorder: 64);

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
            speed);

        byte[] payload = stream.ToArray();

        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> actual = decoder.DecodeFrameBuffer(payload, null, null, out _);
        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);

        Assert.Equal(width, actual.Width);
        Assert.Equal(height, actual.Height);
        Assert.True(frameHeader.CodedLossless);
        Assert.True(frameHeader.AllLossless);
        Assert.Equal(Av1TransformMode.Only4x4, frameHeader.TransformMode);

        foreach (Av1Plane plane in new[] { Av1Plane.Y, Av1Plane.U, Av1Plane.V })
        {
            Av1PlaneRegion<ushort> expectedPlane = expected.Frame.View.GetPlane(plane);
            for (int row = 0; row < height; row++)
            {
                ReadOnlySpan<ushort> expectedRow = expectedPlane.GetRowSpan(row);
                Assert.Equal(expectedRow, actual.GetHighBitDepthRowSpan(plane, row, 0, 0));
            }
        }
    }

    /// <summary>
    /// Verifies that live partition search preserves lossless syntax across clipped parent nodes and superblocks.
    /// </summary>
    [Theory]
    [InlineData(77, 21, HeifEncodingSpeed.Level0)]
    [InlineData(21, 77, HeifEncodingSpeed.Level9)]
    public void EncodeLosslessPartitionSearchAcrossClippedSuperblocks(int width, int height, HeifEncodingSpeed speed)
    {
        using Image<L8> source = LoadCrop<L8>(TestImages.Jpeg.Baseline.GammaDalaiLamaGray, new Rectangle(60, 50, width, height));

        // Clipped parents must split from their own geometry instead of reading a stale position in the
        // original fixed-eight partition preorder.
        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.EightBit, Av1ColorFormat.Yuv400);
        colorConfig.ColorRange = true;
        using MemoryStream stream = new();
        Av1FrameEncoder.Encode(Configuration.Default, source.Frames.RootFrame, stream, colorConfig, qIndex: 0, speed);
        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decoded = decoder.DecodeFrameBuffer(payload, null, null, out _);
        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
        Av1PlaneRegion<byte> actual = decoded.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> expectedRow = MemoryMarshal.AsBytes(source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y));
            Assert.Equal(expectedRow, actual.GetRowSpan(y));
        }
    }

    [Theory]
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
            speed: HeifEncodingSpeed.Level0);

        byte[] payload = stream.ToArray();
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decodedPlanes = decoder.DecodeFrameBuffer(payload, null, null, out _);
        using Image<Rgba64> decoded = new(Configuration.Default, decodedPlanes.Width, decodedPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            decodedPlanes,
            decoded.Bounds,
            decoded.Frames.RootFrame.PixelBuffer.GetRegion(decoded.Bounds),
            decoded.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            decodedPlanes.ColorConfig.ColorRange);

        Assert.True(encodedHeader.ColorConfig.IsMonochrome);
        Assert.Equal(
            bitDepth == Av1BitDepth.TwelveBit ? ObuSequenceProfile.Professional : ObuSequenceProfile.Main,
            encodedHeader.SequenceProfile);

        Assert.Equal(new Size(Width, Height), decoded.Size);
        Assert.True(decoded[0, 0].R < decoded[Width - 1, Height - 1].R);
        Assert.Equal(decoded[0, 0].R, decoded[0, 0].G);
        Assert.Equal(decoded[0, 0].R, decoded[0, 0].B);
        Assert.Equal(ushort.MaxValue, decoded[0, 0].A);
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
            0,
            lumaBorder: 64);

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
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    // The profile occupies the upper three bits of the second byte. An 8x8 frame fits level 2.0, index zero,
    // which libaom's set_bitstream_level_tier infers from the frame size.
    [Theory]
    [InlineData(EightBit, Yuv400, 0x00, 0x1C)]
    [InlineData(TwelveBit, Yuv422, 0x40, 0x68)]
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
            speed: HeifEncodingSpeed.Level0);

        Av1CodecConfiguration configuration = new(sequenceHeader);
        byte[] fixedHeader = new byte[Av1CodecConfiguration.FixedHeaderSize];
        configuration.WriteFixedHeader(fixedHeader);

        Assert.Equal([0x81, expectedProfileAndLevel, expectedColorFlags, 0x00], fixedHeader);
        Av1CodecConfiguration parsed = new(fixedHeader, new DecoderOptions());
        Assert.True(configuration.HasMatchingImageConfiguration(parsed));
        parsed.Validate(sequenceHeader);
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
            0,
            lumaBorder: 64);

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
            0,
            lumaBorder: 64);

        ObuColorConfig colorConfig = CreateColorConfig(Av1BitDepth.TenBit);

        Av1FrameEncoder.PrepareSource(Configuration.Default, image.Frames.RootFrame, frameBuffer.Frame, colorConfig);

        ushort[] expected = [306, 601, 117, 1023];
        AssertReplicatedSingleRow(frameBuffer.Luma, border, expected);
    }

    [Theory]
    [InlineData(64)]
    public void ExtendBordersReplicatesEveryPhysicalPlaneEdge(int lumaBorder)
    {
        const int visibleWidth = 5;
        const int visibleHeight = 3;
        int chromaBorder = lumaBorder / 2;

        using Av1EncoderFrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            visibleWidth,
            visibleHeight,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1,
            lumaBorder);

        Av1PlaneRegion<byte> luma = frameBuffer.Luma;
        Av1PlaneRegion<byte> chromaBlue = frameBuffer.ChromaBlue;
        Av1PlaneRegion<byte> chromaRed = frameBuffer.ChromaRed;

        FillVisible(luma, lumaBorder, lumaBorder, visibleWidth, visibleHeight, 10);
        FillVisible(chromaBlue, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 80);
        FillVisible(chromaRed, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 120);

        frameBuffer.Frame.ExtendBorders();

        AssertReplicatedPlane(luma, lumaBorder, lumaBorder, visibleWidth, visibleHeight, 10);
        AssertReplicatedPlane(chromaBlue, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 80);
        AssertReplicatedPlane(chromaRed, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 120);
    }

    [Theory]
    [InlineData(64, 55_296)]
    public void FrameBufferUsesOneExactSizeOwnerForAllPlanes(int lumaBorder, int expectedLength)
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
            1,
            lumaBorder))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(expectedLength, allocation.Length);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
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
            speed: HeifEncodingSpeed.Level0);

        Assert.False(destination.CanSeek);
        Assert.NotEqual(0, storage.Length);
        TestMemoryAllocator.AllocationRequest tileOutput = Assert.Single(
            allocator.AllocationLog,
            allocation => allocation.ElementType == typeof(byte) && allocation.Length == ExpectedTileOutputLength);

        Assert.Equal(ExpectedTileOutputLength, tileOutput.Length);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.Equal(
            allocator.AllocationLog.Select(allocation => allocation.HashCodeOfBuffer).Order(),
            allocator.ReturnLog.Select(returned => returned.HashCodeOfBuffer).Order());
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

    /// <summary>
    /// Loads a shared test image and crops it to the requested rectangle.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to decode to.</typeparam>
    /// <param name="path">The test image path.</param>
    /// <param name="crop">The rectangle to keep.</param>
    /// <returns>The cropped image.</returns>
    private static Image<TPixel> LoadCrop<TPixel>(string path, Rectangle crop)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel> image = Image.Load<TPixel>(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, path));
        image.Mutate(context => context.Crop(crop));
        return image;
    }

    private static void FillVisible(Av1PlaneRegion<byte> plane, int originX, int originY, int width, int height, int seed)
    {
        for (int y = 0; y < height; y++)
        {
            Span<byte> row = plane.GetRowSpan(originY + y);
            for (int x = 0; x < width; x++)
            {
                row[originX + x] = (byte)(seed + (y * width) + x);
            }
        }
    }

    private static void AssertReplicatedPlane(Av1PlaneRegion<byte> plane, int originX, int originY, int width, int height, int seed)
    {
        for (int y = 0; y < plane.Height; y++)
        {
            ReadOnlySpan<byte> row = plane.GetRowSpan(y);
            int sourceY = Math.Clamp(y - originY, 0, height - 1);
            for (int x = 0; x < row.Length; x++)
            {
                int sourceX = Math.Clamp(x - originX, 0, width - 1);
                Assert.Equal((byte)(seed + (sourceY * width) + sourceX), row[x]);
            }
        }
    }

    private static void AssertReplicatedSingleRow<TSample>(
        Av1PlaneRegion<TSample> plane,
        int originX,
        ReadOnlySpan<TSample> expected)
        where TSample : unmanaged, IEquatable<TSample>
    {
        for (int y = 0; y < plane.Height; y++)
        {
            ReadOnlySpan<TSample> row = plane.GetRowSpan(y);
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

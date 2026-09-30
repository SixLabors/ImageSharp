// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
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

    [Fact]
    public void RectangularIntraReferencesExtendTheLastAvailableSample()
    {
        this.RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)EightBit, false, false, false);
        this.RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)TenBit, true, true, false);
        this.RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)EightBit, true, true, true);
        this.RectangularIntraReferencesExtendTheLastAvailableSampleCase((int)TwelveBit, false, true, true);
    }

    private void RectangularIntraReferencesExtendTheLastAvailableSampleCase(int bitDepthValue, bool transpose, bool extensionAvailable, bool limitedExtent)
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
                encoder.EncodeInterFrame(source.Frames.RootFrame, sample);
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

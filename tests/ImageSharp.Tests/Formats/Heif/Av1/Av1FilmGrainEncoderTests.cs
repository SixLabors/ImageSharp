// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the film grain the encoder signals from a preset or a table.
/// </summary>
[Trait("Format", "Avif")]
public class Av1FilmGrainEncoderTests
{
    /// <summary>
    /// A table with one entry from time 0 to one second, with lag 1 and two luma scaling points.
    /// </summary>
    private const string Table =
        "filmgrn1\n" +
        "E 0 10000000 1 1234 1\n" +
        "\tp 1 7 0 10 0 1 128 192 256 128 192 256\n" +
        "\tsY 2  0 48 255 48\n" +
        "\tsCb 0\n" +
        "\tsCr 0\n" +
        "\tcY 1 -2 3 -4\n" +
        "\tcCb 0 0 0 0 0\n" +
        "\tcCr 0 0 0 0 0\n";

    [Fact]
    public void StillImageSignalsThePresetGrain()
    {
        using Image<Rgb24> image = CreateGradient(32, 32, 0);
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level6, Av1Tuning.Psnr, enableRestoration: true, allIntra: true) { FilmGrainPreset = 1 };
        using MemoryStream stream = new();
        Av1FrameEncoder.Encode(Configuration.Default, image.Frames.RootFrame, stream, CreateColorConfig(), 100, options);

        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decoded = decoder.DecodeFrameBuffer(stream.ToArray(), null, null, out _, layeredImageIndex: null);
        Assert.True(decoder.SequenceHeader!.AreFilmGrainingParametersPresent);

        // Preset 1 has fourteen luma points, the seed 45231 and restricted range clipping.
        ObuFilmGrainParameters grain = decoder.FrameHeader!.FilmGrainParameters;
        Assert.True(grain.ApplyGrain);
        Assert.Equal(45231U, grain.GrainSeed);
        Assert.Equal(14U, grain.NumYPoints);
        Assert.Equal(178, grain.PointYValue[13]);
        Assert.True(grain.ClipToRestrictedRange);
    }

    [Fact]
    public void InterFramesReuseTheGrainOfAReference()
    {
        // Preset 5 does not update its parameters, so each inter frame names a reference slot and only signals its
        // next random seed.
        Av1EncoderOptions options = new(HeifEncodingSpeed.Level8, Av1Tuning.Ssim, enableRestoration: true, allIntra: false)
        {
            RateControlMode = Av1RateControlMode.ConstantBitRate,
            MinimumQuantizer = 21,
            MaximumQuantizer = 29,
            KeyFrameMaximumDistance = 9999,
            FilmGrainPreset = 5
        };

        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            Configuration.Default, 48, 32, CreateColorConfig(), 100, options);

        using Av1Decoder decoder = new(Configuration.Default);
        using MemoryStream sample = new();
        uint expectedSeed = 1063;
        for (int frameIndex = 0; frameIndex < 3; frameIndex++)
        {
            using Image<Rgb24> frame = CreateGradient(48, 32, frameIndex);
            sample.SetLength(0);
            if (frameIndex == 0)
            {
                encoder.EncodeKeyFrame(frame.Frames.RootFrame, sample);
            }
            else
            {
                encoder.EncodeNextFrame(frame.Frames.RootFrame, sample, forceKeyFrame: false);
            }

            decoder.DecodeSequenceReference(sample.ToArray(), null, null);
            ObuFilmGrainParameters grain = decoder.FrameHeader!.FilmGrainParameters;
            Assert.True(grain.ApplyGrain);
            Assert.Equal(expectedSeed, grain.GrainSeed);

            // An inter frame that names a slot decodes only because the slot holds the parameters. The decoder loads
            // every field from the slot, the update flag included, so the flag does not show the reuse.
            Assert.Equal(2U, grain.NumYPoints);
            Assert.Equal(9U, grain.NumCbPoints);

            // Each shown frame moves the seed on by 3381.
            expectedSeed = (expectedSeed + 3381) & 0xFFFF;
        }
    }

    [Fact]
    public void TableGivesTheGrainOfTimeZero()
    {
        Av1FilmGrainTable table = Av1FilmGrainTable.Parse(Table);
        ObuFilmGrainParameters grain = new();
        Assert.True(table.Lookup(0, grain));
        Assert.True(grain.ApplyGrain);
        Assert.Equal(1234U, grain.GrainSeed);
        Assert.Equal(1U, grain.ArCoeffLag);
        Assert.Equal(1U, grain.ArCoeffShiftMinus6);
        Assert.Equal(2U, grain.GrainScalingMinus8);
        Assert.Equal(2U, grain.NumYPoints);
        Assert.Equal(126, grain.ArCoeffsYPlus128[1]);
        Assert.Equal(128, grain.ArCoeffsYPlus128[4]);

        // A time outside every range clears the parameters.
        Assert.False(table.Lookup(10000000, grain));
        Assert.False(grain.ApplyGrain);
    }

    [Theory]
    [InlineData("grain1\nE 0 1 1 0 0\n")]
    [InlineData("filmgrn1\nE 0 1 1 0 1\n\tp 1 7 0 10 0 1\n")]
    [InlineData("filmgrn1\nE 0 1 1 0 1\n\tp 1 7 0 10 0 1 128 192 256 128 192 256\n\tsY 15\n")]
    [InlineData("filmgrn1\nE a b 1 0 0\n")]
    public void InvalidTableIsRejected(string text)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { FilmGrainTable = text });

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void PresetOutsideRangeIsRejected(int preset)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { FilmGrainPreset = preset });

    [Fact]
    public void AnimationWithTableRoundTrips()
    {
        using Image<Rgb24> image = CreateGradient(48, 32, 0);
        image.Frames.AddFrame(CreateGradient(48, 32, 1).Frames.RootFrame);

        using MemoryStream stream = new();
        image.Save(stream, new HeifEncoder { Quality = 80, Speed = HeifEncodingSpeed.Level9, FilmGrainTable = Table });

        stream.Position = 0;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(stream);
        Assert.Equal(2, decoded.Frames.Count);
    }

    /// <summary>
    /// Creates a gradient that moves one sample per frame.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="frameIndex">The frame, which shifts the gradient.</param>
    /// <returns>The image.</returns>
    private static Image<Rgb24> CreateGradient(int width, int height, int frameIndex)
    {
        Image<Rgb24> image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            Span<Rgb24> row = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                int value = (3 * (x + frameIndex)) + (2 * y);
                row[x] = new Rgb24((byte)value, (byte)(255 - value), (byte)(value / 2));
            }
        }

        return image;
    }

    /// <summary>
    /// Creates the limited range 8-bit 4:2:0 color configuration of the tests.
    /// </summary>
    /// <returns>The color configuration.</returns>
    private static ObuColorConfig CreateColorConfig()
        => new()
        {
            IsColorDescriptionPresent = true,
            ColorPrimaries = ObuColorPrimaries.Bt601,
            TransferCharacteristics = ObuTransferCharacteristics.Bt601,
            MatrixCoefficients = ObuMatrixCoefficients.Bt601,
            ColorRange = false,
            SubSamplingX = true,
            SubSamplingY = true,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            BitDepth = Av1BitDepth.EightBit
        };
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

public class Av1RateControlTests
{
    /// <summary>
    /// The requested quantizer of the tests, on the zero-through-63 scale.
    /// </summary>
    private const int Quantizer = 40;

    /// <summary>
    /// The variable-bitrate mode as a test argument, because a public test method cannot take an internal enum.
    /// </summary>
    private const int VariableBitRate = (int)Av1RateControlMode.VariableBitRate;

    /// <summary>
    /// The constant-bitrate mode as a test argument, because a public test method cannot take an internal enum.
    /// </summary>
    private const int ConstantBitRate = (int)Av1RateControlMode.ConstantBitRate;

    [Theory]
    [InlineData(VariableBitRate)]
    [InlineData(ConstantBitRate)]
    public void BitRateStillImageWithAmpleBudgetUsesTheLowestAllowedQuantizer(int modeValue)
    {
        // A 16x16 image needs far fewer bits than the key frame target, so the rate model picks the lowest quantizer
        // the four-step range allows.
        int qIndex = EncodeStillImage((Av1RateControlMode)modeValue, Quantizer - 4, Quantizer + 4);

        Assert.Equal(Av1QuantizationLookup.GetQIndex(Quantizer - 4), qIndex);
    }

    [Fact]
    public void ConstrainedQualityStillImageWithAmpleBudgetCodesBelowTheRequestedQuantizer()
    {
        // The constrained-quality mode keeps the full quantizer range, so the key frame floor sets the quantizer.
        int qIndex = EncodeStillImage(Av1RateControlMode.ConstrainedQuality, 0, 63);

        Assert.InRange(qIndex, 1, Av1QuantizationLookup.GetQIndex(Quantizer) - 1);
    }

    [Fact]
    public void ConstantQualityStillImageCodesAtTheRequestedQuantizer()
    {
        int qIndex = EncodeStillImage(Av1RateControlMode.Quality, 0, 63);

        Assert.Equal(Av1QuantizationLookup.GetQIndex(Quantizer), qIndex);
    }

    [Fact]
    public void BitRateStillImageQuantizerFollowsTheBudget()
    {
        int best = Av1QuantizationLookup.GetQIndex(Quantizer - 4);
        int worst = Av1QuantizationLookup.GetQIndex(Quantizer + 4);

        // A 1280x720 key frame gets 25 average frames in variable-bitrate mode.
        int variable = GetQIndex(1280, 720, Av1RateControlMode.VariableBitRate, best, worst, screenContent: false);
        Assert.InRange(variable, best, worst);

        // Screen content expects fewer bits at each quantizer, so it codes at a lower quantizer.
        int screen = GetQIndex(1280, 720, Av1RateControlMode.VariableBitRate, best, worst, screenContent: true);
        Assert.True(screen < variable);

        // The first constant-bitrate key frame gets half of the starting buffer, a larger target.
        int constant = GetQIndex(1280, 720, Av1RateControlMode.ConstantBitRate, best, worst, screenContent: false);
        Assert.True(constant < variable);

        // An 8K key frame overshoots the budget at every allowed quantizer and codes at the highest one.
        Assert.Equal(worst, GetQIndex(7680, 4320, Av1RateControlMode.VariableBitRate, best, worst, screenContent: false));
    }

    [Theory]
    [InlineData(VariableBitRate)]
    [InlineData(ConstantBitRate)]
    public void LosslessBitRateStillImageStaysLossless(int modeValue)
        => Assert.Equal(0, GetQIndex(1920, 1080, (Av1RateControlMode)modeValue, 0, 0, screenContent: false));

    /// <summary>
    /// Returns the quantizer index the rate model picks for a still image.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="mode">The rate control mode.</param>
    /// <param name="best">The lowest allowed quantizer index.</param>
    /// <param name="worst">The highest allowed quantizer index.</param>
    /// <param name="screenContent">Whether the image is screen content.</param>
    /// <returns>The quantizer index.</returns>
    private static int GetQIndex(int width, int height, Av1RateControlMode mode, int best, int worst, bool screenContent)
        => Av1RateControl.GetStillImageQIndex(width, height, Av1BitDepth.EightBit, HeifEncodingSpeed.Level6, mode, best, worst, screenContent);

    /// <summary>
    /// Encodes a 16x16 still image, decodes it, and returns the quantizer index of the decoded frame.
    /// </summary>
    /// <param name="mode">The rate control mode.</param>
    /// <param name="minimumQuantizer">The lowest quantizer on the zero-through-63 scale.</param>
    /// <param name="maximumQuantizer">The highest quantizer on the zero-through-63 scale.</param>
    /// <returns>The decoded base quantizer index.</returns>
    private static int EncodeStillImage(Av1RateControlMode mode, int minimumQuantizer, int maximumQuantizer)
    {
        using Image<Rgba32> source = Image.Load<Rgba32>(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Png.CalliphoraPartial));
        source.Mutate(context => context.Crop(new Rectangle(150, 120, 16, 16)));

        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            IsMonochrome = false,
            ColorPrimaries = ObuColorPrimaries.Bt601,
            TransferCharacteristics = ObuTransferCharacteristics.Bt601,
            MatrixCoefficients = ObuMatrixCoefficients.Bt601,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        Av1EncoderOptions options = new(HeifEncodingSpeed.Level9, Av1Tuning.Ssim, enableRestoration: true)
        {
            RateControlMode = mode,
            MinimumQuantizer = minimumQuantizer,
            MaximumQuantizer = maximumQuantizer
        };

        using MemoryStream stream = new();
        Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            Av1QuantizationLookup.GetQIndex(Quantizer),
            options);

        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decoded = decoder.DecodeFrameBuffer(stream.ToArray(), null, null, out _);
        Assert.Equal(16, decoded.Width);
        return decoder.FrameHeader.QuantizationParameters.BaseQIndex;
    }
}

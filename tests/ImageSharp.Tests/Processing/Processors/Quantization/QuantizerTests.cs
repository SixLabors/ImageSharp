// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization;

[Trait("Category", "Processors")]
public class QuantizerTests
{
    public static readonly string[] CommonTestImages =
    [
        TestImages.Png.CalliphoraPartial,
        TestImages.Png.Bike
    ];

    private static readonly QuantizerOptions NoDitherOptions = new() { Dither = null };
    private static readonly QuantizerOptions DiffuserDitherOptions = new() { Dither = KnownDitherings.FloydSteinberg };
    private static readonly QuantizerOptions OrderedDitherOptions = new() { Dither = KnownDitherings.Bayer8x8 };

    private static readonly QuantizerOptions Diffuser0_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.FloydSteinberg,
        DitherScale = 0F
    };

    private static readonly QuantizerOptions Diffuser0_25_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.FloydSteinberg,
        DitherScale = .25F
    };

    private static readonly QuantizerOptions Diffuser0_5_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.FloydSteinberg,
        DitherScale = .5F
    };

    private static readonly QuantizerOptions Diffuser0_75_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.FloydSteinberg,
        DitherScale = .75F
    };

    private static readonly QuantizerOptions Ordered0_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.Bayer8x8,
        DitherScale = 0F
    };

    private static readonly QuantizerOptions Ordered0_25_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.Bayer8x8,
        DitherScale = .25F
    };

    private static readonly QuantizerOptions Ordered0_5_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.Bayer8x8,
        DitherScale = .5F
    };

    private static readonly QuantizerOptions Ordered0_75_ScaleDitherOptions = new()
    {
        Dither = KnownDitherings.Bayer8x8,
        DitherScale = .75F
    };

    public static readonly TheoryData<IQuantizer> Quantizers
    = new()
    {
        // Known uses error diffusion by default.
        (IQuantizer)KnownQuantizers.Hexadecatree,
        (IQuantizer)KnownQuantizers.WebSafe,
        (IQuantizer)KnownQuantizers.Werner,
        (IQuantizer)KnownQuantizers.Wu,
        (IQuantizer)new HexadecatreeQuantizer(NoDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(NoDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(NoDitherOptions),
        (IQuantizer)new WuQuantizer(NoDitherOptions),
        (IQuantizer)new HexadecatreeQuantizer(OrderedDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(OrderedDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(OrderedDitherOptions),
        (IQuantizer)new WuQuantizer(OrderedDitherOptions)
    };

    public static readonly TheoryData<IQuantizer> DitherScaleQuantizers
    = new()
    {
        (IQuantizer)new HexadecatreeQuantizer(Diffuser0_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Diffuser0_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Diffuser0_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Diffuser0_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(Diffuser0_25_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Diffuser0_25_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Diffuser0_25_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Diffuser0_25_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(Diffuser0_5_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Diffuser0_5_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Diffuser0_5_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Diffuser0_5_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(Diffuser0_75_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Diffuser0_75_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Diffuser0_75_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Diffuser0_75_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(DiffuserDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(DiffuserDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(DiffuserDitherOptions),
        (IQuantizer)new WuQuantizer(DiffuserDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(Ordered0_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Ordered0_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Ordered0_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Ordered0_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(Ordered0_25_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Ordered0_25_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Ordered0_25_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Ordered0_25_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(Ordered0_5_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Ordered0_5_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Ordered0_5_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Ordered0_5_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(Ordered0_75_ScaleDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(Ordered0_75_ScaleDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(Ordered0_75_ScaleDitherOptions),
        (IQuantizer)new WuQuantizer(Ordered0_75_ScaleDitherOptions),

        (IQuantizer)new HexadecatreeQuantizer(OrderedDitherOptions),
        (IQuantizer)new WebSafePaletteQuantizer(OrderedDitherOptions),
        (IQuantizer)new WernerPaletteQuantizer(OrderedDitherOptions),
        (IQuantizer)new WuQuantizer(OrderedDitherOptions),
    };

    public static readonly TheoryData<IDither> DefaultInstanceDitherers
        = new()
        {
            (IDither)default(ErrorDither),
            (IDither)default(OrderedDither)
        };

    private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05F);

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), nameof(Quantizers), PixelTypes.Rgba32)]
    public void ApplyQuantizationInBox<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        string quantizerName = quantizer.GetType().Name;
        string ditherName = quantizer.Options.Dither?.GetType()?.Name ?? "NoDither";
        string testOutputDetails = $"{quantizerName}_{ditherName}";

        provider.RunRectangleConstrainedValidatingProcessorTest(
            (x, rect) => x.Quantize(quantizer, rect),
            testOutputDetails: testOutputDetails,
            comparer: ValidatorComparer,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), nameof(Quantizers), PixelTypes.Rgba32)]
    public void ApplyQuantization<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        string quantizerName = quantizer.GetType().Name;
        string ditherName = quantizer.Options.Dither?.GetType()?.Name ?? "NoDither";
        string testOutputDetails = $"{quantizerName}_{ditherName}";

        provider.RunValidatingProcessorTest(
            x => x.Quantize(quantizer),
            comparer: ValidatorComparer,
            testOutputDetails: testOutputDetails,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Png.David, nameof(DitherScaleQuantizers), PixelTypes.Rgba32)]
    public void ApplyQuantizationWithDitheringScale<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        string quantizerName = quantizer.GetType().Name;
        string ditherName = quantizer.Options.Dither.GetType().Name;
        float ditherScale = quantizer.Options.DitherScale;
        string testOutputDetails = FormattableString.Invariant($"{quantizerName}_{ditherName}_{ditherScale}");

        provider.RunValidatingProcessorTest(
            x => x.Quantize(quantizer),
            comparer: ValidatorComparer,
            testOutputDetails: testOutputDetails,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [MemberData(nameof(DefaultInstanceDitherers))]
    public void ShouldThrowForDefaultDitherInstance(IDither dither)
    {
        void Command()
        {
            using Image<Rgba32> image = new(10, 10);
            WebSafePaletteQuantizer quantizer = new();
            quantizer.Options.Dither = dither;
            image.Mutate(x => x.Quantize(quantizer));
        }

        Assert.Throws<ImageProcessingException>(Command);
    }
}

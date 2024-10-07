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
        KnownQuantizers.Octree,
        KnownQuantizers.WebSafe,
        KnownQuantizers.Werner,
        KnownQuantizers.Wu,
        new OctreeQuantizer(NoDitherOptions),
        new WebSafePaletteQuantizer(NoDitherOptions),
        new WernerPaletteQuantizer(NoDitherOptions),
        new WuQuantizer(NoDitherOptions),
        new OctreeQuantizer(OrderedDitherOptions),
        new WebSafePaletteQuantizer(OrderedDitherOptions),
        new WernerPaletteQuantizer(OrderedDitherOptions),
        new WuQuantizer(OrderedDitherOptions)
    };

    public static readonly TheoryData<IQuantizer> DitherScaleQuantizers
    = new()
    {
        new OctreeQuantizer(Diffuser0_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Diffuser0_ScaleDitherOptions),
        new WernerPaletteQuantizer(Diffuser0_ScaleDitherOptions),
        new WuQuantizer(Diffuser0_ScaleDitherOptions),

        new OctreeQuantizer(Diffuser0_25_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Diffuser0_25_ScaleDitherOptions),
        new WernerPaletteQuantizer(Diffuser0_25_ScaleDitherOptions),
        new WuQuantizer(Diffuser0_25_ScaleDitherOptions),

        new OctreeQuantizer(Diffuser0_5_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Diffuser0_5_ScaleDitherOptions),
        new WernerPaletteQuantizer(Diffuser0_5_ScaleDitherOptions),
        new WuQuantizer(Diffuser0_5_ScaleDitherOptions),

        new OctreeQuantizer(Diffuser0_75_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Diffuser0_75_ScaleDitherOptions),
        new WernerPaletteQuantizer(Diffuser0_75_ScaleDitherOptions),
        new WuQuantizer(Diffuser0_75_ScaleDitherOptions),

        new OctreeQuantizer(DiffuserDitherOptions),
        new WebSafePaletteQuantizer(DiffuserDitherOptions),
        new WernerPaletteQuantizer(DiffuserDitherOptions),
        new WuQuantizer(DiffuserDitherOptions),

        new OctreeQuantizer(Ordered0_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Ordered0_ScaleDitherOptions),
        new WernerPaletteQuantizer(Ordered0_ScaleDitherOptions),
        new WuQuantizer(Ordered0_ScaleDitherOptions),

        new OctreeQuantizer(Ordered0_25_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Ordered0_25_ScaleDitherOptions),
        new WernerPaletteQuantizer(Ordered0_25_ScaleDitherOptions),
        new WuQuantizer(Ordered0_25_ScaleDitherOptions),

        new OctreeQuantizer(Ordered0_5_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Ordered0_5_ScaleDitherOptions),
        new WernerPaletteQuantizer(Ordered0_5_ScaleDitherOptions),
        new WuQuantizer(Ordered0_5_ScaleDitherOptions),

        new OctreeQuantizer(Ordered0_75_ScaleDitherOptions),
        new WebSafePaletteQuantizer(Ordered0_75_ScaleDitherOptions),
        new WernerPaletteQuantizer(Ordered0_75_ScaleDitherOptions),
        new WuQuantizer(Ordered0_75_ScaleDitherOptions),

        new OctreeQuantizer(OrderedDitherOptions),
        new WebSafePaletteQuantizer(OrderedDitherOptions),
        new WernerPaletteQuantizer(OrderedDitherOptions),
        new WuQuantizer(OrderedDitherOptions),
    };

    public static readonly TheoryData<IDither> DefaultInstanceDitherers
        = new()
        {
            default(ErrorDither),
            default(OrderedDither)
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

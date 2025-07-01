// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public class SpectralToPixelConversionTests
{
    public static readonly string[] BaselineTestJpegs =
    {
        TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk, TestImages.Jpeg.Baseline.Jpeg400,
        TestImages.Jpeg.Baseline.Jpeg444, TestImages.Jpeg.Baseline.Testorig420,
        TestImages.Jpeg.Baseline.Jpeg420Small, TestImages.Jpeg.Baseline.Bad.BadEOF,
        TestImages.Jpeg.Baseline.MultiScanBaselineCMYK
    };

    public SpectralToPixelConversionTests(ITestOutputHelper output) => this.Output = output;

    private ITestOutputHelper Output { get; }

    [Theory]
    [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
    public void Decoder_PixelBufferComparison<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Stream
        byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;
        using MemoryStream ms = new(sourceBytes);
        using BufferedReadStream bufferedStream = new(Configuration.Default, ms);

        // Decoding
        JpegDecoderOptions options = new();
        using SpectralConverter<TPixel> converter = new(Configuration.Default);
        using JpegDecoderCore decoder = new(options);
        HuffmanScanDecoder scanDecoder = new(bufferedStream, converter, cancellationToken: default);
        decoder.ParseStream(bufferedStream, converter, cancellationToken: default);

        // Test metadata
        provider.Utility.TestGroupName = nameof(JpegDecoderTests);
        provider.Utility.TestName = JpegDecoderTests.DecodeBaselineJpegOutputName;

        // Comparison
        using Image<TPixel> image = new(Configuration.Default, converter.GetPixelBuffer(null, CancellationToken.None), new ImageMetadata());
        using Image<TPixel> referenceImage = provider.GetReferenceOutputImage<TPixel>(appendPixelTypeToFileName: false);
        ImageSimilarityReport report = ImageComparer.Exact.CompareImagesOrFrames(referenceImage, image);

        this.Output.WriteLine($"*** {provider.SourceFileOrDescription} ***");
        this.Output.WriteLine($"Difference: {report.DifferencePercentageString}");

        // ReSharper disable once PossibleInvalidOperationException
        Assert.True(report.TotalNormalizedDifference.Value < 0.005f);
    }
}

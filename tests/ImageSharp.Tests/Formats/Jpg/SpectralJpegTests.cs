// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public class SpectralJpegTests
{
    public SpectralJpegTests(ITestOutputHelper output) => this.Output = output;

    private ITestOutputHelper Output { get; }

    public static readonly string[] BaselineTestJpegs =
        {
            TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk, TestImages.Jpeg.Baseline.Jpeg400,
            TestImages.Jpeg.Baseline.Jpeg444, TestImages.Jpeg.Baseline.Testorig420,
            TestImages.Jpeg.Baseline.Jpeg420Small, TestImages.Jpeg.Baseline.Bad.BadEOF,
            TestImages.Jpeg.Baseline.MultiScanBaselineCMYK
        };

    public static readonly string[] ProgressiveTestJpegs =
        {
            TestImages.Jpeg.Progressive.Fb, TestImages.Jpeg.Progressive.Progress,
            TestImages.Jpeg.Progressive.Festzug, TestImages.Jpeg.Progressive.Bad.BadEOF,
            TestImages.Jpeg.Progressive.Bad.ExifUndefType,
        };

    public static readonly string[] AllTestJpegs = BaselineTestJpegs.Concat(ProgressiveTestJpegs).ToArray();

    [Theory(Skip = "Debug only, enable manually!")]
    [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
    public void Decoder_ParseStream_SaveSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Calculating data from ImageSharp
        byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;
        JpegDecoderOptions option = new();

        using var decoder = new JpegDecoderCore(option);
        using var ms = new MemoryStream(sourceBytes);
        using var bufferedStream = new BufferedReadStream(Configuration.Default, ms);

        // internal scan decoder which we substitute to assert spectral correctness
        var debugConverter = new DebugSpectralConverter<TPixel>();
        var scanDecoder = new HuffmanScanDecoder(bufferedStream, debugConverter, cancellationToken: default);

        // This would parse entire image
        decoder.ParseStream(bufferedStream, debugConverter, cancellationToken: default);
        VerifyJpeg.SaveSpectralImage(provider, debugConverter.SpectralData);
    }

    [Theory]
    [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
    public void VerifySpectralCorrectness<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!TestEnvironment.IsWindows)
        {
            return;
        }

        // Expected data from libjpeg
        LibJpegTools.SpectralData libJpegData = LibJpegTools.ExtractSpectralData(provider.SourceFileOrDescription);

        // Calculating data from ImageSharp
        byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;
        JpegDecoderOptions options = new();

        using var decoder = new JpegDecoderCore(options);
        using var ms = new MemoryStream(sourceBytes);
        using var bufferedStream = new BufferedReadStream(Configuration.Default, ms);

        // internal scan decoder which we substitute to assert spectral correctness
        var debugConverter = new DebugSpectralConverter<TPixel>();

        // This would parse entire image
        decoder.ParseStream(bufferedStream, debugConverter, cancellationToken: default);

        // Actual verification
        this.VerifySpectralCorrectnessImpl(libJpegData, debugConverter.SpectralData);
    }

    private void VerifySpectralCorrectnessImpl(
        LibJpegTools.SpectralData libJpegData,
        LibJpegTools.SpectralData imageSharpData)
    {
        bool equality = libJpegData.Equals(imageSharpData);
        this.Output.WriteLine("Spectral data equality: " + equality);

        int componentCount = imageSharpData.ComponentCount;
        if (libJpegData.ComponentCount != componentCount)
        {
            throw new Exception("libJpegData.ComponentCount != componentCount");
        }

        double averageDifference = 0;
        double totalDifference = 0;
        double tolerance = 0;

        this.Output.WriteLine("*** Differences ***");
        for (int i = 0; i < componentCount; i++)
        {
            LibJpegTools.ComponentData libJpegComponent = libJpegData.Components[i];
            LibJpegTools.ComponentData imageSharpComponent = imageSharpData.Components[i];

            (double total, double average) = LibJpegTools.CalculateDifference(libJpegComponent, imageSharpComponent);

            this.Output.WriteLine($"Component{i}: [total: {total} | average: {average}]");
            averageDifference += average;
            totalDifference += total;
            Size s = libJpegComponent.SpectralBlocks.Size();
            tolerance += s.Width * s.Height;
        }

        averageDifference /= componentCount;

        tolerance /= 64; // fair enough?

        this.Output.WriteLine($"AVERAGE: {averageDifference}");
        this.Output.WriteLine($"TOTAL: {totalDifference}");
        this.Output.WriteLine($"TOLERANCE = totalNumOfBlocks / 64 = {tolerance}");

        Assert.True(totalDifference < tolerance);
    }

    private class DebugSpectralConverter<TPixel> : SpectralConverter
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private JpegFrame frame;

        private IRawJpegData jpegData;

        private LibJpegTools.SpectralData spectralData;

        private int baselineScanRowCounter;

        public LibJpegTools.SpectralData SpectralData
        {
            get
            {
                // Due to underlying architecture, baseline interleaved jpegs would inject spectral data during parsing
                // Progressive and multi-scan images must be loaded manually
                if (this.frame.Progressive || !this.frame.Interleaved)
                {
                    this.PrepareForDecoding();
                    LibJpegTools.ComponentData[] components = this.spectralData.Components;
                    for (int i = 0; i < components.Length; i++)
                    {
                        components[i].LoadSpectral(this.frame.Components[i]);
                    }
                }

                return this.spectralData;
            }
        }

        public override void ConvertStrideBaseline(IccProfile iccProfile)
        {
            // This would be called only for baseline non-interleaved images
            // We must copy spectral strides here
            LibJpegTools.ComponentData[] components = this.spectralData.Components;
            for (int i = 0; i < components.Length; i++)
            {
                components[i].LoadSpectralStride(this.frame.Components[i].SpectralBlocks, this.baselineScanRowCounter);
            }

            this.baselineScanRowCounter++;

            // As spectral buffers are reused for each stride decoding - we need to manually clear it like it's done in SpectralConverter<TPixel>
            foreach (JpegComponent component in this.frame.Components)
            {
                Buffer2D<Block8x8> spectralBlocks = component.SpectralBlocks;
                for (int i = 0; i < spectralBlocks.Height; i++)
                {
                    spectralBlocks.DangerousGetRowSpan(i).Clear();
                }
            }
        }

        public override bool HasPixelBuffer() => throw new NotImplementedException();

        public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
        {
            this.frame = frame;
            this.jpegData = jpegData;
        }

        public override void PrepareForDecoding()
        {
            var spectralComponents = new LibJpegTools.ComponentData[this.frame.ComponentCount];
            for (int i = 0; i < spectralComponents.Length; i++)
            {
                JpegComponent component = this.frame.Components[i];
                spectralComponents[i] = new LibJpegTools.ComponentData(component.WidthInBlocks, component.HeightInBlocks, component.Index);
            }

            this.spectralData = new LibJpegTools.SpectralData(spectralComponents);
        }
    }
}

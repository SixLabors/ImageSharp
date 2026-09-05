// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures encoding a photographic sequence with fractional motion at the interpolation-search effort boundaries.
/// </summary>
[MemoryDiagnoser]
public class Av1SequenceEncoderBenchmarks
{
    /// <summary>
    /// The number of displayed pictures in each independently encoded sequence.
    /// </summary>
    private const int FrameCount = 3;

    /// <summary>
    /// The native AV1 quantizer index corresponding to libaom's public constant-quality level 30.
    /// </summary>
    private const int QIndex = 120;

    /// <summary>
    /// The fixed native speed baseline, independent of ImageSharp's effort scale.
    /// </summary>
    private const int NativeCpuUsed = 6;

    /// <summary>
    /// The native public quantizer corresponding to <see cref="QIndex"/>, also used for both rate-control bounds.
    /// </summary>
    private const int NativeQuality = 30;

    private Image<Rgb24> sequence;
    private Configuration configuration;
    private ObuColorConfig colorConfig;
    private MemoryStream output;
    private string outputDirectory;

    /// <summary>
    /// Gets or sets the square frame dimension.
    /// </summary>
    [Params(256, 512)]
    public int Dimension { get; set; }

    /// <summary>
    /// Gets or sets the effort controlling fixed, common switchable, or independently switchable filters.
    /// </summary>
    [Params(7, 8, 9)]
    public int Effort { get; set; }

    /// <summary>
    /// Prepares identical photographic RGB frames and a planar source file for checking reconstructed output quality.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.configuration = Configuration.Default.Clone();
        this.configuration.MaxDegreeOfParallelism = 1;
        this.colorConfig = new ObuColorConfig
        {
            BitDepth = Av1BitDepth.EightBit,
            IsColorDescriptionPresent = true,
            ColorPrimaries = ObuColorPrimaries.Bt601,
            TransferCharacteristics = ObuTransferCharacteristics.Bt601,
            MatrixCoefficients = ObuMatrixCoefficients.Bt601,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown
        };

        this.output = new MemoryStream();
        this.outputDirectory = TestEnvironment.CreateOutputDirectory("Heif", "Av1", nameof(Av1SequenceEncoderBenchmarks));
        string inputPath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Png.Bike);
        using Image<Rgb24> photograph = Image.Load<Rgb24>(inputPath);

        // Leave a source margin for the half-pixel translations. Resampling is setup work, not encoder time;
        // every invocation consumes the same three images rather than repeatedly translating a previous result.
        photograph.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(this.Dimension + FrameCount, this.Dimension + FrameCount),
            Mode = ResizeMode.Crop
        }));

        Rectangle sourceBounds = new(0, 0, photograph.Width, photograph.Height);
        Size targetSize = new(this.Dimension, this.Dimension);
        this.sequence = photograph.Clone(context => context.Crop(new Rectangle(Point.Empty, targetSize)));
        for (int frameIndex = 1; frameIndex < FrameCount; frameIndex++)
        {
            Matrix3x2 translation = Matrix3x2.CreateTranslation(-0.5F * frameIndex, -0.5F * frameIndex);
            using Image<Rgb24> translated = photograph.Clone(context =>
                context.Transform(sourceBounds, translation, targetSize, KnownResamplers.Bicubic));

            this.sequence.Frames.AddFrame(translated.Frames.RootFrame);
        }

        // Export the production-converted source planes only for checking reconstructed output quality.
        // Neither timed encoder reads this file: both convert the original RGB frames during each operation.
        using Av1EncoderFrameBuffer<byte> planar = new(this.configuration, this.Dimension, this.Dimension, 8, Av1ColorFormat.Yuv420, 0, 0);
        using FileStream raw = File.Create(Path.Combine(this.outputDirectory, $"bike-{this.Dimension}-3frames.source.yuv"));
        foreach (ImageFrame<Rgb24> frame in this.sequence.Frames)
        {
            Av1FrameEncoder.PrepareSource(this.configuration, frame, planar.Frame, this.colorConfig);
            for (int planeIndex = 0; planeIndex < this.colorConfig.PlaneCount; planeIndex++)
            {
                Buffer2DRegion<byte> plane = planar.Frame.View.GetPlane((Av1Plane)planeIndex);
                for (int y = 0; y < plane.Height; y++)
                {
                    raw.Write(plane.DangerousGetRowSpan(y));
                }
            }
        }
    }

    /// <summary>
    /// Encodes one key picture and two dependent pictures, returning the complete OBU payload length.
    /// </summary>
    /// <returns>The encoded sequence length.</returns>
    [Benchmark]
    public long ImageSharp()
    {
        this.output.SetLength(0);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            this.configuration, this.Dimension, this.Dimension, this.colorConfig, QIndex, this.Effort);

        // One operation owns the real sequence lifetime: allocation, conversion, key/inter coding, and disposal.
        // The caller's destination is reused, excluding filesystem and MemoryStream growth from steady-state timing.
        encoder.EncodeKeyFrame(this.sequence.Frames.RootFrame, this.output);
        for (int frameIndex = 1; frameIndex < FrameCount; frameIndex++)
        {
            encoder.EncodeInterFrame(this.sequence.Frames[frameIndex], this.output);
        }

        return this.output.Length;
    }

    /// <summary>
    /// Encodes the same RGB sequence with current-main libaom, including conversion, allocation, output, and disposal.
    /// </summary>
    /// <returns>The encoded sequence length.</returns>
    [Benchmark(Baseline = true)]
    public long Libaom()
    {
        // cpu-used is a separate speed scale, not an ImageSharp effort mapping. Keep the reference at
        // good-quality speed six while comparing the three managed interpolation-search boundaries.
        this.output.SetLength(0);
        using LibaomBenchmarkEncoder encoder = LibaomBenchmarkEncoder.Open(this.Dimension, this.Dimension, NativeQuality, NativeCpuUsed);
        using Av1EncoderFrameBuffer<byte> planar = new(this.configuration, this.Dimension, this.Dimension, 8, Av1ColorFormat.Yuv420, 0, 0);
        using Av1FrameEncoder.Av1EncoderConversionWorkspace conversion = new(this.configuration, this.Dimension, this.colorConfig, false, false);
        Rectangle bounds = new(0, 0, this.Dimension, this.Dimension);
        for (int frameIndex = 0; frameIndex < FrameCount; frameIndex++)
        {
            // Conversion belongs inside both measured paths. Reuse the same row workspace and SIMD converter
            // as the managed sequence encoder, writing directly into the planes passed to native libaom.
            conversion.Convert<Rgb24, Av1EncoderFrame<byte>.PlanarView, byte, HeifByteSampleConverter>(
                this.configuration, this.sequence.Frames[frameIndex], bounds, planar.Frame.View);

            encoder.Encode(planar.Frame, frameIndex, this.output);
        }

        encoder.Finish(this.output);
        return this.output.Length;
    }

    /// <summary>
    /// Retains the measured managed encoder output and releases the input images and destination stream.
    /// </summary>
    [GlobalCleanup(Target = nameof(ImageSharp))]
    public void CleanupImageSharp() => this.Cleanup($"bike-{this.Dimension}-q{QIndex}-effort{this.Effort}.obu");

    /// <summary>
    /// Retains the measured reference encoder output and releases the input images and destination stream.
    /// </summary>
    [GlobalCleanup(Target = nameof(Libaom))]
    public void CleanupLibaom() => this.Cleanup($"bike-{this.Dimension}-q{QIndex}-libaom-cpu{NativeCpuUsed}.obu");

    /// <summary>
    /// Writes the measured payload without another encoding pass and releases the shared benchmark resources.
    /// </summary>
    /// <param name="outputName">The codec-specific payload file name.</param>
    private void Cleanup(string outputName)
    {
        // Output validation and quality measurement use the actual measured payload, with no encode or decode
        // hidden inside the timed operation and no file-sized ToArray copy.
        using FileStream encoded = File.Create(Path.Combine(this.outputDirectory, outputName));
        this.output.Position = 0;
        this.output.CopyTo(encoded);
        this.output.Dispose();
        this.sequence.Dispose();
    }
}

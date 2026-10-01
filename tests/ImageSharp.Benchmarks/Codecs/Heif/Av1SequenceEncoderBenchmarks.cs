// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Compares complete managed and native RGB-to-AV1 sequence encoding at equivalent conversion and output boundaries.
/// </summary>
[MemoryDiagnoser]
public class Av1SequenceEncoderBenchmarks
{
    private const int ManagedQIndex = 120;
    private const int NativeQuantizer = 30;
    private const int NativeSpeed = 6;
    private const int FrameCount = 3;

    private Configuration configuration;
    private Image<Rgb24>[] frames;
    private MemoryStream imageSharpOutput;
    private MemoryStream libaomOutput;
    private ObuColorConfig colorConfig;

    /// <summary>
    /// Gets or sets the square frame dimension.
    /// </summary>
    [Params(256, 512)]
    public int Dimension { get; set; }

    /// <summary>
    /// Loads and prepares the source sequence, then verifies both encoded payloads through managed and native decoders.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.configuration = Configuration.Default.Clone();
        this.configuration.MaxDegreeOfParallelism = 1;
        this.imageSharpOutput = new MemoryStream();
        this.libaomOutput = new MemoryStream();
        this.colorConfig = new ObuColorConfig
        {
            IsColorDescriptionPresent = true,
            ColorPrimaries = ObuColorPrimaries.Bt601,
            TransferCharacteristics = ObuTransferCharacteristics.Bt601,
            MatrixCoefficients = ObuMatrixCoefficients.Bt601,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            BitDepth = Av1BitDepth.EightBit
        };

        string sourcePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Png.Bike);
        using Image<Rgb24> source = Image.Load<Rgb24>(sourcePath);
        Image<Rgb24> first = source.Clone(context => context.Resize(this.Dimension, this.Dimension));
        Rectangle bounds = new(0, 0, this.Dimension, this.Dimension);
        Image<Rgb24> second = first.Clone(
            context => context.Transform(bounds, Matrix3x2.CreateTranslation(0.5F, 0.5F), first.Size, KnownResamplers.Bicubic));

        Image<Rgb24> third = first.Clone(
            context => context.Transform(bounds, Matrix3x2.CreateTranslation(1F, 1F), first.Size, KnownResamplers.Bicubic));

        this.frames = [first, second, third];

        this.ImageSharp();
        ValidatePayload(this.configuration, this.imageSharpOutput);
        this.Libaom();
        ValidatePayload(this.configuration, this.libaomOutput);
    }

    /// <summary>
    /// Releases retained source images and reusable output streams.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (this.frames is not null)
        {
            foreach (Image<Rgb24> frame in this.frames)
            {
                frame.Dispose();
            }
        }

        this.imageSharpOutput?.Dispose();
        this.libaomOutput?.Dispose();
    }

    /// <summary>
    /// Encodes all source frames through the managed AV1 sequence encoder.
    /// </summary>
    /// <returns>The complete OBU sequence length.</returns>
    [Benchmark]
    public long ImageSharp()
    {
        Reset(this.imageSharpOutput);
        using Av1FrameEncoder.SequenceEncoder encoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            this.configuration,
            this.Dimension,
            this.Dimension,
            this.colorConfig,
            ManagedQIndex,
            HeifEncodingSpeed.Level6);

        encoder.EncodeKeyFrame(this.frames[0].Frames.RootFrame, this.imageSharpOutput);
        for (int i = 1; i < FrameCount; i++)
        {
            encoder.EncodeNextFrame(this.frames[i].Frames.RootFrame, this.imageSharpOutput, forceKeyFrame: false);
        }

        return this.imageSharpOutput.Length;
    }

    /// <summary>
    /// Converts and encodes all source frames through the optimized reference encoder.
    /// </summary>
    /// <returns>The complete OBU sequence length.</returns>
    [Benchmark(Baseline = true)]
    public long Libaom()
    {
        Reset(this.libaomOutput);
        using Av1EncoderFrameBuffer<byte> source = new(
            this.configuration,
            this.Dimension,
            this.Dimension,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1,
            0);

        using LibaomBenchmarkEncoder encoder = LibaomBenchmarkEncoder.Open(
            this.Dimension,
            this.Dimension,
            NativeQuantizer,
            NativeSpeed);

        for (int i = 0; i < FrameCount; i++)
        {
            Av1FrameEncoder.PrepareSource(this.configuration, this.frames[i].Frames.RootFrame, source.Frame, this.colorConfig);
            encoder.Encode(source.Frame, i, this.libaomOutput);
        }

        encoder.Finish(this.libaomOutput);
        return this.libaomOutput.Length;
    }

    /// <summary>
    /// Resets a retained output stream without changing its capacity.
    /// </summary>
    /// <param name="stream">The stream to reset.</param>
    private static void Reset(MemoryStream stream)
    {
        stream.Position = 0;
        stream.SetLength(0);
    }

    /// <summary>
    /// Requires managed and native decoders to produce the same final RGB presentation for one encoded sequence.
    /// </summary>
    /// <param name="configuration">The single-threaded benchmark configuration.</param>
    /// <param name="stream">The complete encoded OBU sequence.</param>
    private static void ValidatePayload(Configuration configuration, MemoryStream stream)
    {
        if (!stream.TryGetBuffer(out ArraySegment<byte> segment))
        {
            throw new InvalidOperationException("The benchmark output stream does not expose its retained buffer.");
        }

        Span<byte> payload = segment.AsSpan(0, checked((int)stream.Length));
        using Av1Decoder managedDecoder = new(configuration);
        using Av1FrameBuffer<byte> managedPlanes = managedDecoder.DecodeFrameBuffer(payload, null, null, out _);
        using ImageFrame<Rgb48> managed = new(configuration, managedPlanes.Width, managedPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            configuration,
            managedPlanes,
            managed.Bounds,
            managed.PixelBuffer.GetRegion(managed.Bounds),
            managed.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            managedPlanes.ColorConfig.ColorRange);

        using LibaomBenchmarkDecoder nativeDecoder = LibaomBenchmarkDecoder.Open();
        using ImageFrame<Rgb48> native = nativeDecoder.Decode(configuration, payload);
        if (managed.Size != native.Size)
        {
            throw new InvalidOperationException("The managed and native decoders returned different presentation dimensions.");
        }

        for (int y = 0; y < managed.Height; y++)
        {
            if (!managed.PixelBuffer.DangerousGetRowSpan(y).SequenceEqual(native.PixelBuffer.DangerousGetRowSpan(y)))
            {
                throw new InvalidOperationException($"The managed and native decoders disagree on packed RGB row {y}.");
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures frame-wide AV1 YUV 4:2:0 color conversion in both directions.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class Av1ColorConversionBenchmarks
{
    /// <summary>
    /// The benchmark frame width.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The benchmark frame height.
    /// </summary>
    private const int Height = 1080;

    /// <summary>
    /// The source RGB image.
    /// </summary>
    private Image<Rgb48> source;

    /// <summary>
    /// The destination RGB image.
    /// </summary>
    private Image<Rgb48> destination;

    /// <summary>
    /// The reusable AV1 frame planes.
    /// </summary>
    private Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// Gets or sets the encoded AV1 bit depth.
    /// </summary>
    [Params(8, 10, 12)]
    public int BitDepth { get; set; }

    /// <summary>
    /// Allocates and populates deterministic full-HD RGB and YUV frames outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = new Image<Rgb48>(Width, Height);
        this.destination = new Image<Rgb48>(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            Span<Rgb48> row = this.source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                // The relatively prime channel steps avoid uniform rows while remaining deterministic.
                row[x] = new Rgb48(
                    (ushort)((x * 1879) + (y * 791)),
                    (ushort)((x * 977) + (y * 3251)),
                    (ushort)((x * 613) + (y * 4987)));
            }
        }

        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = Width,
            MaxFrameHeight = Height,
            ColorConfig = new ObuColorConfig
            {
                BitDepth = this.BitDepth switch
                {
                    10 => Av1BitDepth.TenBit,
                    12 => Av1BitDepth.TwelveBit,
                    _ => Av1BitDepth.EightBit,
                },
                ColorPrimaries = ObuColorPrimaries.Bt709,
                TransferCharacteristics = ObuTransferCharacteristics.Bt709,
                MatrixCoefficients = ObuMatrixCoefficients.Bt709,
                ColorRange = false,
                SubSamplingX = true,
                SubSamplingY = true,
                ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            },
        };

        this.frameBuffer = new Av1FrameBuffer<byte>(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, this.source.Frames.RootFrame, this.frameBuffer);
    }

    /// <summary>
    /// Releases the benchmark images and reconstructed planes.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.frameBuffer?.Dispose();
        this.destination?.Dispose();
        this.source?.Dispose();
    }

    /// <summary>
    /// Measures full-frame YUV-to-RGB conversion, including chroma reconstruction and packed-pixel conversion.
    /// </summary>
    /// <returns>A converted pixel that keeps the frame result observable.</returns>
    [Benchmark]
    public Rgb48 ConvertToRgb()
    {
        Av1FrameBuffer<byte> frameBuffer = this.frameBuffer;
        Image<Rgb48> destination = this.destination;
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            frameBuffer,
            new Rectangle(Point.Empty, destination.Frames.RootFrame.Size),
            destination.Frames.RootFrame.PixelBuffer.GetRegion(),
            destination.Frames.RootFrame.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            frameBuffer.ColorConfig.ColorRange);
        return destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(Height - 1)[Width - 1];
    }

    /// <summary>
    /// Measures full-frame RGB-to-YUV conversion, including planar unpacking and chroma downsampling.
    /// </summary>
    /// <returns>An encoded luma sample that keeps the frame result observable.</returns>
    [Benchmark]
    public int ConvertFromRgb()
    {
        Image<Rgb48> source = this.source;
        Av1FrameBuffer<byte> frameBuffer = this.frameBuffer;
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, frameBuffer);
        return this.BitDepth == 8
            ? frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(Height - 1)[Width - 1]
            : frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, Height - 1, 0, 0)[Width - 1];
    }
}

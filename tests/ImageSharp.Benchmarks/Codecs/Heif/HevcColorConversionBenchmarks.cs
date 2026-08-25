// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures frame-wide HEVC YUV 4:2:0 color conversion in both directions.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class HevcColorConversionBenchmarks
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
    /// The eight-bit source RGB image.
    /// </summary>
    private Image<Rgba32> byteSource = null!;

    /// <summary>
    /// The eight-bit destination RGB image.
    /// </summary>
    private Image<Rgba32> byteDestination = null!;

    /// <summary>
    /// The high-bit-depth source RGB image.
    /// </summary>
    private Image<Rgb48> highBitDepthSource = null!;

    /// <summary>
    /// The high-bit-depth destination RGB image.
    /// </summary>
    private Image<Rgb48> highBitDepthDestination = null!;

    /// <summary>
    /// The reusable HEVC component planes.
    /// </summary>
    private HevcPictureBuffer picture = null!;

    /// <summary>
    /// The H.273 profile used by both conversion directions.
    /// </summary>
    private CicpProfile colorProfile = null!;

    /// <summary>
    /// Gets or sets the encoded HEVC bit depth.
    /// </summary>
    [Params(8, 10, 12)]
    public int BitDepth { get; set; }

    /// <summary>
    /// Allocates and populates deterministic full-HD RGB and YUV frames outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.byteSource = new Image<Rgba32>(Width, Height);
        this.byteDestination = new Image<Rgba32>(Width, Height);
        this.highBitDepthSource = new Image<Rgb48>(Width, Height);
        this.highBitDepthDestination = new Image<Rgb48>(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            Span<Rgba32> byteRow = this.byteSource.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<Rgb48> highBitDepthRow = this.highBitDepthSource.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                // Relatively prime channel steps exercise the row-wide converter without introducing setup randomness.
                byteRow[x] = new Rgba32((byte)((x * 29) + (y * 11)), (byte)((x * 17) + (y * 31)), (byte)((x * 7) + (y * 43)));
                highBitDepthRow[x] = new Rgb48(
                    (ushort)((x * 1879) + (y * 791)),
                    (ushort)((x * 977) + (y * 3251)),
                    (ushort)((x * 613) + (y * 4987)));
            }
        }

        this.colorProfile = new CicpProfile(
            (byte)CicpColorPrimaries.ItuRBt709_6,
            (byte)CicpTransferCharacteristics.ItuRBt709_6,
            (byte)CicpMatrixCoefficients.ItuRBt709_6,
            false);

        this.picture = new HevcPictureBuffer(Configuration.Default, Width, Height, this.BitDepth, this.BitDepth, 1, false);
        if (this.BitDepth == 8)
        {
            HevcYuvConverter.ConvertFromRgb(
                Configuration.Default,
                this.byteSource.Frames.RootFrame,
                this.picture,
                this.colorProfile,
                HevcChromaSampleLocation.Left);
        }
        else
        {
            HevcYuvConverter.ConvertFromRgb(
                Configuration.Default,
                this.highBitDepthSource.Frames.RootFrame,
                this.picture,
                this.colorProfile,
                HevcChromaSampleLocation.Left);
        }
    }

    /// <summary>
    /// Releases the benchmark images and component planes.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.picture.Dispose();
        this.highBitDepthDestination.Dispose();
        this.highBitDepthSource.Dispose();
        this.byteDestination.Dispose();
        this.byteSource.Dispose();
    }

    /// <summary>
    /// Measures full-frame YUV-to-RGB conversion, including chroma reconstruction and packed-pixel conversion.
    /// </summary>
    /// <returns>A converted component that keeps the frame result observable.</returns>
    [Benchmark]
    public ushort ConvertToRgb()
    {
        if (this.BitDepth == 8)
        {
            HevcYuvConverter.ConvertToRgb(
                Configuration.Default,
                this.picture,
                this.byteDestination.Frames.RootFrame,
                this.colorProfile,
                HevcChromaSampleLocation.Left);

            return this.byteDestination[Width - 1, Height - 1].R;
        }

        HevcYuvConverter.ConvertToRgb(
            Configuration.Default,
            this.picture,
            this.highBitDepthDestination.Frames.RootFrame,
            this.colorProfile,
            HevcChromaSampleLocation.Left);

        return this.highBitDepthDestination[Width - 1, Height - 1].R;
    }

    /// <summary>
    /// Measures full-frame RGB-to-YUV conversion, including planar unpacking and chroma downsampling.
    /// </summary>
    /// <returns>An encoded luma sample that keeps the frame result observable.</returns>
    [Benchmark]
    public ushort ConvertFromRgb()
    {
        if (this.BitDepth == 8)
        {
            HevcYuvConverter.ConvertFromRgb(
                Configuration.Default,
                this.byteSource.Frames.RootFrame,
                this.picture,
                this.colorProfile,
                HevcChromaSampleLocation.Left);
        }
        else
        {
            HevcYuvConverter.ConvertFromRgb(
                Configuration.Default,
                this.highBitDepthSource.Frames.RootFrame,
                this.picture,
                this.colorProfile,
                HevcChromaSampleLocation.Left);
        }

        return this.picture.GetRowSpan(HevcPlane.Y, Height - 1)[Width - 1];
    }
}

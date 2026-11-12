// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// Enable this for larger TIFF files. They exceed 700 MB, so they are not part of the repository.
// Use the scripts gen_big.ps1 and gen_medium.ps1 in tests\Images\Input\Tiff\Benchmarks to generate those images.
//// #define BIG_TESTS

using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class DecodeTiff
{
    private byte[] data;

    // All three libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

#if BIG_TESTS
    private static readonly int BufferSize = 1024 * 68;

    private string TestImageFullPath => Path.Combine(
        TestEnvironment.InputImagesDirectoryFullPath,
        Path.Combine(TestImages.Tiff.Benchmark_Path, this.TestImage));

    [Params(
        TestImages.Tiff.Benchmark_BwFax3,
        //// TestImages.Tiff.Benchmark_RgbFax4, // fax4 is not supported yet.
        TestImages.Tiff.Benchmark_GrayscaleUncompressed,
        TestImages.Tiff.Benchmark_PaletteUncompressed,
        TestImages.Tiff.Benchmark_RgbDeflate,
        TestImages.Tiff.Benchmark_RgbLzw,
        TestImages.Tiff.Benchmark_RgbPackbits,
        TestImages.Tiff.Benchmark_RgbUncompressed)]
    public string TestImage { get; set; }

#else
    private static readonly int BufferSize = Configuration.Default.StreamProcessingBufferSize;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(
        TestImages.Tiff.CcittFax3AllTermCodes,
        TestImages.Tiff.Fax4Compressed2,
        TestImages.Tiff.HuffmanRleAllMakeupCodes,
        TestImages.Tiff.Calliphora_GrayscaleUncompressed,
        TestImages.Tiff.Calliphora_RgbPaletteLzw_Predictor,
        TestImages.Tiff.Calliphora_RgbDeflate_Predictor,
        TestImages.Tiff.Calliphora_RgbLzwPredictor,
        TestImages.Tiff.Calliphora_RgbPackbits,
        TestImages.Tiff.Calliphora_RgbUncompressed)]
    public string TestImage { get; set; }
#endif

    /// <summary>
    /// Loads the selected TIFF fixture before measuring its decoders.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        // File I/O belongs to setup so measured iterations include only decoding.
        this.data = File.ReadAllBytes(this.TestImageFullPath);
        this.inputStream = new MemoryStream(this.data, false);
    }

    /// <summary>
    /// Releases the shared input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
    public SDSize TiffSystemDrawing()
    {
        this.inputStream.Position = 0;
        using SDImage image = SDImage.FromStream(this.inputStream);
        return image.Size;
    }

    /// <summary>
    /// Decodes a TIFF image with ImageMagick for the decoder comparison.
    /// </summary>
    [Benchmark(Description = "ImageMagick Tiff")]
    public uint TiffImageMagick()
    {
        // Constructing MagickImage reads the pixels. Ping would read only the headers and skew this comparison.
        MagickReadSettings settings = new() { Format = MagickFormat.Tiff };
        this.inputStream.Position = 0;
        using MagickImage image = new(this.inputStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Tiff")]
    public Size TiffCore()
    {
        this.inputStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.inputStream);
        return image.Size;
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                | TestImage                                      | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Gen2    | Allocated  | Alloc Ratio |
    |---------------------- |----------------------------------------------- |-------------:|-------------:|-----------:|------:|--------:|----------:|---------:|--------:|-----------:|------------:|
    | 'System.Drawing Tiff' | Tiff/Calliphora_grayscale_uncompressed.tiff    |     44.76 us |    26.782 us |   1.468 us |  1.00 |    0.04 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_grayscale_uncompressed.tiff    |    103.15 us |    81.113 us |   4.446 us |  2.31 |    0.11 |    0.2441 |        - |       - |     4733 B |       14.43 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_grayscale_uncompressed.tiff    |     32.26 us |     2.838 us |   0.156 us |  0.72 |    0.02 |    0.4883 |        - |       - |     8885 B |       27.09 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    470.78 us |   198.241 us |  10.866 us |  1.00 |    0.03 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    757.61 us |   174.943 us |   9.589 us |  1.61 |    0.04 |         - |        - |       - |     4741 B |       14.45 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    423.02 us |    49.025 us |   2.687 us |  0.90 |    0.02 |    0.4883 |        - |       - |     9476 B |       28.89 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_lzw_predictor.tiff         |    701.51 us |    16.524 us |   0.906 us |  1.00 |    0.00 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_lzw_predictor.tiff         |    600.04 us |    22.602 us |   1.239 us |  0.86 |    0.00 |         - |        - |       - |     4741 B |       14.45 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_lzw_predictor.tiff         |  1,483.78 us |   908.774 us |  49.813 us |  2.12 |    0.06 |  173.8281 |  85.9375 |       - |  2922070 B |    8,908.75 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_packbits.tiff              |    121.73 us |    30.683 us |   1.682 us |  1.00 |    0.02 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_packbits.tiff              |    168.06 us |   124.119 us |   6.803 us |  1.38 |    0.05 |    0.2441 |        - |       - |     4734 B |       14.43 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_packbits.tiff              |     17.84 us |     3.610 us |   0.198 us |  0.15 |    0.00 |    0.5188 |        - |       - |     9064 B |       27.63 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff |  6,765.49 us |   925.739 us |  50.743 us |  1.00 |    0.01 |         - |        - |       - |      331 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff |  8,907.99 us |   840.021 us |  46.044 us |  1.32 |    0.01 |         - |        - |       - |     4934 B |       14.91 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 13,774.03 us |   955.519 us |  52.375 us |  2.04 |    0.01 | 1078.1250 | 531.2500 | 15.6250 | 18043481 B |   54,512.03 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff          |    112.34 us |    17.190 us |   0.942 us |  1.00 |    0.01 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_uncompressed.tiff          |    137.91 us |    24.123 us |   1.322 us |  1.23 |    0.01 |    0.2441 |        - |       - |     4734 B |       14.43 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff          |     11.52 us |     1.577 us |   0.086 us |  0.10 |    0.00 |    0.5188 |        - |       - |     8868 B |       27.04 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     22.27 us |     5.360 us |   0.294 us |  1.00 |    0.02 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     66.43 us |     3.545 us |   0.194 us |  2.98 |    0.03 |    0.2441 |        - |       - |     4733 B |       14.43 |
    | 'ImageSharp Tiff'     | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     32.31 us |     7.871 us |   0.431 us |  1.45 |    0.02 |    0.4272 |        - |       - |     7195 B |       21.94 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/CCITTGroup4.tiff                          |    758.06 us |   383.972 us |  21.047 us |  1.00 |    0.03 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/CCITTGroup4.tiff                          |  4,364.34 us |   594.351 us |  32.578 us |  5.76 |    0.14 |         - |        - |       - |     4831 B |       14.73 |
    | 'ImageSharp Tiff'     | Tiff/CCITTGroup4.tiff                          | 10,130.63 us | 3,936.036 us | 215.747 us | 13.37 |    0.40 |   15.6250 |  15.6250 | 15.6250 |    16556 B |       50.48 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/huffman_rle_all_makeup_codes.tiff         |     32.13 us |    19.569 us |   1.073 us |  1.00 |    0.04 |         - |        - |       - |      328 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/huffman_rle_all_makeup_codes.tiff         |    216.88 us |   535.164 us |  29.334 us |  6.75 |    0.81 |    0.2441 |        - |       - |     4734 B |       14.43 |
    | 'ImageSharp Tiff'     | Tiff/huffman_rle_all_makeup_codes.tiff         |    651.87 us |   592.187 us |  32.460 us | 20.30 |    1.05 |         - |        - |       - |     8760 B |       26.71 |

    Allocated reports managed memory only.
    */
}

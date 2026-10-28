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
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
    public SDSize TiffSystemDrawing()
    {
        using MemoryStream memoryStream = new(this.data);
        using SDImage image = SDImage.FromStream(memoryStream);
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
        using MemoryStream memoryStream = new(this.data);
        using MagickImage image = new(memoryStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Tiff")]
    public Size TiffCore()
    {
        using MemoryStream ms = new(this.data);
        using Image<Rgba32> image = Image.Load<Rgba32>(ms);
        return image.Size;
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                 | TestImage                                       |           Mean |        Error |      StdDev |   Ratio |   RatioSD |        Gen0 |       Gen1 |      Gen2 |    Allocated |   Alloc Ratio |
    | ---------------------- | ----------------------------------------------- | -------------: | -----------: | ----------: | ------: | --------: | ----------: | ---------: | --------: | -----------: | ------------: |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_grayscale_uncompressed.tiff     |       45.70 μs |     3.994 μs |    0.219 μs |    1.00 |      0.01 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/Calliphora_grayscale_uncompressed.tiff     |      101.78 μs |    18.275 μs |    1.002 μs |    2.23 |      0.02 |      0.2441 |          - |         - |       4797 B |         12.24 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_grayscale_uncompressed.tiff     |       31.80 μs |     0.390 μs |    0.021 μs |    0.70 |      0.00 |      0.4883 |          - |         - |       8896 B |         22.69 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_deflate_predictor.tiff      |      463.94 μs |   104.494 μs |    5.728 μs |    1.00 |      0.02 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/Calliphora_rgb_deflate_predictor.tiff      |      745.63 μs |   106.212 μs |    5.822 μs |    1.61 |      0.02 |           - |          - |         - |       4805 B |         12.26 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_deflate_predictor.tiff      |      422.26 μs |    48.053 μs |    2.634 μs |    0.91 |      0.01 |      0.4883 |          - |         - |       9376 B |         23.92 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_lzw_predictor.tiff          |      637.96 μs |   130.474 μs |    7.152 μs |    1.00 |      0.01 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/Calliphora_rgb_lzw_predictor.tiff          |      626.72 μs |    69.555 μs |    3.813 μs |    0.98 |      0.01 |           - |          - |         - |       4805 B |         12.26 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_lzw_predictor.tiff          |    1,408.41 μs |   108.544 μs |    5.950 μs |    2.21 |      0.02 |    173.8281 |    85.9375 |         - |    2921573 B |      7,452.99 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_packbits.tiff               |      119.91 μs |     4.718 μs |    0.259 μs |    1.00 |      0.00 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/Calliphora_rgb_packbits.tiff               |      160.04 μs |    49.690 μs |    2.724 μs |    1.33 |      0.02 |      0.2441 |          - |         - |       4798 B |         12.24 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_packbits.tiff               |       17.28 μs |     0.713 μs |    0.039 μs |    0.14 |      0.00 |      0.5188 |          - |         - |       8928 B |         22.78 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff  |    6,385.73 μs |   610.637 μs |   33.471 μs |    1.00 |      0.01 |           - |          - |         - |        395 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff  |    8,851.98 μs |   703.671 μs |   38.571 μs |    1.39 |      0.01 |           - |          - |         - |       4994 B |         12.64 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff  |   14,182.08 μs |   632.607 μs |   34.675 μs |    2.22 |      0.01 |   1078.1250 |   531.2500 |   15.6250 |   18027055 B |     45,638.11 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_uncompressed.tiff           |      113.54 μs |    10.179 μs |    0.558 μs |    1.00 |      0.01 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff           |      136.52 μs |     5.909 μs |    0.324 μs |    1.20 |      0.01 |      0.2441 |          - |         - |       4798 B |         12.24 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_uncompressed.tiff           |       11.31 μs |     0.137 μs |    0.007 μs |    0.10 |      0.00 |      0.5341 |          - |         - |       8944 B |         22.82 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/ccitt_fax3_all_terminating_codes.tiff      |       22.19 μs |     2.117 μs |    0.116 μs |    1.00 |      0.01 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/ccitt_fax3_all_terminating_codes.tiff      |       65.87 μs |     9.096 μs |    0.499 μs |    2.97 |      0.02 |      0.2441 |          - |         - |       4797 B |         12.24 |
    | 'ImageSharp Tiff'      | Tiff/ccitt_fax3_all_terminating_codes.tiff      |       31.82 μs |     0.088 μs |    0.005 μs |    1.43 |      0.01 |      0.4272 |          - |         - |       7224 B |         18.43 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/CCITTGroup4.tiff                           |      727.30 μs |    35.418 μs |    1.941 μs |    1.00 |      0.00 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/CCITTGroup4.tiff                           |    4,232.42 μs |   693.868 μs |   38.033 μs |    5.82 |      0.05 |           - |          - |         - |       4893 B |         12.48 |
    | 'ImageSharp Tiff'      | Tiff/CCITTGroup4.tiff                           |   10,706.91 μs |   310.112 μs |   16.998 μs |   14.72 |      0.04 |     15.6250 |    15.6250 |   15.6250 |       8200 B |         20.92 |
    |                        |                                                 |                |              |             |         |           |             |            |           |              |               |
    | 'System.Drawing Tiff'  | Tiff/huffman_rle_all_makeup_codes.tiff          |       27.24 μs |     4.481 μs |    0.246 μs |    1.00 |      0.01 |           - |          - |         - |        392 B |          1.00 |
    | 'ImageMagick Tiff'     | Tiff/huffman_rle_all_makeup_codes.tiff          |      137.86 μs |     8.790 μs |    0.482 μs |    5.06 |      0.04 |      0.2441 |          - |         - |       4798 B |         12.24 |
    | 'ImageSharp Tiff'      | Tiff/huffman_rle_all_makeup_codes.tiff          |      562.16 μs |    12.596 μs |    0.690 μs |   20.64 |      0.16 |           - |          - |         - |       7800 B |         19.90 |

    Allocated reports managed memory only.
    */
}

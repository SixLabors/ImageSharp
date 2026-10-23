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

    /* Measured 2026-10-18

    | Method                | TestImage                                      |         Mean |        Error |     StdDev | Ratio | RatioSD |      Gen0 |     Gen1 |    Gen2 |  Allocated | Alloc Ratio |
    | --------------------- | ---------------------------------------------- | -----------: | -----------: | ---------: | ----: | ------: | --------: | -------: | ------: | ---------: | ----------: |
    | 'System.Drawing Tiff' | Tiff/Calliphora_grayscale_uncompressed.tiff    |     44.80 μs |     0.692 μs |   0.038 μs |  1.00 |    0.00 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_grayscale_uncompressed.tiff    |    100.14 μs |     8.297 μs |   0.455 μs |  2.24 |    0.01 |    0.2441 |        - |       - |     4797 B |       12.24 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_grayscale_uncompressed.tiff    |     31.60 μs |     2.120 μs |   0.116 μs |  0.71 |    0.00 |    0.4883 |        - |       - |     8629 B |       22.01 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    464.93 μs |   159.149 μs |   8.723 μs |  1.00 |    0.02 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    734.93 μs |    28.383 μs |   1.556 μs |  1.58 |    0.03 |         - |        - |       - |     4805 B |       12.26 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    421.27 μs |    27.479 μs |   1.506 μs |  0.91 |    0.02 |    0.4883 |        - |       - |     9228 B |       23.54 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_lzw_predictor.tiff         |    642.36 μs |   280.550 μs |  15.378 μs |  1.00 |    0.03 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_lzw_predictor.tiff         |    628.92 μs |   157.649 μs |   8.641 μs |  0.98 |    0.02 |         - |        - |       - |     4805 B |       12.26 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_lzw_predictor.tiff         |  1,439.36 μs |   184.528 μs |  10.115 μs |  2.24 |    0.05 |  173.8281 |  85.9375 |       - |  2921822 B |    7,453.63 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_packbits.tiff              |    121.41 μs |     6.523 μs |   0.358 μs |  1.00 |    0.00 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_packbits.tiff              |    165.03 μs |    89.348 μs |   4.897 μs |  1.36 |    0.04 |    0.2441 |        - |       - |     4798 B |       12.24 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_packbits.tiff              |     17.77 μs |     0.931 μs |   0.051 μs |  0.15 |    0.00 |    0.5188 |        - |       - |     8816 B |       22.49 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff |  6,469.36 μs |   567.909 μs |  31.129 μs |  1.00 |    0.01 |         - |        - |       - |      395 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff |  8,990.68 μs | 1,205.920 μs |  66.101 μs |  1.39 |    0.01 |         - |        - |       - |     4994 B |       12.64 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 14,996.58 μs | 2,563.271 μs | 140.502 μs |  2.32 |    0.02 | 1078.1250 | 531.2500 | 15.6250 | 18043193 B |   45,678.97 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff          |    126.15 μs |    39.109 μs |   2.144 μs |  1.00 |    0.02 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_uncompressed.tiff          |    137.49 μs |    12.791 μs |   0.701 μs |  1.09 |    0.02 |    0.2441 |        - |       - |     4798 B |       12.24 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff          |     11.33 μs |     2.046 μs |   0.112 μs |  0.09 |    0.00 |    0.5035 |        - |       - |     8660 B |       22.09 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     21.90 μs |     3.458 μs |   0.190 μs |  1.00 |    0.01 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     65.14 μs |     6.353 μs |   0.348 μs |  2.97 |    0.03 |    0.2441 |        - |       - |     4797 B |       12.24 |
    | 'ImageSharp Tiff'     | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     31.52 μs |     1.265 μs |   0.069 μs |  1.44 |    0.01 |    0.3662 |        - |       - |     6947 B |       17.72 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/CCITTGroup4.tiff                          |    720.26 μs |   160.602 μs |   8.803 μs |  1.00 |    0.02 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/CCITTGroup4.tiff                          |  4,263.93 μs |   493.756 μs |  27.064 μs |  5.92 |    0.07 |         - |        - |       - |     4893 B |       12.48 |
    | 'ImageSharp Tiff'     | Tiff/CCITTGroup4.tiff                          |  9,631.47 μs |   852.696 μs |  46.739 μs | 13.37 |    0.15 |   15.6250 |  15.6250 | 15.6250 |    16308 B |       41.60 |
    |                       |                                                |              |              |            |       |         |           |          |         |            |             |
    | 'System.Drawing Tiff' | Tiff/huffman_rle_all_makeup_codes.tiff         |     27.44 μs |     5.727 μs |   0.314 μs |  1.00 |    0.01 |         - |        - |       - |      392 B |        1.00 |
    | 'ImageMagick Tiff'    | Tiff/huffman_rle_all_makeup_codes.tiff         |    143.84 μs |    20.176 μs |   1.106 μs |  5.24 |    0.06 |    0.2441 |        - |       - |     4798 B |       12.24 |
    | 'ImageSharp Tiff'     | Tiff/huffman_rle_all_makeup_codes.tiff         |    492.41 μs |     8.224 μs |   0.451 μs | 17.95 |    0.18 |         - |        - |       - |     8552 B |       21.82 |

    Allocated reports managed memory only; ImageMagick also allocates native memory.
    The input files have no ICC profiles. System.Drawing uses FromStream with image-data validation enabled.
    */
}

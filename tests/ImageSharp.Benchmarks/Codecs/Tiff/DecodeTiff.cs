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

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Path.Combine(TestImages.Tiff.Benchmark_Path, this.TestImage));

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

    /* Results 07.10.2026
        BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9550)
        AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
        .NET SDK 11.0.100-preview.7.26381.103
          [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
          Job-RCQXAA : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

        Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
        WarmupCount=3

        | Method                | TestImage                                      | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0      | Gen1     | Allocated  | Alloc Ratio |
        |---------------------- |----------------------------------------------- |-------------:|--------------:|-------------:|------:|--------:|----------:|---------:|-----------:|------------:|
        | 'System.Drawing Tiff' | Tiff/Calliphora_grayscale_uncompressed.tiff    |     45.37 μs |     10.003 μs |     0.548 μs |  1.00 |    0.01 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/Calliphora_grayscale_uncompressed.tiff    |    102.34 μs |     14.018 μs |     0.768 μs |  2.26 |    0.03 |    0.2441 |        - |     4797 B |       12.24 |
        | 'ImageSharp Tiff'     | Tiff/Calliphora_grayscale_uncompressed.tiff    |     32.49 μs |      1.313 μs |     0.072 μs |  0.72 |    0.01 |    0.4883 |        - |     8592 B |       21.92 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    521.71 μs |    217.900 μs |    11.944 μs |  1.00 |    0.03 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    952.37 μs |    660.618 μs |    36.211 μs |  1.83 |    0.07 |         - |        - |     4805 B |       12.26 |
        | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_deflate_predictor.tiff     |    539.04 μs |    547.762 μs |    30.025 μs |  1.03 |    0.05 |    0.4883 |        - |     9112 B |       23.24 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_lzw_predictor.tiff         |    881.69 μs |    626.805 μs |    34.357 μs |  1.00 |    0.05 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_lzw_predictor.tiff         |    859.84 μs |  1,160.419 μs |    63.606 μs |  0.98 |    0.07 |         - |        - |     4805 B |       12.26 |
        | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_lzw_predictor.tiff         |  2,011.53 μs |  1,304.388 μs |    71.498 μs |  2.28 |    0.10 |  173.8281 |  85.9375 |  2921277 B |    7,452.24 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_packbits.tiff              |    157.40 μs |    204.772 μs |    11.224 μs |  1.00 |    0.09 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_packbits.tiff              |    225.84 μs |    478.260 μs |    26.215 μs |  1.44 |    0.17 |    0.2441 |        - |     4798 B |       12.24 |
        | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_packbits.tiff              |     30.01 μs |     67.186 μs |     3.683 μs |  0.19 |    0.02 |    0.5188 |        - |     8744 B |       22.31 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff |  8,394.62 μs |  3,531.058 μs |   193.549 μs |  1.00 |    0.03 |         - |        - |      395 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 14,073.70 μs | 13,344.360 μs |   731.449 μs |  1.68 |    0.08 |         - |        - |     4994 B |       12.64 |
        | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 18,324.73 μs | 21,319.493 μs | 1,168.593 μs |  2.18 |    0.13 | 1078.1250 | 531.2500 | 18036984 B |   45,663.25 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff          |    152.31 μs |     14.302 μs |     0.784 μs |  1.00 |    0.01 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/Calliphora_rgb_uncompressed.tiff          |    178.45 μs |     32.427 μs |     1.777 μs |  1.17 |    0.01 |    0.2441 |        - |     4798 B |       12.24 |
        | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff          |     17.28 μs |      2.643 μs |     0.145 μs |  0.11 |    0.00 |    0.5035 |        - |     8568 B |       21.86 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     30.54 μs |     25.089 μs |     1.375 μs |  1.00 |    0.06 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     89.30 μs |    242.627 μs |    13.299 μs |  2.93 |    0.40 |    0.2441 |        - |     4797 B |       12.24 |
        | 'ImageSharp Tiff'     | Tiff/ccitt_fax3_all_terminating_codes.tiff     |     44.81 μs |     75.255 μs |     4.125 μs |  1.47 |    0.13 |    0.3662 |        - |     6928 B |       17.67 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/CCITTGroup4.tiff                          |    984.44 μs |    134.457 μs |     7.370 μs |  1.00 |    0.01 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/CCITTGroup4.tiff                          |  5,335.77 μs |  1,556.299 μs |    85.306 μs |  5.42 |    0.08 |         - |        - |     4893 B |       12.48 |
        | 'ImageSharp Tiff'     | Tiff/CCITTGroup4.tiff                          | 11,659.66 μs | 10,085.036 μs |   552.795 μs | 11.84 |    0.49 |         - |        - |     7837 B |       19.99 |
        |                       |                                                |              |               |              |       |         |           |          |            |             |
        | 'System.Drawing Tiff' | Tiff/huffman_rle_all_makeup_codes.tiff         |     38.21 μs |     30.267 μs |     1.659 μs |  1.00 |    0.05 |         - |        - |      392 B |        1.00 |
        | 'ImageMagick Tiff'    | Tiff/huffman_rle_all_makeup_codes.tiff         |    178.73 μs |    362.357 μs |    19.862 μs |  4.68 |    0.49 |    0.2441 |        - |     4798 B |       12.24 |
        | 'ImageSharp Tiff'     | Tiff/huffman_rle_all_makeup_codes.tiff         |    555.03 μs |     42.643 μs |     2.337 μs | 14.54 |    0.56 |         - |        - |     7504 B |       19.14 |

        Allocated reports managed memory only; ImageMagick also allocates native memory.
        System.Drawing measures the existing FromStream followed by Size path.
     */
}

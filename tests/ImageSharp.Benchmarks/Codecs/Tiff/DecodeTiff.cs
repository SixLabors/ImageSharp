// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#if OS_WINDOWS
// Enable this for using larger Tiff files. Those files are very large (> 700MB) and therefor not part of the git repo.
// Use the scripts gen_big.ps1 and gen_medium.ps1 in tests\Images\Input\Tiff\Benchmarks to generate those images.
//// #define BIG_TESTS

using BenchmarkDotNet.Attributes;
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
    private string prevImage;

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

    [IterationSetup]
    public void ReadImages()
    {
        if (this.prevImage != this.TestImage)
        {
            this.data = File.ReadAllBytes(this.TestImageFullPath);
            this.prevImage = this.TestImage;
        }
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
    public SDSize TiffSystemDrawing()
    {
        using MemoryStream memoryStream = new(this.data);
        using SDImage image = SDImage.FromStream(memoryStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Tiff")]
    public Size TiffCore()
    {
        using MemoryStream ms = new(this.data);
        using Image<Rgba32> image = Image.Load<Rgba32>(ms);
        return image.Size;
    }
}
#endif

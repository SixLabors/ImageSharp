// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Exposes every closed JPEG operator traversal beside the Vector512 implementation it replaces.
/// </summary>
/// <remarks>
/// A 63-pixel buffer leaves 256-bit, 128-bit, and scalar remainders after the 512-bit loop, making
/// every operator overload visible in the generated traversal assembly on AVX-512 hardware.
/// </remarks>
[Config(typeof(Config.Analysis))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JpegColorConverterTraversalAssembly
{
    private const int Count = 63;

    private readonly JpegColorConverterBase.GrayScaleVector512 grayscaleLegacy = new(8);
    private readonly JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.GrayScaleOperator> grayscaleOperator = new(8);
    private readonly JpegColorConverterBase.RgbVector512 rgbLegacy = new(8);
    private readonly JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.RgbOperator> rgbOperator = new(8);
    private readonly JpegColorConverterBase.CmykVector512 cmykLegacy = new(8);
    private readonly JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.CmykOperator> cmykOperator = new(8);
    private readonly JpegColorConverterBase.YCbCrVector512 yCbCrLegacy = new(8);
    private readonly JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YCbCrOperator> yCbCrOperator = new(8);
    private readonly JpegColorConverterBase.YccKVector512 yccKLegacy = new(8);
    private readonly JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YccKOperator> yccKOperator = new(8);
    private readonly JpegColorConverterBase.TiffCmykVector512 tiffCmykLegacy = new(8);
    private readonly JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.TiffCmykOperator> tiffCmykOperator = new(8);
    private readonly JpegColorConverterBase.TiffYccKVector512 tiffYccKLegacy = new(8);
    private readonly JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.TiffYccKOperator> tiffYccKOperator = new(8);

    private readonly float[] legacyC0 = new float[Count];
    private readonly float[] legacyC1 = new float[Count];
    private readonly float[] legacyC2 = new float[Count];
    private readonly float[] legacyC3 = new float[Count];
    private readonly float[] operatorC0 = new float[Count];
    private readonly float[] operatorC1 = new float[Count];
    private readonly float[] operatorC2 = new float[Count];
    private readonly float[] operatorC3 = new float[Count];
    private readonly float[] r = new float[Count];
    private readonly float[] g = new float[Count];
    private readonly float[] b = new float[Count];

    /// <summary>
    /// Populates the component and RGB planes with deterministic sample-domain values.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Random random = new(42);

        for (int i = 0; i < Count; i++)
        {
            // Independent non-constant lanes prevent the JIT from folding arithmetic or mask decisions.
            this.legacyC0[i] = this.operatorC0[i] = (float)random.NextDouble() * 255F;
            this.legacyC1[i] = this.operatorC1[i] = (float)random.NextDouble() * 255F;
            this.legacyC2[i] = this.operatorC2[i] = (float)random.NextDouble() * 255F;
            this.legacyC3[i] = this.operatorC3[i] = (float)random.NextDouble() * 255F;
            this.r[i] = (float)random.NextDouble() * 255F;
            this.g[i] = (float)random.NextDouble() * 255F;
            this.b[i] = (float)random.NextDouble() * 255F;
        }
    }

    /// <summary>
    /// Runs the replaced grayscale component-to-RGB traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Grayscale.ToRgb")]
    public void GrayscaleLegacyToRgb()
        => this.grayscaleLegacy.ConvertToRgbInPlace(this.CreateLegacyValues(1));

    /// <summary>
    /// Runs the shared grayscale component-to-RGB traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Grayscale.ToRgb")]
    public void GrayscaleOperatorToRgb()
        => this.grayscaleOperator.ConvertToRgbInPlace(this.CreateOperatorValues(1));

    /// <summary>
    /// Runs the replaced grayscale RGB-to-component traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Grayscale.FromRgb")]
    public void GrayscaleLegacyFromRgb()
        => this.grayscaleLegacy.ConvertFromRgb(this.CreateLegacyValues(1), this.r, this.g, this.b);

    /// <summary>
    /// Runs the shared grayscale RGB-to-component traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Grayscale.FromRgb")]
    public void GrayscaleOperatorFromRgb()
        => this.grayscaleOperator.ConvertFromRgb(this.CreateOperatorValues(1), this.r, this.g, this.b);

    /// <summary>
    /// Runs the replaced RGB component-to-RGB traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgb.ToRgb")]
    public void RgbLegacyToRgb()
        => this.rgbLegacy.ConvertToRgbInPlace(this.CreateLegacyValues(3));

    /// <summary>
    /// Runs the shared RGB component-to-RGB traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Rgb.ToRgb")]
    public void RgbOperatorToRgb()
        => this.rgbOperator.ConvertToRgbInPlace(this.CreateOperatorValues(3));

    /// <summary>
    /// Runs the replaced RGB RGB-to-component traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgb.FromRgb")]
    public void RgbLegacyFromRgb()
        => this.rgbLegacy.ConvertFromRgb(this.CreateLegacyValues(3), this.r, this.g, this.b);

    /// <summary>
    /// Runs the shared RGB RGB-to-component traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Rgb.FromRgb")]
    public void RgbOperatorFromRgb()
        => this.rgbOperator.ConvertFromRgb(this.CreateOperatorValues(3), this.r, this.g, this.b);

    /// <summary>
    /// Runs the replaced CMYK component-to-RGB traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Cmyk.ToRgb")]
    public void CmykLegacyToRgb()
        => this.cmykLegacy.ConvertToRgbInPlace(this.CreateLegacyValues(4));

    /// <summary>
    /// Runs the shared CMYK component-to-RGB traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Cmyk.ToRgb")]
    public void CmykOperatorToRgb()
        => this.cmykOperator.ConvertToRgbInPlace(this.CreateOperatorValues(4));

    /// <summary>
    /// Runs the replaced CMYK RGB-to-component traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Cmyk.FromRgb")]
    public void CmykLegacyFromRgb()
        => this.cmykLegacy.ConvertFromRgb(this.CreateLegacyValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Runs the shared CMYK RGB-to-component traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Cmyk.FromRgb")]
    public void CmykOperatorFromRgb()
        => this.cmykOperator.ConvertFromRgb(this.CreateOperatorValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Runs the replaced YCbCr component-to-RGB traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("YCbCr.ToRgb")]
    public void YCbCrLegacyToRgb()
        => this.yCbCrLegacy.ConvertToRgbInPlace(this.CreateLegacyValues(3));

    /// <summary>
    /// Runs the shared YCbCr component-to-RGB traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("YCbCr.ToRgb")]
    public void YCbCrOperatorToRgb()
        => this.yCbCrOperator.ConvertToRgbInPlace(this.CreateOperatorValues(3));

    /// <summary>
    /// Runs the replaced YCbCr RGB-to-component traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("YCbCr.FromRgb")]
    public void YCbCrLegacyFromRgb()
        => this.yCbCrLegacy.ConvertFromRgb(this.CreateLegacyValues(3), this.r, this.g, this.b);

    /// <summary>
    /// Runs the shared YCbCr RGB-to-component traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("YCbCr.FromRgb")]
    public void YCbCrOperatorFromRgb()
        => this.yCbCrOperator.ConvertFromRgb(this.CreateOperatorValues(3), this.r, this.g, this.b);

    /// <summary>
    /// Runs the replaced YCCK component-to-RGB traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("YccK.ToRgb")]
    public void YccKLegacyToRgb()
        => this.yccKLegacy.ConvertToRgbInPlace(this.CreateLegacyValues(4));

    /// <summary>
    /// Runs the shared YCCK component-to-RGB traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("YccK.ToRgb")]
    public void YccKOperatorToRgb()
        => this.yccKOperator.ConvertToRgbInPlace(this.CreateOperatorValues(4));

    /// <summary>
    /// Runs the replaced YCCK RGB-to-component traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("YccK.FromRgb")]
    public void YccKLegacyFromRgb()
        => this.yccKLegacy.ConvertFromRgb(this.CreateLegacyValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Runs the shared YCCK RGB-to-component traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("YccK.FromRgb")]
    public void YccKOperatorFromRgb()
        => this.yccKOperator.ConvertFromRgb(this.CreateOperatorValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Runs the replaced TIFF CMYK component-to-RGB traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TiffCmyk.ToRgb")]
    public void TiffCmykLegacyToRgb()
        => this.tiffCmykLegacy.ConvertToRgbInPlace(this.CreateLegacyValues(4));

    /// <summary>
    /// Runs the shared TIFF CMYK component-to-RGB traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("TiffCmyk.ToRgb")]
    public void TiffCmykOperatorToRgb()
        => this.tiffCmykOperator.ConvertToRgbInPlace(this.CreateOperatorValues(4));

    /// <summary>
    /// Runs the replaced TIFF CMYK RGB-to-component traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TiffCmyk.FromRgb")]
    public void TiffCmykLegacyFromRgb()
        => this.tiffCmykLegacy.ConvertFromRgb(this.CreateLegacyValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Runs the shared TIFF CMYK RGB-to-component traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("TiffCmyk.FromRgb")]
    public void TiffCmykOperatorFromRgb()
        => this.tiffCmykOperator.ConvertFromRgb(this.CreateOperatorValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Runs the replaced TIFF YCCK component-to-RGB traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TiffYccK.ToRgb")]
    public void TiffYccKLegacyToRgb()
        => this.tiffYccKLegacy.ConvertToRgbInPlace(this.CreateLegacyValues(4));

    /// <summary>
    /// Runs the shared TIFF YCCK component-to-RGB traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("TiffYccK.ToRgb")]
    public void TiffYccKOperatorToRgb()
        => this.tiffYccKOperator.ConvertToRgbInPlace(this.CreateOperatorValues(4));

    /// <summary>
    /// Runs the replaced TIFF YCCK RGB-to-component traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TiffYccK.FromRgb")]
    public void TiffYccKLegacyFromRgb()
        => this.tiffYccKLegacy.ConvertFromRgb(this.CreateLegacyValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Runs the shared TIFF YCCK RGB-to-component traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("TiffYccK.FromRgb")]
    public void TiffYccKOperatorFromRgb()
        => this.tiffYccKOperator.ConvertFromRgb(this.CreateOperatorValues(4), this.r, this.g, this.b);

    /// <summary>
    /// Creates a correctly aliased component view over the legacy planes.
    /// </summary>
    /// <param name="componentCount">The number of component planes owned by the color model.</param>
    /// <returns>The legacy component view.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JpegColorConverterBase.ComponentValues CreateLegacyValues(int componentCount)
        => new(
            componentCount,
            this.legacyC0,
            componentCount > 1 ? this.legacyC1 : this.legacyC0,
            componentCount > 2 ? this.legacyC2 : this.legacyC0,
            componentCount > 3 ? this.legacyC3 : []);

    /// <summary>
    /// Creates a correctly aliased component view over the operator planes.
    /// </summary>
    /// <param name="componentCount">The number of component planes owned by the color model.</param>
    /// <returns>The operator component view.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JpegColorConverterBase.ComponentValues CreateOperatorValues(int componentCount)
        => new(
            componentCount,
            this.operatorC0,
            componentCount > 1 ? this.operatorC1 : this.operatorC0,
            componentCount > 2 ? this.operatorC2 : this.operatorC0,
            componentCount > 3 ? this.operatorC3 : []);
}

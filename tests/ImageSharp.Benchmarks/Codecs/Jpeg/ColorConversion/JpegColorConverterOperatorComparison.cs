// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Compares each shared operator converter with the Vector512 converter it replaces.
/// </summary>
[Config(typeof(Config.Standard))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JpegColorConverterOperatorComparison
{
    private JpegColorConverterBase legacy;
    private JpegColorConverterBase operatorConverter;
    private float[] legacyC0;
    private float[] legacyC1;
    private float[] legacyC2;
    private float[] legacyC3;
    private float[] operatorC0;
    private float[] operatorC1;
    private float[] operatorC2;
    private float[] operatorC3;
    private float[] r;
    private float[] g;
    private float[] b;
    private int componentCount;

    /// <summary>
    /// Gets or sets the color model measured by the current benchmark case.
    /// </summary>
    [Params(
        JpegColorModel.Grayscale,
        JpegColorModel.Rgb,
        JpegColorModel.Cmyk,
        JpegColorModel.YCbCr,
        JpegColorModel.YccK,
        JpegColorModel.TiffCmyk,
        JpegColorModel.TiffYccK)]
    public JpegColorModel ColorModel { get; set; }

    /// <summary>
    /// Gets or sets the number of pixels converted by each invocation.
    /// </summary>
    [Params(128, 1024)]
    public int Count { get; set; }

    /// <summary>
    /// Creates equivalent legacy and operator converters and their independent component buffers.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        (JpegColorConverterBase Legacy, JpegColorConverterBase Operator, int ComponentCount) converters =
            this.ColorModel switch
        {
            JpegColorModel.Grayscale => (
                new JpegColorConverterBase.GrayScaleVector512(8),
                new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.GrayScaleOperator>(8),
                1),
            JpegColorModel.Rgb => (
                new JpegColorConverterBase.RgbVector512(8),
                new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.RgbOperator>(8),
                3),
            JpegColorModel.Cmyk => (
                new JpegColorConverterBase.CmykVector512(8),
                new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.CmykOperator>(8),
                4),
            JpegColorModel.YCbCr => (
                new JpegColorConverterBase.YCbCrVector512(8),
                new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YCbCrOperator>(8),
                3),
            JpegColorModel.YccK => (
                new JpegColorConverterBase.YccKVector512(8),
                new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YccKOperator>(8),
                4),
            JpegColorModel.TiffCmyk => (
                new JpegColorConverterBase.TiffCmykVector512(8),
                new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.TiffCmykOperator>(8),
                4),
            JpegColorModel.TiffYccK => (
                new JpegColorConverterBase.TiffYccKVector512(8),
                new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.TiffYccKOperator>(8),
                4),
            _ => throw new InvalidOperationException(),
        };

        (this.legacy, this.operatorConverter, this.componentCount) = converters;

        Random random = new(42);
        this.legacyC0 = CreateRandomValues(this.Count, random);
        this.legacyC1 = CreateRandomValues(this.Count, random);
        this.legacyC2 = CreateRandomValues(this.Count, random);
        this.legacyC3 = CreateRandomValues(this.Count, random);
        this.operatorC0 = this.legacyC0.ToArray();
        this.operatorC1 = this.legacyC1.ToArray();
        this.operatorC2 = this.legacyC2.ToArray();
        this.operatorC3 = this.legacyC3.ToArray();
        this.r = CreateRandomValues(this.Count, random);
        this.g = CreateRandomValues(this.Count, random);
        this.b = CreateRandomValues(this.Count, random);
    }

    /// <summary>
    /// Converts JPEG components to RGB using the replaced Vector512 implementation.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ToRgb")]
    public void LegacyToRgb()
    {
        JpegColorConverterBase.ComponentValues values = this.CreateLegacyValues();

        this.legacy.ConvertToRgbInPlace(values);
    }

    /// <summary>
    /// Converts JPEG components to RGB using the shared operator traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("ToRgb")]
    public void OperatorToRgb()
    {
        JpegColorConverterBase.ComponentValues values = this.CreateOperatorValues();

        this.operatorConverter.ConvertToRgbInPlace(values);
    }

    /// <summary>
    /// Converts RGB to JPEG components using the replaced Vector512 implementation.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FromRgb")]
    public void LegacyFromRgb()
    {
        JpegColorConverterBase.ComponentValues values = this.CreateLegacyValues();

        this.legacy.ConvertFromRgb(values, this.r, this.g, this.b);
    }

    /// <summary>
    /// Converts RGB to JPEG components using the shared operator traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FromRgb")]
    public void OperatorFromRgb()
    {
        JpegColorConverterBase.ComponentValues values = this.CreateOperatorValues();

        this.operatorConverter.ConvertFromRgb(values, this.r, this.g, this.b);
    }

    /// <summary>
    /// Creates a component view over the buffers owned by the legacy converter.
    /// </summary>
    /// <returns>The component view for the configured color model.</returns>
    private JpegColorConverterBase.ComponentValues CreateLegacyValues()
        => new(
            this.componentCount,
            this.legacyC0,
            this.componentCount > 1 ? this.legacyC1 : this.legacyC0,
            this.componentCount > 2 ? this.legacyC2 : this.legacyC0,
            this.componentCount > 3 ? this.legacyC3 : []);

    /// <summary>
    /// Creates a component view over the buffers owned by the operator converter.
    /// </summary>
    /// <returns>The component view for the configured color model.</returns>
    private JpegColorConverterBase.ComponentValues CreateOperatorValues()
        => new(
            this.componentCount,
            this.operatorC0,
            this.componentCount > 1 ? this.operatorC1 : this.operatorC0,
            this.componentCount > 2 ? this.operatorC2 : this.operatorC0,
            this.componentCount > 3 ? this.operatorC3 : []);

    /// <summary>
    /// Creates deterministic sample-domain values for one component plane.
    /// </summary>
    /// <param name="length">The number of samples to create.</param>
    /// <param name="random">The deterministic random source shared by setup.</param>
    /// <returns>The populated component plane.</returns>
    private static float[] CreateRandomValues(int length, Random random)
    {
        float[] values = new float[length];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (float)random.NextDouble() * 255F;
        }

        return values;
    }

    /// <summary>
    /// Identifies the JPEG color model used by a benchmark case.
    /// </summary>
    public enum JpegColorModel
    {
        /// <summary>
        /// One luminance component.
        /// </summary>
        Grayscale,

        /// <summary>
        /// Three direct RGB components.
        /// </summary>
        Rgb,

        /// <summary>
        /// Four inverted Adobe CMYK components.
        /// </summary>
        Cmyk,

        /// <summary>
        /// Three JPEG YCbCr components.
        /// </summary>
        YCbCr,

        /// <summary>
        /// Four inverted Adobe YCCK components.
        /// </summary>
        YccK,

        /// <summary>
        /// Four non-inverted TIFF CMYK components.
        /// </summary>
        TiffCmyk,

        /// <summary>
        /// Four non-inverted TIFF YCCK components.
        /// </summary>
        TiffYccK,
    }
}

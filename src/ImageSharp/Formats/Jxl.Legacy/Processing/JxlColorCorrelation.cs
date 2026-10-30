// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlColorCorrelation
{
    private float baseCorrelationX;
    private float baseCorrelationB;

    private InlineArray4<float> dcFactors;
    private uint colorFactor;
    private float colorScale;

    /// <summary>
    /// Initializes the scalar correlation fields to the JPEG XL defaults.
    /// </summary>
    public JxlColorCorrelation() => this.Reset();

    /// <summary>
    /// Restores the scalar field defaults before a reused map decodes another frame's DC correlation.
    /// </summary>
    /// <remarks>
    /// The map owner applies its XYB setting and recomputes the DC factors after this reset.
    /// An encoded all-default DC field does not carry replacements for these values.
    /// </remarks>
    public void Reset()
    {
        this.baseCorrelationX = 0;
        this.baseCorrelationB = JxlOpsinConstants.YToBRatio;
        this.colorFactor = JxlChromaFromLuma.DefaultColorFactor;
        this.colorScale = 1F / JxlChromaFromLuma.DefaultColorFactor;
        this.YToXDc = 0;
        this.YToBDc = 0;
        this.dcFactors = default;
    }

    public int YToXDc { get; private set; }

    public int YToBDc { get; private set; }

    public float ColorFactor => this.colorFactor;

    public float BaseCorrelationX
    {
        get => this.baseCorrelationX;
        set => this.baseCorrelationX = value;
    }

    public float BaseCorrelationB
    {
        get => this.baseCorrelationB;
        set => this.baseCorrelationB = value;
    }

    public bool IsJpegCompatible =>
        this.BaseCorrelationX == 0 &&
        this.BaseCorrelationB == 0 &&
        this.YToBDc == 0 &&
        this.YToXDc == 0 &&
        this.ColorFactor == JxlChromaFromLuma.DefaultColorFactor;

    public ReadOnlySpan<float> DcFactors => this.dcFactors;

    public float YToXRatio(int xFactor) => this.BaseCorrelationX + (xFactor * this.colorScale);

    public float YToBRatio(int bFactor) => this.BaseCorrelationB + (bFactor * this.colorScale);

    public void SetColorFactor(uint factor)
    {
        this.colorFactor = factor;
        this.colorScale = 1f / factor;
        this.RecomputeDcFactors();
    }

    public void SetYToBDc(int yToBDc)
    {
        this.YToBDc = yToBDc;
        this.RecomputeDcFactors();
    }

    public void SetYToXDc(int yToXDc)
    {
        this.YToXDc = yToXDc;
        this.RecomputeDcFactors();
    }

    public void RecomputeDcFactors()
    {
        this.dcFactors[0] = this.YToXRatio(this.YToXDc);
        this.dcFactors[2] = this.YToBRatio(this.YToBDc);
    }

    public bool DecodeDc(JxlBitReader reader)
    {
        bool allDefault = reader.ReadBoolean();

        if (allDefault)
        {
            return true;
        }

        this.SetColorFactor(JxlU32Coder.Read(JxlChromaFromLuma.ColorFactorDistribution, reader));

        if (!JxlF16Coder.Read(reader, ref this.baseCorrelationX))
        {
            return false;
        }

        if (MathF.Abs(this.baseCorrelationX) > 4f)
        {
            throw new InvalidOperationException("Base X correlation is out of range");
        }

        if (!JxlF16Coder.Read(reader, ref this.baseCorrelationB))
        {
            return false;
        }

        if (MathF.Abs(this.baseCorrelationB) > 4f)
        {
            throw new InvalidOperationException("Base B correlation is out of range");
        }

        this.YToXDc = (int)reader.ReadBits32(8) + sbyte.MinValue;
        this.YToBDc = (int)reader.ReadBits32(8) + sbyte.MinValue;

        this.RecomputeDcFactors();
        return true;
    }

    public static int RatioJpeg(int factor) => factor * (1 << JxlChromaFromLuma.CflFixedPointPrecision) / JxlChromaFromLuma.DefaultColorFactor;
}

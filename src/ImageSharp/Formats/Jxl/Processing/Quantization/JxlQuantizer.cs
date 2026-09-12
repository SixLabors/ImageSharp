// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// The quantizer for DCT DC/AC coefficients.
/// </summary>
/// <remarks>
/// The quantizer's primary role is to lower the value
/// of coefficients. For example, during encoding,
/// coefficients may be divided by 3 and have to be
/// multiplied by 3 at decoding (which is lossy). Quantization is often
/// useful for variable-length coding where the amount
/// of bits depend on how large the number is.
/// </remarks>
internal sealed class JxlQuantizer
{
    /// <summary>
    /// Denominator for the global_scale value.
    /// </summary>
    private const int GlobalScaleDenominator = 1 << 16;

    /// <summary>
    /// Numerator for the global_scale value.
    /// </summary>
    private const int GlobalScaleNumerator = 4096;

    /// <summary>
    /// Numerator for biases.
    /// </summary>
    private const float BiasNumerator = 0.145f;

    /// <summary>
    /// The default value of the quant.
    /// </summary>
    private const int DefaultQuant = 64;

    /// <summary>
    /// The maximum value for a quant. Quant cannot be greater than this -
    /// if attempted to, it will be limited to this value.
    /// </summary>
    private const int MaxQuant = 256;

    /// <summary>
    /// Represents the multipliers for the DC coefficients.
    /// </summary>
    private InlineArray4<float> mulDc;

    /// <summary>
    /// Represents the inverse multipliers for the DC coefficients.
    /// </summary>
    private InlineArray4<float> inverseMulDc;

    /// <summary>
    /// Global scale
    /// </summary>
    private int globalScale;

    /// <summary>
    /// Quantizer DC
    /// </summary>
    private int quantDc;

    /// <summary>
    /// The zero bias.
    /// </summary>
    private readonly InlineArray3<float> zeroBias;

    /// <summary>
    /// The dequant matrices.
    /// </summary>
    private readonly JxlDequantMatrices? dequant;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlQuantizer"/> class using the default
    /// DC quantizer &amp; scale factors.
    /// </summary>
    /// <param name="dequant">The dequant.</param>
    public JxlQuantizer(JxlDequantMatrices dequant)
        : this(dequant, DefaultQuant, GlobalScaleDenominator / DefaultQuant)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlQuantizer"/> class.
    /// </summary>
    /// <param name="dequant">The dequant.</param>
    /// <param name="quantDc">The DC quantizer.</param>
    /// <param name="globalScale">The scale factor.</param>
    public JxlQuantizer(JxlDequantMatrices dequant, int quantDc, int globalScale)
    {
        this.dequant = dequant;
        this.quantDc = quantDc;
        this.globalScale = globalScale;

        this.RecomputeFromGlobalScale();
        this.InverseQuantDc = this.InverseGlobalScale / this.quantDc;

        ZeroBiasDefault.CopyTo(this.zeroBias);
    }

    /// <summary>
    /// Gets the scaling factor.
    /// </summary>
    public float Scale { get; private set; }

    /// <summary>
    /// Gets the inverse scaling factor. It is a reciprocal of <see cref="Scale"/>.
    /// </summary>
    public float InverseGlobalScale { get; private set; }

    /// <summary>
    /// Gets the inverse DC quantization base value.
    /// </summary>
    public float InverseQuantDc { get; private set; }

    public ReadOnlySpan<float> MulDc => this.mulDc;

    public ReadOnlySpan<float> InverseMulDc => this.inverseMulDc;

    /// <summary>
    /// Gets the zero-biases for quantizing channels X, Y, and B.
    /// </summary>
    private static ReadOnlySpan<float> ZeroBiasDefault => [0.5f, 0.5f, 0.5f];

    /// <summary>
    /// Gets the default bias for quant.
    /// </summary>
    public static ReadOnlySpan<float> DefaultQuantBias =>
    [
        1.0f - 0.05465007330715401f,
        1.0f - 0.07005449891748593f,
        1.0f - 0.049935103337343655f,
        0.145f,
    ];

    /// <summary>
    /// Clears the <see cref="MulDc"/> and <see cref="InverseMulDc"/> values,
    /// setting their contents to 1.0f.
    /// </summary>
    public void ClearDcMultipliers()
    {
        ((Span<float>)this.mulDc).Fill(1f);
        ((Span<float>)this.inverseMulDc).Fill(1f);
    }

    /// <summary>
    /// Ensure that the input value stays within the range of 1..MaxQuant.
    /// </summary>
    /// <param name="value">Value to clamp</param>
    /// <returns>The input value clamped into the range of 1..MaxQuant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Clamp(float value) => (int)MathF.Max(1.0f, MathF.Min(value, MaxQuant));

    /// <summary>
    /// Scales the global scale value.
    /// </summary>
    /// <param name="scale">The new scale</param>
    /// <returns>The scale value, scaled by the global scale.</returns>
    public float ScaleGlobalScale(float scale)
    {
        int newGlobalScale = (int)MathF.Round(this.globalScale * scale, MidpointRounding.AwayFromZero);
        float scaleOut = newGlobalScale * 1.0f / this.globalScale;
        this.globalScale = newGlobalScale;

        this.RecomputeFromGlobalScale();

        return scaleOut;
    }

    /// <summary>
    /// Recomputes quant values from the scale.
    /// </summary>
    public void RecomputeFromGlobalScale()
    {
        this.Scale = this.globalScale * (1.0f / GlobalScaleDenominator);
        this.InverseGlobalScale = 1.0f * GlobalScaleDenominator / this.globalScale;
        this.InverseQuantDc = this.InverseGlobalScale / this.quantDc;

        for (int c = 0; c < 3; c++)
        {
            this.mulDc[c] = this.GetDcStep(c);
            this.inverseMulDc[c] = this.GetInverseDcStep(c);
        }
    }

    /// <summary>
    /// Returns the dequant matrix.
    /// </summary>
    /// <param name="strategy">The quant kind</param>
    /// <param name="c">The quantization index</param>
    /// <returns>The dequant matrix.</returns>
    public ReadOnlySpan<float> DequantMatrix(JxlAcStrategyType strategy, int c)
        => this.dequant!.GetMatrix(strategy, c);

    /// <summary>
    /// Returns the inverse dequant matrix.
    /// </summary>
    /// <param name="strategy">The quant kind</param>
    /// <param name="c">The quantization index</param>
    /// <returns>The inverse dequant matrix.</returns>
    public ReadOnlySpan<float> InverseDequantMatrix(JxlAcStrategyType strategy, int c)
        => this.dequant!.GetInverseMatrix(strategy, c);

    /// <summary>
    /// Returns the DC quantization step.
    /// </summary>
    /// <param name="c">The quantization index</param>
    /// <returns>The DC quantization step</returns>
    public float GetDcStep(int c) => this.InverseQuantDc * this.dequant!.GetDcQuant(c);

    /// <summary>
    /// Returns the inverse DC quantization step.
    /// </summary>
    /// <param name="c">The quantization index</param>
    /// <returns>The inverse DC quantization step</returns>
    public float GetInverseDcStep(int c) => this.dequant!.GetInverseDcQuant(c) * (this.Scale * this.quantDc);

    /// <summary>
    /// Creates JXL quantizer parameters with values reflecting those in this quantizer instance.
    /// </summary>
    /// <returns>The quantizer parameters.</returns>
    public JxlQuantizerParameters GetParameters() => new()
    {
        QuantDc = (uint)this.quantDc,
        GlobalScale = (uint)this.globalScale
    };

    /// <summary>
    /// Reads the quantizer values from the bit-stream.
    /// </summary>
    /// <param name="reader">The bit reader.</param>
    /// <exception cref="IOException">Thrown when it is not possible to parse the quantizer parameters.</exception>
    public void Decode(JxlBitReader reader)
    {
        JxlQuantizerParameters qp = new();
        if (!JxlBundle.Read(reader, qp))
        {
            throw new IOException("Could not read quantizer parameters");
        }

        this.globalScale = (int)qp.GlobalScale;
        this.quantDc = (int)qp.QuantDc;

        this.RecomputeFromGlobalScale();
    }

    /// <summary>
    /// Recomputes the scaling factors and quant.
    /// </summary>
    public void ComputeGlobalScaleAndQuant(float quantDc, float quantMedian, float quantMedianAbsd)
    {
        const int quantFieldTarget = 5;
        float scale = GlobalScaleDenominator * (quantMedian - quantMedianAbsd) / quantFieldTarget;

        scale = Math.Clamp(scale, 1, 1 << 15);

        int newGlobalScale = (int)scale;
        int scaledQuantDc = (int)(quantDc * GlobalScaleNumerator * 1.6f);

        if (newGlobalScale > scaledQuantDc)
        {
            newGlobalScale = scaledQuantDc;

            if (newGlobalScale <= 0)
            {
                newGlobalScale = 1;
            }
        }

        this.globalScale = newGlobalScale;

        this.RecomputeFromGlobalScale();

        float valueF = (quantDc * this.InverseGlobalScale) + 0.5f;
        float clipValueF = MathF.Min(1 << 16, valueF);
        int newQuant = (int)clipValueF;
        this.quantDc = newQuant;

        this.RecomputeFromGlobalScale();
    }

    /// <summary>
    /// Quantizes the specified rectangular selection.
    /// </summary>
    public void SetQuantFieldRect(JxlImageF qf, in Rectangle rect, JxlImageI rawQuantField)
    {
        for (int y = 0; y < rect.Height; y++)
        {
            ReadOnlySpan<float> rowQf = qf.GetRow(rect, y);
            Span<int> rowQi = rawQuantField.GetRow(rect, y);

            for (int x = 0; x < rect.Width; x++)
            {
                int val = Clamp((rowQf[x] * this.InverseGlobalScale) + 0.5f);

                rowQi[x] = val;
            }
        }
    }

    /// <summary>
    /// Set the quant field.
    /// </summary>
    public bool SetQuantField(Configuration configuration, float quantDc, JxlImageF qf, JxlImageI? rawQuantField)
    {
        IMemoryOwner<float> data = configuration.MemoryAllocator.Allocate<float>(qf.XSize * qf.YSize);
        Span<float> dataSpan = data.Memory.Span;

        for (int y = 0; y < qf.YSize; y++)
        {
            ReadOnlySpan<float> rowQf = qf.GetRow(y);

            for (int x = 0; x < qf.XSize; y++)
            {
                float quant = rowQf[x];

                dataSpan[(qf.XSize * y) + x] = quant;
            }
        }

        dataSpan[dataSpan.Length / 2] = JxlSpanHelper.NthElement(dataSpan, dataSpan.Length / 2);
        float quantMedian = dataSpan[dataSpan.Length / 2];

        IMemoryOwner<float> deviations = configuration.MemoryAllocator.Allocate<float>(dataSpan.Length);
        Span<float> deviationsSpan = deviations.Memory.Span;
        for (int i = 0; i < dataSpan.Length; i++)
        {
            deviationsSpan[i] = MathF.Abs(dataSpan[i] - quantMedian);
        }

        deviationsSpan[deviationsSpan.Length / 2] = JxlSpanHelper.NthElement(deviationsSpan, deviationsSpan.Length / 2);
        float quantMedianAbsd = deviationsSpan[deviationsSpan.Length / 2];

        this.ComputeGlobalScaleAndQuant(quantDc, quantMedian, quantMedianAbsd);

        if (rawQuantField != null)
        {
            if (rawQuantField.GetRectangle() != qf.GetRectangle())
            {
                data.Dispose();
                deviations.Dispose();

                return false;
            }

            this.SetQuantFieldRect(qf, qf.GetRectangle(), rawQuantField);
        }

        data.Dispose();
        deviations.Dispose();

        return true;
    }

    public void SetQuant(float quantDc, float quantAc, JxlImageI rawQuantField)
    {
        this.ComputeGlobalScaleAndQuant(quantDc, quantAc, 0);

        int value = Clamp((quantAc * this.InverseGlobalScale) + 0.5f);
        JxlImageOperations.FillImage(value, rawQuantField);
    }
}

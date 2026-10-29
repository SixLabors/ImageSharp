// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
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
    public const int MaxQuant = 256;

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
    private int dcQuantization;

    /// <summary>
    /// The zero bias.
    /// </summary>
    private readonly InlineArray3<float> zeroBias;

    /// <summary>
    /// The dequantization matrices used by this quantizer.
    /// </summary>
    private readonly JxlDequantMatrices dequantizationMatrices;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlQuantizer"/> class using the default
    /// DC quantizer &amp; scale factors.
    /// </summary>
    /// <param name="dequantizationMatrices">The matrices used to restore coefficient magnitudes.</param>
    public JxlQuantizer(JxlDequantMatrices dequantizationMatrices)
        : this(dequantizationMatrices, DefaultQuant, GlobalScaleDenominator / DefaultQuant)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlQuantizer"/> class.
    /// </summary>
    /// <param name="dequantizationMatrices">The matrices used to restore coefficient magnitudes.</param>
    /// <param name="dcQuantization">The DC quantizer.</param>
    /// <param name="globalScale">The scale factor.</param>
    public JxlQuantizer(JxlDequantMatrices dequantizationMatrices, int dcQuantization, int globalScale)
    {
        this.dequantizationMatrices = dequantizationMatrices;
        this.dcQuantization = dcQuantization;
        this.globalScale = globalScale;

        this.RecomputeFromGlobalScale();
        this.InverseQuantDc = this.InverseGlobalScale / this.dcQuantization;

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
        this.InverseQuantDc = this.InverseGlobalScale / this.dcQuantization;

        for (int channelIndex = 0; channelIndex < 3; channelIndex++)
        {
            this.mulDc[channelIndex] = this.GetDcStep(channelIndex);
            this.inverseMulDc[channelIndex] = this.GetInverseDcStep(channelIndex);
        }
    }

    /// <summary>
    /// Returns the dequantization matrix.
    /// </summary>
    /// <param name="strategy">The quant kind</param>
    /// <param name="channelIndex">The XYB channel index.</param>
    /// <returns>The dequantization matrix.</returns>
    public ReadOnlySpan<float> DequantMatrix(JxlAcStrategyType strategy, int channelIndex)
        => this.dequantizationMatrices.GetMatrix(strategy, channelIndex);

    /// <summary>
    /// Returns the inverse dequantization matrix.
    /// </summary>
    /// <param name="strategy">The quant kind</param>
    /// <param name="channelIndex">The XYB channel index.</param>
    /// <returns>The inverse dequantization matrix.</returns>
    public ReadOnlySpan<float> InverseDequantMatrix(JxlAcStrategyType strategy, int channelIndex)
        => this.dequantizationMatrices.GetInverseMatrix(strategy, channelIndex);

    /// <summary>
    /// Returns the DC quantization step.
    /// </summary>
    /// <param name="channelIndex">The XYB channel index.</param>
    /// <returns>The DC quantization step</returns>
    public float GetDcStep(int channelIndex) => this.InverseQuantDc * this.dequantizationMatrices.GetDcQuant(channelIndex);

    /// <summary>
    /// Returns the inverse DC quantization step.
    /// </summary>
    /// <param name="channelIndex">The XYB channel index.</param>
    /// <returns>The inverse DC quantization step</returns>
    public float GetInverseDcStep(int channelIndex) => this.dequantizationMatrices.GetInverseDcQuant(channelIndex) * (this.Scale * this.dcQuantization);

    /// <summary>
    /// Creates JXL quantizer parameters with values reflecting those in this quantizer instance.
    /// </summary>
    /// <returns>The quantizer parameters.</returns>
    public JxlQuantizerParameters GetParameters() => new()
    {
        QuantDc = (uint)this.dcQuantization,
        GlobalScale = (uint)this.globalScale
    };

    /// <summary>
    /// Reads the quantizer values from the bit-stream.
    /// </summary>
    /// <param name="reader">The bit reader.</param>
    /// <exception cref="IOException">Thrown when it is not possible to parse the quantizer parameters.</exception>
    public void Decode(JxlBitReader reader)
    {
        JxlQuantizerParameters parameters = new();
        if (!JxlBundle.Read(reader, parameters))
        {
            throw new IOException("Could not read quantizer parameters");
        }

        this.globalScale = (int)parameters.GlobalScale;
        this.dcQuantization = (int)parameters.QuantDc;

        this.RecomputeFromGlobalScale();
    }

    /// <summary>
    /// Recomputes the scaling factors and quant.
    /// </summary>
    /// <param name="dcQuantization">The requested DC quantization scale.</param>
    /// <param name="medianQuantization">The median of the per-block quantization values.</param>
    /// <param name="medianAbsoluteDeviation">The median absolute deviation from that median.</param>
    public void ComputeGlobalScaleAndQuant(float dcQuantization, float medianQuantization, float medianAbsoluteDeviation)
    {
        const int quantFieldTarget = 5;
        float scale = GlobalScaleDenominator * (medianQuantization - medianAbsoluteDeviation) / quantFieldTarget;

        scale = Math.Clamp(scale, 1, 1 << 15);

        int newGlobalScale = (int)scale;
        int scaledQuantDc = (int)(dcQuantization * GlobalScaleNumerator * 1.6f);

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

        float valueF = (dcQuantization * this.InverseGlobalScale) + 0.5f;
        float clipValueF = MathF.Min(1 << 16, valueF);
        int newQuant = (int)clipValueF;
        this.dcQuantization = newQuant;

        this.RecomputeFromGlobalScale();
    }

    /// <summary>
    /// Converts floating-point quantization values to integers within a block region.
    /// </summary>
    /// <param name="quantizationField">The source per-block quantization values.</param>
    /// <param name="blockBounds">The region in blocks, applied to both fields.</param>
    /// <param name="quantizedField">The destination integer quantization values.</param>
    public void SetQuantFieldRect(JxlPlane<float> quantizationField, in Rectangle blockBounds, JxlPlane<int> quantizedField)
    {
        ReadOnlySpan<float> source = quantizationField.GetRow(0);
        Span<int> destination = quantizedField.GetRow(0);

        for (int y = 0; y < blockBounds.Height; y++)
        {
            ReadOnlySpan<float> quantizationRow = source[(((blockBounds.Top + y) * quantizationField.PixelsPerRow) + blockBounds.Left)..];
            Span<int> quantizedRow = destination[(((blockBounds.Top + y) * quantizedField.PixelsPerRow) + blockBounds.Left)..];

            for (int x = 0; x < blockBounds.Width; x++)
            {
                int val = Clamp((quantizationRow[x] * this.InverseGlobalScale) + 0.5f);

                quantizedRow[x] = val;
            }
        }
    }

    /// <summary>
    /// Sets the global quantization scale and optionally writes the integer quantization field.
    /// </summary>
    /// <param name="dcQuantization">The requested DC quantization scale.</param>
    /// <param name="quantizationField">The source per-block quantization values.</param>
    /// <param name="quantizedField">The destination integer field, or null to update only the global scale.</param>
    /// <param name="scratch">Reusable storage with capacity for every logical sample. It must not overlap either field.</param>
    /// <returns>Whether the destination field has matching dimensions.</returns>
    public bool SetQuantField(float dcQuantization, JxlPlane<float> quantizationField, JxlPlane<int>? quantizedField, Span<float> scratch)
    {
        if (quantizedField is not null && quantizedField.GetRectangle() != quantizationField.GetRectangle())
        {
            return false;
        }

        // A valid frame supplies one value per DCT block. Copy logical samples only: GetRow also exposes
        // the plane's padded stride and following rows. Selection must not include that storage or mutate the source field.
        int sampleCount = quantizationField.XSize * quantizationField.YSize;
        Span<float> samples = scratch[..sampleCount];
        ReadOnlySpan<float> source = quantizationField.GetRow(0);

        for (int y = 0; y < quantizationField.YSize; y++)
        {
            ReadOnlySpan<float> row = source.Slice(y * quantizationField.PixelsPerRow, quantizationField.XSize);
            row.CopyTo(samples.Slice(y * quantizationField.XSize, quantizationField.XSize));
        }

        float medianQuantization = samples.NthElement(sampleCount / 2);

        // Selection only permutes the copied values. Once their median is retained, the same allocation can
        // hold absolute deviations. Shared tensor operations provide all SIMD widths and the scalar fallback.
        TensorPrimitives.Subtract(samples, medianQuantization, samples);
        TensorPrimitives.Abs(samples, samples);
        float medianAbsoluteDeviation = samples.NthElement(sampleCount / 2);

        this.ComputeGlobalScaleAndQuant(dcQuantization, medianQuantization, medianAbsoluteDeviation);

        if (quantizedField is not null)
        {
            this.SetQuantFieldRect(quantizationField, quantizationField.GetRectangle(), quantizedField);
        }

        return true;
    }

    /// <summary>
    /// Sets uniform AC quantization and the requested DC quantization scale.
    /// </summary>
    /// <param name="dcQuantization">The requested DC quantization scale.</param>
    /// <param name="acQuantization">The AC quantization value for every block.</param>
    /// <param name="quantizedField">The destination integer quantization field.</param>
    public void SetQuant(float dcQuantization, float acQuantization, JxlPlane<int> quantizedField)
    {
        this.ComputeGlobalScaleAndQuant(dcQuantization, acQuantization, 0);

        int value = Clamp((acQuantization * this.InverseGlobalScale) + 0.5f);
        JxlImageOperations.FillImage(value, quantizedField);
    }
}

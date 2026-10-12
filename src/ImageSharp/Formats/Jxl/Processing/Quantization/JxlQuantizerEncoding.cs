// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// Specifies weights and quantizer modes.
/// </summary>
internal sealed class JxlQuantizerEncoding
{
    public JxlQuantizerEncoding()
    {
    }

    public JxlQuantizerEncoding(JxlQuantizerEncoding other)
    {
        // Simple shallow copy
        this.AfvWeights = other.AfvWeights;
        this.Dct2Weights = other.Dct2Weights;
        this.Dct4Multipliers = other.Dct4Multipliers;
        this.Dct4x8Multipliers = other.Dct4x8Multipliers;
        this.DctParameters = other.DctParameters;
        this.DctParametersAfv4x4 = other.DctParametersAfv4x4;
        this.IdWeights = other.IdWeights;
        this.Mode = other.Mode;
        this.Predefined = other.Predefined;
        this.QuantizationTable = other.QuantizationTable;
        this.QuantizationTableDenominator = other.QuantizationTableDenominator;

        if (other.QuantizationTable is not null)
        {
            // Do a deep clone for the quantization table.
            // Using AsSpan() should be way faster than a normal array copy...
            this.QuantizationTable = GC.AllocateUninitializedArray<int>(other.QuantizationTable.Length);
            other.QuantizationTable.AsSpan().CopyTo(this.QuantizationTable);
        }
    }

    /// <summary>
    /// Gets or sets the kind of transform used for this quantizer encoding.
    /// </summary>
    public JxlQuantMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the weights for DCT4+ tables.
    /// </summary>
    public JxlDctQuantWeightParameters? DctParameters { get; set; }

    /// <summary>
    /// Gets or sets the weights for the 4x4 sub-block in AFV.
    /// </summary>
    public JxlDctQuantWeightParameters? DctParametersAfv4x4 { get; set; }

    /// <summary>
    /// Gets or sets the weights for the identity transform.
    /// </summary>
    public float[][]? IdWeights { get; set; }

    /// <summary>
    /// Gets or sets the weights for the DCT2 transform.
    /// </summary>
    public float[][]? Dct2Weights { get; set; }

    /// <summary>
    /// Gets or sets the multipliers for the DCT4 transform.
    /// </summary>
    public float[][]? Dct4Multipliers { get; set; }

    /// <summary>
    /// Gets or sets the weights for the AFV transform.
    /// </summary>
    public float[][]? AfvWeights { get; set; }

    /// <summary>
    /// Gets or sets the multipliers for the 4x8 DCT block-based transform.
    /// </summary>
    public float[]? Dct4x8Multipliers { get; set; }

    /// <summary>
    /// Gets or sets the explicit quantization table (like in JPEG).
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="Mode"/> == <see cref="JxlQuantMode.Raw"/>.
    /// </remarks>
    public int[]? QuantizationTable { get; set; }

    /// <summary>
    /// Gets or sets the denominator for each item in the explicit quantization table.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="Mode"/> == <see cref="JxlQuantMode.Raw"/>.
    /// </remarks>
    public float QuantizationTableDenominator { get; set; } = 1f / (8 * 255);

    /// <summary>
    /// Gets or sets a value indicating which predefined table to use. The value is
    /// only used when <see cref="Mode"/> == <see cref="JxlQuantMode.Library"/>.
    /// </summary>
    public byte Predefined { get; set; }

    /// <summary>
    /// Creates a new quantizer encoding with the Library quantizer mode
    /// and the specified library index.
    /// </summary>
    /// <param name="libraryIndex">The library index (aka predefined table).</param>
    /// <returns>A new Library quantizer encoding.</returns>
    public static JxlQuantizerEncoding Library(int libraryIndex)
    {
        DebugGuard.MustBeLessThan(libraryIndex, JxlQuantWeights.NumPredefinedTables, nameof(libraryIndex));

        return new()
        {
            Mode = JxlQuantMode.Library,
            Predefined = (byte)libraryIndex
        };
    }

    /// <summary>
    /// Creates a new quantizer encoding with the Identity quantizer mode
    /// and the specified XYB/identity weights.
    /// </summary>
    /// <param name="xybWeights">Weights for the identity transform.</param>
    /// <returns>A new Identity quantizer encoding.</returns>
    public static JxlQuantizerEncoding Identity(float[][] xybWeights)
        => new()
        {
            Mode = JxlQuantMode.Id,
            IdWeights = xybWeights
        };

    /// <summary>
    /// Creates a new quantizer encoding with the DCT2x2 quantizer mode
    /// and the specified XYB/DCT2x2 weights.
    /// </summary>
    /// <param name="xybWeights">Weights for the DCT2x2 transform.</param>
    /// <returns>A new DCT2x2 quantizer encoding.</returns>
    public static JxlQuantizerEncoding Dct2(float[][] xybWeights)
        => new()
        {
            Mode = JxlQuantMode.Dct2,
            Dct2Weights = xybWeights
        };

    /// <summary>
    /// Creates a new quantizer encoding with the DCT4x4 quantizer mode,
    /// the specified XYB/DCT4x4 multipliers, and quantizer weight parameters.
    /// </summary>
    /// <param name="parameters">Quantizer weights for the DCT4x4 transform.</param>
    /// <param name="xybMul">XYB multipliers for the DCT4x4 transform.</param>
    /// <returns>A new DCT4x4 quantizer encoding.</returns>
    public static JxlQuantizerEncoding Dct4(JxlDctQuantWeightParameters parameters, float[][] xybMul)
        => new()
        {
            Mode = JxlQuantMode.Dct4,
            DctParameters = parameters,
            Dct4Multipliers = xybMul
        };

    /// <summary>
    /// Creates a new quantizer encoding with the DCT4x8 quantizer mode,
    /// the specified XYB/DCT4x8 multipliers, and quantizer weight parameters.
    /// </summary>
    /// <param name="parameters">Quantizer weights for the DCT4x8 transform.</param>
    /// <param name="xybMul">XYB multipliers for the DCT4x8 transform.</param>
    /// <returns>A new DCT4x8 quantizer encoding.</returns>
    public static JxlQuantizerEncoding Dct4x8(JxlDctQuantWeightParameters parameters, float[] xybMul)
        => new()
        {
            Mode = JxlQuantMode.Dct4x8,
            DctParameters = parameters,
            Dct4x8Multipliers = xybMul
        };

    /// <summary>
    /// Creates a new quantizer encoding with the DCT quantizer mode
    /// and quantizer weight parameters.
    /// </summary>
    /// <param name="parameters">Quantizer weights for the DCT transform.</param>
    /// <returns>A new DCT quantizer encoding.</returns>
    public static JxlQuantizerEncoding Dct(JxlDctQuantWeightParameters parameters)
        => new()
        {
            Mode = JxlQuantMode.Dct,
            DctParameters = parameters,
        };

    /// <summary>
    /// Creates a new quantizer encoding with the AFV quantizer mode,
    /// quantizer weight parameters for 4x8/4x4 blocks, and weights.
    /// </summary>
    /// <param name="params4x8">Quantizer weights for the 4x8 sub-block for the AFV transform.</param>
    /// <param name="params4x4">Quantizer weights for the 4x4 sub-block for the AFV transform.</param>
    /// <param name="weights">Quantizer weights.</param>
    /// <returns>A new DCT quantizer encoding.</returns>
    public static JxlQuantizerEncoding Afv(
        JxlDctQuantWeightParameters params4x8,
        JxlDctQuantWeightParameters params4x4,
        float[][] weights)
        => new()
        {
            Mode = JxlQuantMode.Afv,
            DctParameters = params4x8,
            AfvWeights = weights,
            DctParametersAfv4x4 = params4x4
        };

    /// <summary>
    /// Creates a new raw quantizer encoding.
    /// </summary>
    /// <param name="quantizationTable">The quantization table for raw quantization.</param>
    /// <param name="shift">The shift value for the denominator.</param>
    /// <returns>A raw quantizer encoding.</returns>
    public static JxlQuantizerEncoding Raw(Span<int> quantizationTable, int shift = 0)
    {
        JxlQuantizerEncoding encoding = new()
        {
            Mode = JxlQuantMode.Raw,
            QuantizationTableDenominator = (1 << shift) * (1f / (8 * 255)),
            QuantizationTable = GC.AllocateUninitializedArray<int>(quantizationTable.Length)
        };

        quantizationTable.CopyTo(encoding.QuantizationTable);
        return encoding;
    }
}

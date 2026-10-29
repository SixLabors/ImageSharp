// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// Owns one quantization descriptor's fixed parameters and optional raw integer table.
/// </summary>
/// <remarks>
/// Fixed mode weights use three rows of nine floats. Identity, DCT2, DCT4, DCT4x8 and AFV use the first 3, 6, 2, 1 and 9 values per row.
/// DCT distance bands use separate fixed value fields. Only raw mode needs allocator-owned sample storage.
/// </remarks>
internal sealed class JxlQuantizerEncoding : IDisposable
{
    private IMemoryOwner<int>? rawOwner;
    private Memory<int> rawAllocation;
    private Memory<int> rawTable;

    /// <summary>The primary DCT distance bands. Parsing updates this field by reference.</summary>
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "Fixed parameter storage must support reference parsing without value copies.")]
    public JxlDctQuantWeightParameters DctParameters;

    /// <summary>The AFV 4 by 4 sub-block's distance bands, independent of its primary 4 by 8 bands.</summary>
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "Fixed parameter storage must support reference parsing without value copies.")]
    public JxlDctQuantWeightParameters DctParametersAfv4x4;

    /// <summary>The three fixed weight rows. The selected mode determines the active prefix of each row.</summary>
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "Fixed parameter storage must support reference parsing without value copies.")]
    public InlineArray3<InlineArray9<float>> ModeWeights;

    /// <summary>Gets or sets the wire-defined quantization mode.</summary>
    public JxlQuantMode Mode { get; set; }

    /// <summary>Gets the logical raw table, stored as consecutive X, Y and B coefficient planes.</summary>
    /// <remarks>The borrowed memory remains valid until raw preparation or disposal changes its allocation.</remarks>
    public ReadOnlyMemory<int> QuantizationTable => this.rawTable;

    /// <summary>Gets or sets the raw table's positive F16 denominator.</summary>
    public float QuantizationTableDenominator { get; set; } = 1F / (8 * 255);

    /// <summary>Gets or sets the library index used only by Library mode.</summary>
    public byte Predefined { get; set; }

    /// <summary>Prepares raw sample storage at the table-read boundary and returns its logical writable span.</summary>
    /// <param name="configuration">The configuration that owns raw sample storage.</param>
    /// <param name="length">The complete X, Y and B table length established by the transform kind.</param>
    /// <returns>The destination for the decoded positive integer values.</returns>
    public Span<int> PrepareRawTable(Configuration configuration, int length)
    {
        if (length > this.rawAllocation.Length)
        {
            IMemoryOwner<int> replacement = configuration.MemoryAllocator.Allocate<int>(length);
            Memory<int> memory = replacement.Memory;
            this.rawOwner?.Dispose();
            this.rawOwner = replacement;
            this.rawAllocation = memory;
        }

        this.rawTable = this.rawAllocation[..length];
        return this.rawTable.Span;
    }

    /// <summary>Creates a library descriptor without allocating coefficient or raw table storage.</summary>
    /// <param name="libraryIndex">The predefined library index.</param>
    /// <returns>The new owned descriptor.</returns>
    public static JxlQuantizerEncoding Library(int libraryIndex)
    {
        DebugGuard.MustBeLessThan(libraryIndex, JxlQuantWeights.NumPredefinedTables, nameof(libraryIndex));
        return new() { Mode = JxlQuantMode.Library, Predefined = (byte)libraryIndex };
    }

    /// <summary>Copies identity weights into a new descriptor.</summary>
    /// <param name="xybWeights">Three consecutive channel rows of three values.</param>
    /// <returns>The new owned descriptor.</returns>
    public static JxlQuantizerEncoding Identity(ReadOnlySpan<float> xybWeights)
    {
        JxlQuantizerEncoding encoding = new() { Mode = JxlQuantMode.Id };
        encoding.CopyWeights(xybWeights, 3);
        return encoding;
    }

    /// <summary>Copies DCT2 weights into a new descriptor.</summary>
    /// <param name="xybWeights">Three consecutive channel rows of six values.</param>
    /// <returns>The new owned descriptor.</returns>
    public static JxlQuantizerEncoding Dct2(ReadOnlySpan<float> xybWeights)
    {
        JxlQuantizerEncoding encoding = new() { Mode = JxlQuantMode.Dct2 };
        encoding.CopyWeights(xybWeights, 6);
        return encoding;
    }

    /// <summary>Copies the DCT4 distance bands and sub-block multipliers into a new descriptor.</summary>
    /// <param name="parameters">The distance-band values, copied once.</param>
    /// <param name="xybMul">Three consecutive channel rows of two multipliers.</param>
    /// <returns>The new owned descriptor.</returns>
    public static JxlQuantizerEncoding Dct4(in JxlDctQuantWeightParameters parameters, ReadOnlySpan<float> xybMul)
    {
        JxlQuantizerEncoding encoding = new() { Mode = JxlQuantMode.Dct4, DctParameters = parameters };
        encoding.CopyWeights(xybMul, 2);
        return encoding;
    }

    /// <summary>Copies the DCT4x8 distance bands and channel multipliers into a new descriptor.</summary>
    /// <param name="parameters">The distance-band values, copied once.</param>
    /// <param name="xybMul">One multiplier for each of the three channels.</param>
    /// <returns>The new owned descriptor.</returns>
    public static JxlQuantizerEncoding Dct4x8(in JxlDctQuantWeightParameters parameters, ReadOnlySpan<float> xybMul)
    {
        JxlQuantizerEncoding encoding = new() { Mode = JxlQuantMode.Dct4x8, DctParameters = parameters };
        encoding.CopyWeights(xybMul, 1);
        return encoding;
    }

    /// <summary>Copies DCT distance bands into a new descriptor.</summary>
    /// <param name="parameters">The three distance-band rows and their active count.</param>
    /// <returns>The new owned descriptor.</returns>
    public static JxlQuantizerEncoding Dct(in JxlDctQuantWeightParameters parameters)
        => new() { Mode = JxlQuantMode.Dct, DctParameters = parameters };

    /// <summary>Copies independent AFV sub-block bands and fixed weights into a new descriptor.</summary>
    /// <param name="params4x8">The 4 by 8 distance-band values.</param>
    /// <param name="params4x4">The 4 by 4 distance-band values.</param>
    /// <param name="weights">Three consecutive channel rows of nine fixed weights.</param>
    /// <returns>The new owned descriptor.</returns>
    public static JxlQuantizerEncoding Afv(
        in JxlDctQuantWeightParameters params4x8,
        in JxlDctQuantWeightParameters params4x4,
        ReadOnlySpan<float> weights)
    {
        JxlQuantizerEncoding encoding = new()
        {
            Mode = JxlQuantMode.Afv,
            DctParameters = params4x8,
            DctParametersAfv4x4 = params4x4
        };

        encoding.CopyWeights(weights, 9);
        return encoding;
    }

    /// <summary>Copies packed channel prefixes into the common fixed mode bank.</summary>
    /// <param name="weights">Three consecutive channel prefixes.</param>
    /// <param name="valuesPerChannel">The mode's number of active values in each row.</param>
    private void CopyWeights(ReadOnlySpan<float> weights, int valuesPerChannel)
    {
        for (int c = 0; c < 3; c++)
        {
            Span<float> row = this.ModeWeights[c];
            weights.Slice(c * valuesPerChannel, valuesPerChannel).CopyTo(row);
        }
    }

    /// <summary>Releases raw storage. Fixed fields remain unchanged, including shared default parameters.</summary>
    public void Dispose()
    {
        this.rawOwner?.Dispose();
        this.rawOwner = null;
        this.rawAllocation = default;
        this.rawTable = default;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;

/// <summary>
/// Identifies a modular entropy stream within the frame's global, DC, metadata, quantization, or AC stream ranges.
/// </summary>
internal readonly struct JxlModularStreamId
{
    /// <summary>
    /// The global modular stream, which always has identifier zero.
    /// </summary>
    public static readonly JxlModularStreamId Global = new(JxlModularKind.GlobalData, 0, 0, 0);

    /// <summary>
    /// Initializes the fields used to select one frame-local modular entropy stream.
    /// </summary>
    /// <param name="kind">The stream range.</param>
    /// <param name="quantTableId">The quantization-table index when the kind selects a quantization table.</param>
    /// <param name="groupId">The DC or AC group index when the kind selects a group.</param>
    /// <param name="passId">The AC pass index, or the pass count when calculating the total stream count.</param>
    public JxlModularStreamId(JxlModularKind kind, int quantTableId, int groupId, int passId)
    {
        this.Kind = kind;
        this.QuantTableId = quantTableId;
        this.GroupId = groupId;
        this.PassId = passId;
    }

    /// <summary>
    /// Gets the stream range.
    /// </summary>
    public JxlModularKind Kind { get; }

    /// <summary>
    /// Gets the quantization-table index for a quantization-table stream.
    /// </summary>
    public int QuantTableId { get; }

    /// <summary>
    /// Gets the DC or AC group index for a group stream.
    /// </summary>
    public int GroupId { get; }

    /// <summary>
    /// Gets the AC pass index, or the pass count when calculating the total stream count.
    /// </summary>
    public int PassId { get; }

    /// <summary>
    /// Gets the entropy stream identifier from the parsed frame's DC and AC group counts.
    /// </summary>
    /// <param name="dimensions">The parsed frame dimensions whose section count passed the TOC limit.</param>
    /// <returns>The identifier used by modular prediction contexts.</returns>
    public int GetId(JxlFrameDimensions dimensions) => this.Kind switch
    {
        JxlModularKind.GlobalData => 0,
        JxlModularKind.VarDctDc => 1 + this.GroupId,
        JxlModularKind.ModularDc => 1 + dimensions.NumDcGroups + this.GroupId,
        JxlModularKind.AcMetadata => 1 + (2 * dimensions.NumDcGroups) + this.GroupId,
        JxlModularKind.QuantizerTable => 1 + (3 * dimensions.NumDcGroups) + this.QuantTableId,

        // Each AC pass occupies NumGroups consecutive identifiers after the fixed stream ranges.
        // The validated TOC bounds group/pass products to 65,536; these fixed offsets also fit within Int32.
        _ => 1 + (3 * dimensions.NumDcGroups) + JxlQuantizerConstants.NumberOfQuantizerTables + (dimensions.NumGroups * this.PassId) + this.GroupId
    };

    /// <summary>
    /// Selects a VarDCT DC stream.
    /// </summary>
    /// <param name="groupId">The validated DC group index.</param>
    /// <returns>The selected stream.</returns>
    public static JxlModularStreamId CreateVarDctDc(int groupId) => new(JxlModularKind.VarDctDc, 0, groupId, 0);

    /// <summary>
    /// Selects a modular DC stream.
    /// </summary>
    /// <param name="groupId">The validated DC group index.</param>
    /// <returns>The selected stream.</returns>
    public static JxlModularStreamId CreateModularDc(int groupId) => new(JxlModularKind.ModularDc, 0, groupId, 0);

    /// <summary>
    /// Selects a transform-strategy and quantization metadata stream.
    /// </summary>
    /// <param name="groupId">The validated DC group index.</param>
    /// <returns>The selected stream.</returns>
    public static JxlModularStreamId CreateAcMetadata(int groupId) => new(JxlModularKind.AcMetadata, 0, groupId, 0);

    /// <summary>
    /// Selects one AC group's modular stream within a pass.
    /// </summary>
    /// <param name="groupId">The validated AC group index.</param>
    /// <param name="passId">The validated AC pass index.</param>
    /// <returns>The selected stream.</returns>
    public static JxlModularStreamId CreateModularAc(int groupId, int passId) => new(JxlModularKind.ModularAc, 0, groupId, passId);

    /// <summary>
    /// Selects a raw quantization-table stream.
    /// </summary>
    /// <param name="quantTableId">The table index within the format's fixed quantization-table set.</param>
    /// <returns>The selected stream.</returns>
    public static JxlModularStreamId CreateQuantizerTable(int quantTableId)
    {
        DebugGuard.MustBeLessThan(quantTableId, JxlQuantizerConstants.NumberOfQuantizerTables, nameof(quantTableId));
        return new(JxlModularKind.QuantizerTable, quantTableId, 0, 0);
    }

    /// <summary>
    /// Gets the number of modular entropy streams reserved for a frame.
    /// </summary>
    /// <param name="frameDimensions">The parsed frame dimensions whose section count passed the TOC limit.</param>
    /// <param name="passes">The parsed coefficient-pass count.</param>
    /// <returns>The identifier immediately after the final AC group of the final pass.</returns>
    public static int Num(JxlFrameDimensions frameDimensions, int passes) => CreateModularAc(0, passes).GetId(frameDimensions);
}

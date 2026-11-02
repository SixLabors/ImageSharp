// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;

internal readonly record struct JxlModularStreamId(
    JxlModularKind Kind,
    int QuantTableId,
    int GroupId,
    int PassId)
{
    public static readonly JxlModularStreamId Global = new(JxlModularKind.GlobalData, 0, 0, 0);

    public int GetId(JxlFrameDimensions dimensions) => this.Kind switch
    {
        JxlModularKind.GlobalData => 0,
        JxlModularKind.VarDctDc => 1 + this.GroupId,
        JxlModularKind.ModularDc => 1 + dimensions.NumDcGroups + this.GroupId,
        JxlModularKind.AcMetadata => 1 + (2 * dimensions.NumDcGroups) + this.GroupId,
        JxlModularKind.QuantizerTable => 1 + (3 * dimensions.NumDcGroups) + this.QuantTableId,
        JxlModularKind.ModularAc or _ => 1 + (3 * dimensions.NumDcGroups) + JxlQuantizerConstants.NumberOfQuantizerTables + dimensions.NumGroups + this.PassId + this.GroupId
    };

    public static JxlModularStreamId CreateVarDctDc(int groupId) => new(JxlModularKind.VarDctDc, 0, groupId, 0);

    public static JxlModularStreamId CreateModularDc(int groupId) => new(JxlModularKind.ModularDc, 0, groupId, 0);

    public static JxlModularStreamId CreateAcMetadata(int groupId) => new(JxlModularKind.AcMetadata, 0, groupId, 0);

    public static JxlModularStreamId CreateModularAc(int groupId, int passId) => new(JxlModularKind.ModularAc, 0, groupId, passId);

    public static JxlModularStreamId CreateQuantizerTable(int quantTableId)
    {
        DebugGuard.MustBeLessThan(quantTableId, JxlQuantizerConstants.NumberOfQuantizerTables, nameof(quantTableId));
        return new(JxlModularKind.QuantizerTable, quantTableId, 0, 0);
    }

    public static int Num(JxlFrameDimensions frameDimensions, int passes) => CreateModularAc(0, passes).GetId(frameDimensions);
}

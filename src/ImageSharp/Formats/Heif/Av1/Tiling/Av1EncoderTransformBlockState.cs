// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the entropy syntax retained for one AV1 transform block.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = sizeof(int))]
internal struct Av1EncoderTransformBlockState
{
    /// <summary>
    /// Stores the position after the final nonzero coefficient.
    /// </summary>
    private ushort endOfBlock;

    /// <summary>
    /// Stores the selected transform type.
    /// </summary>
    private Av1TransformType transformType;

    /// <summary>
    /// Stores the skip context in bits 0 through 3 and DC-sign context in bits 4 and 5.
    /// </summary>
    public byte EntropyContext;

    /// <summary>
    /// Gets or sets the position after the final nonzero coefficient.
    /// </summary>
    public ushort EndOfBlock
    {
        readonly get => this.endOfBlock;
        set => this.endOfBlock = value;
    }

    /// <summary>
    /// Gets or sets the selected transform type.
    /// </summary>
    public Av1TransformType TransformType
    {
        readonly get => this.transformType;
        set => this.transformType = value;
    }
}

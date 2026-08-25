// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Stores the signed-bit ranges assigned to every stage of one AV1 transform axis.
/// </summary>
[InlineArray(Av1Transform2dFlipConfiguration.MaxStageNumber)]
internal struct Av1TransformStageRange
{
    /// <summary>
    /// The signed-bit range assigned to the first transform stage.
    /// </summary>
    private byte element0;
}

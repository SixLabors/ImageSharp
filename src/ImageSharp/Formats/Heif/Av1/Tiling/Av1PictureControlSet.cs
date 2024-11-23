// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1PictureControlSet
{
    public required Av1NeighborArrayUnit<byte>[] luma_dc_sign_level_coeff_na { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] cr_dc_sign_level_coeff_na { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] cb_dc_sign_level_coeff_na { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] txfm_context_array { get; internal set; }

    public required Av1SequenceControlSet Sequence { get; internal set; }

    public required Av1PictureParentControlSet Parent { get; internal set; }
}

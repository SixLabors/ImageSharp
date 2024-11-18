// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1TileWriter
{
    internal class Av1PictureControlSet
    {
        public required Av1NeighborArrayUnit[] luma_dc_sign_level_coeff_na { get; internal set; }

        public required Av1NeighborArrayUnit[] cr_dc_sign_level_coeff_na { get; internal set; }

        public required Av1NeighborArrayUnit[] cb_dc_sign_level_coeff_na { get; internal set; }

        public required Av1NeighborArrayUnit[] txfm_context_array { get; internal set; }

        public required Av1SequenceControlSet Sequence { get; internal set; }

        public required Av1PictureParentControlSet Parent { get; internal set; }
    }
}

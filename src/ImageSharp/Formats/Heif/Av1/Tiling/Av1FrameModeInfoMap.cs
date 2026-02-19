// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1FrameInfo
{
    /// <summary>
    /// Mapping of <see cref="Av1BlockModeInfo"/> instances, from position to index into the <see cref="Av1FrameInfo"/>.
    /// </summary>
    /// <remarks>
    /// For a visual representation of how this map looks in practice, see <seealso href="https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/v2.1.0/Docs/svt-av1-decoder-design.md?ref_type=tags#blockmodeinfo"/>
    /// </remarks>
    public class Av1FrameModeInfoMap
    {
        private readonly ushort[] offsets;
        private Size alignedModeInfoCount;

        public Av1FrameModeInfoMap(Size modeInfoCount, int superblockSizeLog2)
        {
            this.alignedModeInfoCount = new Size(
                modeInfoCount.Width * (1 << (superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2)),
                modeInfoCount.Height * (1 << (superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2)));
            this.NextIndex = 0;
            this.offsets = new ushort[this.alignedModeInfoCount.Width * this.alignedModeInfoCount.Height];
        }

        /// <summary>
        /// Gets the next index to use.
        /// </summary>
        public int NextIndex { get; private set; }

        /// <summary>
        /// Gets the mapped index for the given location.
        /// </summary>
        public int this[Point location]
        {
            get
            {
                int index = (location.Y * this.alignedModeInfoCount.Width) + location.X;
                return this.offsets[index];
            }
        }

        public void Update(Point modeInfoLocation, Av1BlockSize blockSize)
        {
            // Equivalent in SVT-Av1: EbDecNbr.c svt_aom_update_block_nbrs
            int bw4 = blockSize.Get4x4WideCount();
            int bh4 = blockSize.Get4x4HighCount();
            DebugGuard.MustBeGreaterThanOrEqualTo(modeInfoLocation.Y, 0, nameof(modeInfoLocation));
            DebugGuard.MustBeLessThanOrEqualTo(modeInfoLocation.Y + bh4, this.alignedModeInfoCount.Height, nameof(modeInfoLocation));
            DebugGuard.MustBeGreaterThanOrEqualTo(modeInfoLocation.X, 0, nameof(modeInfoLocation));
            DebugGuard.MustBeLessThanOrEqualTo(modeInfoLocation.X + bw4, this.alignedModeInfoCount.Width, nameof(modeInfoLocation));
            /* Update 4x4 nbr offset map */
            for (int i = modeInfoLocation.Y; i < modeInfoLocation.Y + bh4; i++)
            {
                Array.Fill(this.offsets, (ushort)this.NextIndex, (i * this.alignedModeInfoCount.Width) + modeInfoLocation.X, bw4);
            }

            this.NextIndex++;
        }
    }
}

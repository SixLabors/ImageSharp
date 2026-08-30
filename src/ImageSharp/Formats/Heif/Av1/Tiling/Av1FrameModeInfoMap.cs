// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Provides frame-wide lookup and storage for decoded AV1 mode-information blocks.
/// </summary>
internal partial class Av1FrameInfo
{
    /// <summary>
    /// Mapping of <see cref="Av1BlockModeInfo"/> instances, from position to index into the <see cref="Av1FrameInfo"/>.
    /// </summary>
    public class Av1FrameModeInfoMap
    {
        /// <summary>
        /// Stores the mode-information index assigned to each aligned 4x4 frame location.
        /// </summary>
        private readonly ushort[] offsets;

        /// <summary>
        /// The dimensions of <see cref="offsets"/> in 4x4 mode-information units.
        /// </summary>
        private readonly Size alignedModeInfoCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Av1FrameModeInfoMap"/> class.
        /// </summary>
        /// <param name="modeInfoCount">The aligned frame dimensions in 4x4 mode-information units.</param>
        public Av1FrameModeInfoMap(Size modeInfoCount)
        {
            this.alignedModeInfoCount = modeInfoCount;
            this.NextIndex = 0;
            this.offsets = new ushort[this.alignedModeInfoCount.Width * this.alignedModeInfoCount.Height];
        }

        /// <summary>
        /// Gets the next index to use.
        /// </summary>
        public int NextIndex { get; private set; }

        /// <summary>
        /// Gets the mode-information index mapped to the specified 4x4 location.
        /// </summary>
        /// <param name="location">The location in 4x4 mode-information units.</param>
        public int this[Point location]
        {
            get
            {
                int index = (location.Y * this.alignedModeInfoCount.Width) + location.X;
                return this.offsets[index];
            }
        }

        /// <summary>
        /// Maps every 4x4 location covered by a decoded block to the next mode-information index.
        /// </summary>
        /// <param name="modeInfoLocation">The block origin in 4x4 mode-information units.</param>
        /// <param name="blockSize">The decoded block size.</param>
        public void Update(Point modeInfoLocation, Av1BlockSize blockSize)
        {
            int bw4 = blockSize.Get4x4WideCount();
            int bh4 = blockSize.Get4x4HighCount();
            DebugGuard.MustBeGreaterThanOrEqualTo(modeInfoLocation.Y, 0, nameof(modeInfoLocation));
            DebugGuard.MustBeLessThanOrEqualTo(modeInfoLocation.Y + bh4, this.alignedModeInfoCount.Height, nameof(modeInfoLocation));
            DebugGuard.MustBeGreaterThanOrEqualTo(modeInfoLocation.X, 0, nameof(modeInfoLocation));
            DebugGuard.MustBeLessThanOrEqualTo(modeInfoLocation.X + bw4, this.alignedModeInfoCount.Width, nameof(modeInfoLocation));

            // Every 4x4 cell covered by the block must resolve to the same mode information,
            // because later blocks query their above and left neighbors at cell granularity.
            for (int i = modeInfoLocation.Y; i < modeInfoLocation.Y + bh4; i++)
            {
                Array.Fill(this.offsets, (ushort)this.NextIndex, (i * this.alignedModeInfoCount.Width) + modeInfoLocation.X, bw4);
            }

            this.NextIndex++;
        }
    }
}

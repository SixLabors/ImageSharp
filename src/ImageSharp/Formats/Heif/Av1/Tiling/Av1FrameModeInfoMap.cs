// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Provides frame-wide lookup and storage for decoded AV1 mode-information blocks.
/// </summary>
internal partial class Av1FrameInfo
{
    /// <summary>
    /// Mapping of <see cref="Av1BlockModeInfo"/> values, from position to index into the <see cref="Av1FrameInfo"/>.
    /// </summary>
    public sealed class Av1FrameModeInfoMap : IDisposable
    {
        /// <summary>
        /// Stores the mode-information index assigned to each aligned 4x4 frame location.
        /// </summary>
        private readonly MemoryGroup<int> offsets;

        /// <summary>
        /// The dimensions of <see cref="offsets"/> in 4x4 mode-information units.
        /// </summary>
        private readonly Size alignedModeInfoCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Av1FrameModeInfoMap"/> class using the default allocator.
        /// </summary>
        /// <param name="modeInfoCount">The aligned frame dimensions in 4x4 mode-information units.</param>
        public Av1FrameModeInfoMap(Size modeInfoCount)
            : this(Configuration.Default.MemoryAllocator, modeInfoCount)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Av1FrameModeInfoMap"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The allocator providing frame-sized storage.</param>
        /// <param name="modeInfoCount">The aligned frame dimensions in 4x4 mode-information units.</param>
        public Av1FrameModeInfoMap(MemoryAllocator memoryAllocator, Size modeInfoCount)
        {
            this.alignedModeInfoCount = modeInfoCount;
            this.NextIndex = 0;
            long offsetCount = (long)this.alignedModeInfoCount.Width * this.alignedModeInfoCount.Height;
            this.offsets = memoryAllocator.AllocateGroup<int>(offsetCount, 1, AllocationOptions.Clean);
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
                long offset = ((long)location.Y * this.alignedModeInfoCount.Width) + location.X;
                return this.offsets.GetRemainingSliceOfBuffer(offset)[0];
            }
        }

        /// <summary>
        /// Maps every 4x4 location covered by a decoded block to the next mode-information index.
        /// </summary>
        /// <param name="modeInfoLocation">The block origin in 4x4 mode-information units.</param>
        /// <param name="blockSize">The decoded block size.</param>
        /// <param name="storageIndex">The packed frame-storage index assigned to the decoded block.</param>
        public void Update(Point modeInfoLocation, Av1BlockSize blockSize, int storageIndex)
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
                long offset = ((long)i * this.alignedModeInfoCount.Width) + modeInfoLocation.X;
                int remaining = bw4;
                while (remaining > 0)
                {
                    Span<int> destination = this.offsets.GetRemainingSliceOfBuffer(offset);
                    int count = Math.Min(remaining, destination.Length);
                    destination[..count].Fill(storageIndex);
                    offset += count;
                    remaining -= count;
                }
            }

            this.NextIndex++;
        }

        /// <summary>
        /// Maps every 4x4 location covered by a decoded block to its traversal-order index.
        /// </summary>
        /// <param name="modeInfoLocation">The block origin in 4x4 mode-information units.</param>
        /// <param name="blockSize">The decoded block size.</param>
        public void Update(Point modeInfoLocation, Av1BlockSize blockSize)
            => this.Update(modeInfoLocation, blockSize, this.NextIndex);

        /// <inheritdoc/>
        public void Dispose() => this.offsets.Dispose();
    }
}

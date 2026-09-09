// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Configures restoration search from the frame's coding mode and speed.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Holds the frame-wide restoration search decisions.
    /// </summary>
    private readonly struct SearchSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchSettings"/> struct.
        /// </summary>
        public SearchSettings(
            HeifEncodingSpeed speed,
            bool stillPicture,
            Size frameSize,
            int qIndex,
            bool boosted,
            bool internalAltReference,
            bool screenContent,
            int superblockSize)
        {
            int level = (int)speed;
            int minimumDimension = Math.Min(frameSize.Width, frameSize.Height);
            this.EnableWiener = !stillPicture || level < 5;
            this.EnableSelfGuided = this.EnableWiener;
            this.EnableChroma = stillPicture || level < 2 || boosted;
            this.WienerWindow = !stillPicture || level >= 3 ? 5 : 7;
            this.WienerVariancePruning = level >= 3 ? 2 : level >= 2 ? 1 : 0;
            this.SelfGuidedPruning = !stillPicture && level >= 5 ? 2 : level >= 1 ? 1 : 0;
            this.SelfGuidedWienerPruning = level >= 3 ? (screenContent ? 1 : 2) : level >= 2 ? 1 : 0;
            this.DualSelfGuidedPenalty = level >= 1 ? 1.01 : 1;
            this.DownsampleWienerStatistics = !stillPicture && level >= 3;
            this.RefineWiener = stillPicture || level < 5;

            // Ordinary inter frames restrict self-guided search to the middle quantizer range.
            // Boosted and internal alternate-reference frames retain their more detailed search.
            if (level >= 4 && !boosted && !internalAltReference)
            {
                int lowQIndex = minimumDimension >= 720 ? 60 : 100;
                int highQIndex = minimumDimension >= 720 ? 160 : 180;
                if (qIndex <= lowQIndex || qIndex > highQIndex)
                {
                    this.EnableSelfGuided = false;
                    this.RefineWiener = false;
                }
            }

            int minimumUnitSize = 64;
            int maximumUnitSize = 256;
            if (level >= 1)
            {
                minimumUnitSize = minimumDimension >= 1440 ? 256 : minimumDimension >= 720 ? 128 : 64;
            }

            if (level >= 3 || (stillPicture && level >= 1))
            {
                minimumUnitSize = maximumUnitSize = qIndex <= 96 && minimumDimension < 1440 ? 128 : 256;
            }

            // Syntax cannot signal a restoration unit smaller than the sequence superblock.
            this.MinimumUnitSize = Math.Max(superblockSize, minimumUnitSize);
            this.MaximumUnitSize = Math.Max(this.MinimumUnitSize, maximumUnitSize);
        }

        /// <summary>
        /// Gets a value indicating whether Wiener candidates are searched.
        /// </summary>
        public bool EnableWiener { get; }

        /// <summary>
        /// Gets a value indicating whether self-guided candidates are searched.
        /// </summary>
        public bool EnableSelfGuided { get; }

        /// <summary>
        /// Gets a value indicating whether chroma planes are searched.
        /// </summary>
        public bool EnableChroma { get; }

        /// <summary>
        /// Gets the luma Wiener fitting window.
        /// </summary>
        public int WienerWindow { get; }

        /// <summary>
        /// Gets the source-variance pruning level.
        /// </summary>
        public int WienerVariancePruning { get; }

        /// <summary>
        /// Gets the self-guided parameter-set pruning level.
        /// </summary>
        public int SelfGuidedPruning { get; }

        /// <summary>
        /// Gets how the Wiener result controls subsequent self-guided search.
        /// </summary>
        public int SelfGuidedWienerPruning { get; }

        /// <summary>
        /// Gets the cost multiplier for dual-radius self-guided candidates.
        /// </summary>
        public double DualSelfGuidedPenalty { get; }

        /// <summary>
        /// Gets a value indicating whether eight-bit Wiener statistics use every fourth row.
        /// </summary>
        public bool DownsampleWienerStatistics { get; }

        /// <summary>
        /// Gets a value indicating whether fitted Wiener taps undergo coefficient refinement.
        /// </summary>
        public bool RefineWiener { get; }

        /// <summary>
        /// Gets the smallest restoration unit considered.
        /// </summary>
        public int MinimumUnitSize { get; }

        /// <summary>
        /// Gets the largest restoration unit considered.
        /// </summary>
        public int MaximumUnitSize { get; }
    }
}

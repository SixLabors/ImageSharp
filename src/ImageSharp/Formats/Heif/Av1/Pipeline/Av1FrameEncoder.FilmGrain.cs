// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Signals the film grain of the frames of a sequence.
/// </content>
internal static partial class Av1FrameEncoder
{
    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// Sets the film grain of the frame about to be written. libavif gives every frame time stamp 0. Reference:
        /// the aom_codec_encode() call of aomCodecEncodeImage().
        /// </summary>
        private protected void PrepareFilmGrain() => this.filmGrain?.PrepareFrame(this.FrameHeader, 0);

        /// <summary>
        /// Keeps the film grain of the coded frame in the reference slots it refreshes.
        /// </summary>
        private protected void RefreshFilmGrain() => this.filmGrain?.RefreshSlots(this.FrameHeader);
    }
}

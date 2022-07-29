// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    internal struct AnimationFrameData
    {
        /// <summary>
        /// The animation chunk size.
        /// </summary>
        public uint DataSize;

        /// <summary>
        /// The X coordinate of the upper left corner of the frame is Frame X * 2.
        /// </summary>
        public uint X;

        /// <summary>
        /// The Y coordinate of the upper left corner of the frame is Frame Y * 2.
        /// </summary>
        public uint Y;

        /// <summary>
        /// The width of the frame.
        /// </summary>
        public uint Width;

        /// <summary>
        /// The height of the frame.
        /// </summary>
        public uint Height;

        /// <summary>
        /// The time to wait before displaying the next frame, in 1 millisecond units.
        /// Note the interpretation of frame duration of 0 (and often smaller then 10) is implementation defined.
        /// </summary>
        public uint Duration;

        /// <summary>
        /// Indicates how transparent pixels of the current frame are to be blended with corresponding pixels of the previous canvas.
        /// </summary>
        public AnimationBlendingMethod BlendingMethod;

        /// <summary>
        /// Indicates how the current frame is to be treated after it has been displayed (before rendering the next frame) on the canvas.
        /// </summary>
        public AnimationDisposalMethod DisposalMethod;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Interface for a JPEG scan decoder.
    /// </summary>
    internal interface IJpegScanDecoder
    {
        /// <summary>
        /// Sets the reset interval.
        /// </summary>
        int ResetInterval { set; }

        /// <summary>
        /// Gets or sets the spectral selection start.
        /// </summary>
        int SpectralStart { get; set; }

        /// <summary>
        /// Gets or sets the spectral selection end.
        /// </summary>
        int SpectralEnd { get; set; }

        /// <summary>
        /// Gets or sets the successive approximation high bit end.
        /// </summary>
        int SuccessiveHigh { get; set; }

        /// <summary>
        /// Gets or sets the successive approximation low bit end.
        /// </summary>
        int SuccessiveLow { get; set; }

        /// <summary>
        /// Decodes the entropy coded data.
        /// </summary>
        /// <param name="scanComponentCount">Component count in the current scan.</param>
        void ParseEntropyCodedData(int scanComponentCount);

        /// <summary>
        /// Sets the JpegFrame and its components and injects the frame data into the spectral converter.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="jpegData">The raw JPEG data.</param>
        void InjectFrameData(JpegFrame frame, IRawJpegData jpegData);
    }
}

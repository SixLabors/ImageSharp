// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Converter used to convert jpeg spectral data.
    /// </summary>
    /// <remarks>
    /// This is tightly coupled with <see cref="HuffmanScanDecoder"/> and <see cref="JpegDecoderCore"/>.
    /// </remarks>
    internal abstract class SpectralConverter
    {
        /// <summary>
        /// Injects jpeg image decoding metadata.
        /// </summary>
        /// <remarks>
        /// This is guaranteed to be called only once at SOF marker by <see cref="HuffmanScanDecoder"/>.
        /// </remarks>
        /// <param name="frame"><see cref="JpegFrame"/> instance containing decoder-specific parameters.</param>
        /// <param name="jpegData"><see cref="IRawJpegData"/> instance containing decoder-specific parameters.</param>
        public abstract void InjectFrameData(JpegFrame frame, IRawJpegData jpegData);

        /// <summary>
        /// Called once per spectral stride for each component in <see cref="HuffmanScanDecoder"/>.
        /// This is called only for baseline interleaved jpegs.
        /// </summary>
        /// <remarks>
        /// Spectral 'stride' doesn't particularly mean 'single stride'.
        /// Actual stride height depends on the subsampling factor of the given component.
        /// </remarks>
        public abstract void ConvertStrideBaseline();
    }
}

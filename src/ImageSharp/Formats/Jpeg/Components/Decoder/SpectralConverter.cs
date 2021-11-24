// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;

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
        /// Gets a value indicating whether this converter has converted spectral
        /// data of the current image or not.
        /// </summary>
        protected bool Converted { get; private set; }

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

        /// <summary>
        /// Marks current converter state as 'converted'.
        /// </summary>
        /// <remarks>
        /// This must be called only for baseline interleaved jpeg's.
        /// </remarks>
        public void CommitConversion()
        {
            DebugGuard.IsFalse(this.Converted, nameof(this.Converted), $"{nameof(this.CommitConversion)} must be called only once");

            this.Converted = true;
        }

        /// <summary>
        /// Gets the color converter.
        /// </summary>
        /// <param name="frame">The jpeg frame with the color space to convert to.</param>
        /// <param name="jpegData">The raw JPEG data.</param>
        /// <returns>The color converter.</returns>
        protected virtual JpegColorConverter GetColorConverter(JpegFrame frame, IRawJpegData jpegData) => JpegColorConverter.GetConverter(jpegData.ColorSpace, frame.Precision);
    }
}

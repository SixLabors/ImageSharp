// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Converter used to convert jpeg spectral data to pixels.
    /// </summary>
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
        /// Converts single spectral jpeg stride to color stride in baseline
        /// decoding mode.
        /// </summary>
        /// <remarks>
        /// Called once per decoded spectral stride in <see cref="HuffmanScanDecoder"/>
        /// only for baseline interleaved jpeg images.
        /// Spectral 'stride' doesn't particularly mean 'single stride'.
        /// Actual stride height depends on the subsampling factor of the given image.
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
        protected virtual JpegColorConverterBase GetColorConverter(JpegFrame frame, IRawJpegData jpegData) => JpegColorConverterBase.GetConverter(jpegData.ColorSpace, frame.Precision);
    }
}

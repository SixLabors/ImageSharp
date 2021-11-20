// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Provides methods to convert decoded jpeg spectral data to color data.
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
        /// <param name="frame"><see cref="JpegFrame"/> instance containing decoder-specific parameters.</param>
        /// <param name="jpegData"><see cref="IRawJpegData"/> instance containing decoder-specific parameters.</param>
        public abstract void InjectFrameData(JpegFrame frame, IRawJpegData jpegData);

        /// <summary>
        /// Called once per spectral stride for each component in <see cref="HuffmanScanDecoder"/>.
        /// This is called only for baseline interleaved jpegs.
        /// </summary>
        /// <remarks>
        /// For YCbCr color space 'stride' may be higher than default 8 pixels
        /// used in 4:4:4 chroma coding.
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
            DebugGuard.IsFalse(this.Converted, nameof(this.Converted), "This method must be called only once.");
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

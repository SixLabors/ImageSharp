// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the frame.
    /// </summary>
    public class TiffFrameMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        public TiffFrameMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        /// <param name="other">The other tiff frame metadata.</param>
        private TiffFrameMetadata(TiffFrameMetadata other)
        {
            this.BitsPerPixel = other.BitsPerPixel;
            this.Compression = other.Compression;
            this.PhotometricInterpretation = other.PhotometricInterpretation;
            this.Predictor = other.Predictor;
        }

        /// <summary>
        /// Gets or sets the bits per pixel.
        /// </summary>
        public TiffBitsPerPixel? BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the compression scheme used on the image data.
        /// </summary>
        public TiffCompression? Compression { get; set; }

        /// <summary>
        /// Gets or sets the color space of the image data.
        /// </summary>
        public TiffPhotometricInterpretation? PhotometricInterpretation { get; set; }

        /// <summary>
        /// Gets or sets a mathematical operator that is applied to the image data before an encoding scheme is applied.
        /// </summary>
        public TiffPredictor? Predictor { get; set; }

        /// <summary>
        /// Returns a new <see cref="TiffFrameMetadata"/> instance parsed from the given Exif profile.
        /// </summary>
        /// <param name="profile">The Exif profile containing tiff frame directory tags to parse.
        /// If null, a new instance is created and parsed instead.</param>
        /// <returns>The <see cref="TiffFrameMetadata"/>.</returns>
        internal static TiffFrameMetadata Parse(ExifProfile profile)
        {
            var meta = new TiffFrameMetadata();
            Parse(meta, profile);
            return meta;
        }

        /// <summary>
        /// Parses the given Exif profile to populate the properties of the tiff frame meta data..
        /// </summary>
        /// <param name="meta">The tiff frame meta data.</param>
        /// <param name="profile">The Exif profile containing tiff frame directory tags.</param>
        internal static void Parse(TiffFrameMetadata meta, ExifProfile profile)
        {
            if (profile != null)
            {
                ushort[] bitsPerSample = profile.GetValue(ExifTag.BitsPerSample)?.Value;
                meta.BitsPerPixel = BitsPerPixelFromBitsPerSample(bitsPerSample);
                meta.Compression = (TiffCompression?)profile.GetValue(ExifTag.Compression)?.Value;
                meta.PhotometricInterpretation =
                    (TiffPhotometricInterpretation?)profile.GetValue(ExifTag.PhotometricInterpretation)?.Value;
                meta.Predictor = (TiffPredictor?)profile.GetValue(ExifTag.Predictor)?.Value;

                profile.RemoveValue(ExifTag.BitsPerSample);
                profile.RemoveValue(ExifTag.Compression);
                profile.RemoveValue(ExifTag.PhotometricInterpretation);
                profile.RemoveValue(ExifTag.Predictor);
            }
        }

        /// <summary>
        /// Gets the bits per pixel for the given bits per sample.
        /// </summary>
        /// <param name="bitsPerSample">The tiff bits per sample.</param>
        /// <returns>Bits per pixel.</returns>
        private static TiffBitsPerPixel? BitsPerPixelFromBitsPerSample(ushort[] bitsPerSample)
        {
            if (bitsPerSample == null)
            {
                return null;
            }

            int bitsPerPixel = 0;
            foreach (ushort bits in bitsPerSample)
            {
                bitsPerPixel += bits;
            }

            return (TiffBitsPerPixel)bitsPerPixel;
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffFrameMetadata(this);
    }
}

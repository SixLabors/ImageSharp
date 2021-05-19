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

        private TiffFrameMetadata(TiffFrameMetadata other)
        {
            this.BitsPerSample = other.BitsPerSample;
            this.BitsPerPixel = other.BitsPerPixel;
        }

        /// <summary>
        /// Gets or sets the number of bits per component.
        /// </summary>
        public TiffBitsPerSample? BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the bits per pixel.
        /// </summary>
        public TiffBitsPerPixel BitsPerPixel { get; set; }

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
            if (profile is null)
            {
                profile = new ExifProfile();
            }

            TiffPhotometricInterpretation photometricInterpretation = profile.GetValue(ExifTag.PhotometricInterpretation) != null
                ? (TiffPhotometricInterpretation)profile.GetValue(ExifTag.PhotometricInterpretation).Value
                : TiffPhotometricInterpretation.WhiteIsZero;

            ushort[] bits = profile.GetValue(ExifTag.BitsPerSample)?.Value;
            if (bits == null)
            {
                if (photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero
                    || photometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                {
                    meta.BitsPerSample = TiffBitsPerSample.Bit1;
                }

                meta.BitsPerSample = null;
            }
            else
            {
                meta.BitsPerSample = bits.GetBitsPerSample();
            }

            meta.BitsPerPixel = (TiffBitsPerPixel)meta.BitsPerSample.GetValueOrDefault().BitsPerPixel();
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffFrameMetadata(this);
    }
}

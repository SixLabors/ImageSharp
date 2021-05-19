// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
        private TiffFrameMetadata(TiffFrameMetadata other) => this.BitsPerPixel = other.BitsPerPixel;

        /// <summary>
        /// Gets or sets the bits per pixel.
        /// </summary>
        public TiffBitsPerPixel? BitsPerPixel { get; set; }

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
            profile ??= new ExifProfile();

            ushort[] bitsPerSample = profile.GetValue(ExifTag.BitsPerSample)?.Value;
            meta.BitsPerPixel = BitsPerPixelFromBitsPerSample(bitsPerSample);
        }

        /// <summary>
        /// Gets the bits per pixel for the given bits per sample.
        /// </summary>
        /// <param name="bitsPerSample">The tiff bits per sample.</param>
        /// <returns>Bits per pixel.</returns>
        private static TiffBitsPerPixel BitsPerPixelFromBitsPerSample(ushort[] bitsPerSample)
        {
            if (bitsPerSample == null)
            {
                return TiffBitsPerPixel.Bit24;
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

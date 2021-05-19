// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the frame.
    /// </summary>
    internal class TiffFrameMetadata : IDeepCloneable
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
        /// <param name="frameTags">The Tiff frame directory tags.</param>
        public TiffFrameMetadata(ExifProfile frameTags) => this.Initialize(frameTags ?? new ExifProfile());

        /// <summary>
        /// Gets or sets the number of bits per component.
        /// </summary>
        public TiffBitsPerSample? BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the bits per pixel.
        /// </summary>
        public TiffBitsPerPixel BitsPerPixel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class with a given ExifProfile.
        /// </summary>
        /// <param name="frameTags">The Tiff frame directory tags.</param>
        public void Initialize(ExifProfile frameTags)
        {
            TiffPhotometricInterpretation photometricInterpretation = frameTags.GetValue(ExifTag.PhotometricInterpretation) != null ?
                (TiffPhotometricInterpretation)frameTags.GetValue(ExifTag.PhotometricInterpretation).Value : TiffPhotometricInterpretation.WhiteIsZero;

            ushort[] bits = frameTags.GetValue(ExifTag.BitsPerSample)?.Value;
            if (bits == null)
            {
                if (photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero || photometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                {
                    this.BitsPerSample = TiffBitsPerSample.Bit1;
                }

                this.BitsPerSample = null;
            }
            else
            {
                this.BitsPerSample = bits.GetBitsPerSample();
            }

            this.BitsPerPixel = (TiffBitsPerPixel)this.BitsPerSample.GetValueOrDefault().BitsPerPixel();
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone()
        {
            var clone = new TiffFrameMetadata
            {
                BitsPerSample = this.BitsPerSample,
                BitsPerPixel = this.BitsPerPixel
            };

            return clone;
        }
    }
}

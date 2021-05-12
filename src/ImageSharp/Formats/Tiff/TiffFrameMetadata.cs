// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the frame.
    /// </summary>
    public class TiffFrameMetadata : IDeepCloneable
    {
        private const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

        private const TiffPredictor DefaultPredictor = TiffPredictor.None;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        public TiffFrameMetadata() => this.Initialize(new ExifProfile());

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        /// <param name="frameTags">The Tiff frame directory tags.</param>
        public TiffFrameMetadata(ExifProfile frameTags) => this.Initialize(frameTags);

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class with a given ExifProfile.
        /// </summary>
        /// <param name="frameTags">The Tiff frame directory tags.</param>
        public void Initialize(ExifProfile frameTags)
        {
            this.ExifProfile = frameTags;

            this.FillOrder = (TiffFillOrder?)this.ExifProfile.GetValue(ExifTag.FillOrder)?.Value ?? TiffFillOrder.MostSignificantBitFirst;
            this.Compression = this.ExifProfile.GetValue(ExifTag.Compression) != null ? (TiffCompression)this.ExifProfile.GetValue(ExifTag.Compression).Value : TiffCompression.None;
            this.SubfileType = (TiffNewSubfileType?)this.ExifProfile.GetValue(ExifTag.SubfileType)?.Value ?? TiffNewSubfileType.FullImage;
            this.OldSubfileType = (TiffSubfileType?)this.ExifProfile.GetValue(ExifTag.OldSubfileType)?.Value;
            this.HorizontalResolution = this.ExifProfile.GetValue(ExifTag.XResolution)?.Value.ToDouble();
            this.VerticalResolution = this.ExifProfile.GetValue(ExifTag.YResolution)?.Value.ToDouble();
            this.PlanarConfiguration = (TiffPlanarConfiguration?)this.ExifProfile.GetValue(ExifTag.PlanarConfiguration)?.Value ?? DefaultPlanarConfiguration;
            this.ResolutionUnit = UnitConverter.ExifProfileToResolutionUnit(this.ExifProfile);
            this.ColorMap = this.ExifProfile.GetValue(ExifTag.ColorMap)?.Value;
            this.ExtraSamples = this.ExifProfile.GetValue(ExifTag.ExtraSamples)?.Value;
            this.Predictor = (TiffPredictor?)this.ExifProfile.GetValue(ExifTag.Predictor)?.Value ?? DefaultPredictor;
            this.SampleFormat = this.ExifProfile.GetValue(ExifTag.SampleFormat)?.Value?.Select(a => (TiffSampleFormat)a).ToArray();
            this.SamplesPerPixel = this.ExifProfile.GetValue(ExifTag.SamplesPerPixel)?.Value;
            this.StripRowCounts = this.ExifProfile.GetValue(ExifTag.StripRowCounts)?.Value;
            this.RowsPerStrip = this.ExifProfile.GetValue(ExifTag.RowsPerStrip) != null ? this.ExifProfile.GetValue(ExifTag.RowsPerStrip).Value : TiffConstants.RowsPerStripInfinity;
            this.TileOffsets = this.ExifProfile.GetValue(ExifTag.TileOffsets)?.Value;

            this.PhotometricInterpretation = this.ExifProfile.GetValue(ExifTag.PhotometricInterpretation) != null ?
                (TiffPhotometricInterpretation)this.ExifProfile.GetValue(ExifTag.PhotometricInterpretation).Value : TiffPhotometricInterpretation.WhiteIsZero;

            // Required Fields for decoding the image.
            this.StripOffsets = this.ExifProfile.GetValue(ExifTag.StripOffsets)?.Value;
            this.StripByteCounts = this.ExifProfile.GetValue(ExifTag.StripByteCounts)?.Value;

            ushort[] bits = this.ExifProfile.GetValue(ExifTag.BitsPerSample)?.Value;
            if (bits == null)
            {
                if (this.PhotometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero || this.PhotometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                {
                    this.BitsPerSample = TiffBitsPerSample.Bit1;
                }

                this.BitsPerSample = null;
            }
            else
            {
                this.BitsPerSample = bits.GetBitsPerSample();
            }

            this.BitsPerPixel = this.BitsPerSample.GetValueOrDefault().BitsPerPixel();
        }

        /// <summary>
        /// Verifies that the required fields for decoding an image are present.
        /// If not, a ImageFormatException will be thrown.
        /// </summary>
        public void VerifyRequiredFieldsArePresent()
        {
            if (this.StripOffsets == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripOffsets are missing and are required for decoding the TIFF image!");
            }

            if (this.StripByteCounts == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripByteCounts are missing and are required for decoding the TIFF image!");
            }

            if (this.BitsPerSample == null)
            {
                TiffThrowHelper.ThrowNotSupported("The TIFF BitsPerSample entry is missing which is required to decode the image!");
            }
        }

        /// <summary>
        /// Gets the Tiff directory tags.
        /// </summary>
        public ExifProfile ExifProfile { get; internal set; }

        /// <summary>
        /// Gets or sets a general indication of the kind of data contained in this subfile.
        /// </summary>
        public TiffNewSubfileType? SubfileType { get; set; }

        /// <summary>
        /// Gets or sets a general indication of the kind of data contained in this subfile.
        /// </summary>
        public TiffSubfileType? OldSubfileType { get; set; }

        /// <summary>
        /// Gets or sets  the number of bits per component.
        /// </summary>
        public TiffBitsPerSample? BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the compression scheme used on the image data.
        /// </summary>
        /// <value>The compression scheme used on the image data.</value>
        public TiffCompression Compression { get; set; }

        /// <summary>
        /// Gets or sets the color space of the image data.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

        /// <summary>
        /// Gets or sets the logical order of bits within a byte.
        /// </summary>
        internal TiffFillOrder FillOrder { get; set; }

        /// <summary>
        /// Gets or sets for each strip, the byte offset of that strip.
        /// </summary>
        public Number[] StripOffsets { get; set; }

        /// <summary>
        /// Gets or sets the strip row counts.
        /// </summary>
        public uint[] StripRowCounts { get; set; }

        /// <summary>
        /// Gets or sets the number of components per pixel.
        /// </summary>
        public ushort? SamplesPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the number of rows per strip.
        /// </summary>
        public Number RowsPerStrip { get; set; }

        /// <summary>
        /// Gets or sets for each strip, the number of bytes in the strip after compression.
        /// </summary>
        public Number[] StripByteCounts { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in x-direction.
        /// </summary>
        public double? HorizontalResolution { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in y-direction.
        /// </summary>
        public double? VerticalResolution { get; set; }

        /// <summary>
        /// Gets or sets how the components of each pixel are stored.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the unit of measurement for XResolution and YResolution.
        /// </summary>
        public PixelResolutionUnit ResolutionUnit { get; set; }

        /// <summary>
        /// Gets or sets a color map for palette color images.
        /// </summary>
        public ushort[] ColorMap { get; set; }

        /// <summary>
        /// Gets or sets the description of extra components.
        /// </summary>
        public ushort[] ExtraSamples { get; set; }

        /// <summary>
        /// Gets or sets the tile offsets.
        /// </summary>
        public uint[] TileOffsets { get; set; }

        /// <summary>
        /// Gets or sets a mathematical operator that is applied to the image data before an encoding scheme is applied.
        /// </summary>
        public TiffPredictor Predictor { get; set; }

        /// <summary>
        /// Gets or sets the specifies how to interpret each data sample in a pixel.
        /// </summary>
        public TiffSampleFormat[] SampleFormat { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffFrameMetadata(this.ExifProfile.DeepClone());
    }
}

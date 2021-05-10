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

        private ExifProfile frameTags;

        /// <summary>
        /// Gets the Tiff directory tags.
        /// </summary>
        public ExifProfile ExifProfile
        {
            get => this.frameTags ??= new ExifProfile();
            internal set => this.frameTags = value;
        }

        /// <summary>
        /// Gets a general indication of the kind of data contained in this subfile.
        /// </summary>
        public TiffNewSubfileType SubfileType => (TiffNewSubfileType?)this.ExifProfile.GetValue(ExifTag.SubfileType)?.Value ?? TiffNewSubfileType.FullImage;

        /// <summary>
        /// Gets a general indication of the kind of data contained in this subfile.
        /// </summary>
        public TiffSubfileType? OldSubfileType => (TiffSubfileType?)this.ExifProfile.GetValue(ExifTag.OldSubfileType)?.Value;

        /// <summary>
        /// Gets the number of bits per component.
        /// </summary>
        public TiffBitsPerSample BitsPerSample
        {
            get
            {
                ushort[] bits = this.ExifProfile.GetValue(ExifTag.BitsPerSample)?.Value;
                if (bits == null)
                {
                    if (this.PhotometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero
                        || this.PhotometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                    {
                        return TiffBitsPerSample.Bit1;
                    }

                    TiffThrowHelper.ThrowNotSupported("The TIFF BitsPerSample entry is missing which is required to decode the image.");
                }

                return bits.GetBitsPerSample();
            }
        }

        /// <summary>
        /// Gets the bits per pixel.
        /// </summary>
        public int BitsPerPixel => this.BitsPerSample.BitsPerPixel();

        /// <summary>
        /// Gets the compression scheme used on the image data.
        /// </summary>
        /// <value>The compression scheme used on the image data.</value>
        public TiffCompression Compression
        {
            get
            {
                IExifValue<ushort> compression = this.ExifProfile.GetValue(ExifTag.Compression);
                if (compression == null)
                {
                    return TiffCompression.None;
                }

                return (TiffCompression)compression.Value;
            }
        }

        /// <summary>
        /// Gets the color space of the image data.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation
        {
            get
            {
                IExifValue<ushort> photometricInterpretation = this.ExifProfile.GetValue(ExifTag.PhotometricInterpretation);
                if (photometricInterpretation == null)
                {
                    return TiffPhotometricInterpretation.WhiteIsZero;
                }

                return (TiffPhotometricInterpretation)photometricInterpretation.Value;
            }
        }

        /// <summary>
        /// Gets the logical order of bits within a byte.
        /// </summary>
        internal TiffFillOrder FillOrder => (TiffFillOrder?)this.ExifProfile.GetValue(ExifTag.FillOrder)?.Value ?? TiffFillOrder.MostSignificantBitFirst;

        /// <summary>
        /// Gets for each strip, the byte offset of that strip.
        /// </summary>
        public Number[] StripOffsets
        {
            get
            {
                IExifValue<Number[]> stripOffsets = this.ExifProfile.GetValue(ExifTag.StripOffsets);
                if (stripOffsets == null)
                {
                    TiffThrowHelper.ThrowImageFormatException("StripOffsets are missing");
                }

                return stripOffsets.Value;
            }
        }

        /// <summary>
        /// Gets the number of components per pixel.
        /// </summary>
        public ushort SamplesPerPixel => this.ExifProfile.GetValue(ExifTag.SamplesPerPixel).Value;

        /// <summary>
        /// Gets the number of rows per strip.
        /// </summary>
        public Number RowsPerStrip
        {
            get
            {
                IExifValue<Number> rowsPerStrip = this.ExifProfile.GetValue(ExifTag.RowsPerStrip);
                if (rowsPerStrip == null)
                {
                    return TiffConstants.RowsPerStripInfinity;
                }

                return rowsPerStrip.Value;
            }
        }

        /// <summary>
        /// Gets for each strip, the number of bytes in the strip after compression.
        /// </summary>
        public Number[] StripByteCounts
        {
            get
            {
                IExifValue<Number[]> stripByteCounts = this.ExifProfile.GetValue(ExifTag.StripByteCounts);
                if (stripByteCounts == null)
                {
                    TiffThrowHelper.ThrowImageFormatException("StripByteCounts are missing");
                }

                return stripByteCounts.Value;
            }
        }

        /// <summary>
        /// Gets the resolution of the image in x- direction.
        /// </summary>
        /// <value>The density of the image in x- direction.</value>
        public double? HorizontalResolution => this.ExifProfile.GetValue(ExifTag.XResolution)?.Value.ToDouble();

        /// <summary>
        /// Gets the resolution of the image in y- direction.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        public double? VerticalResolution => this.ExifProfile.GetValue(ExifTag.YResolution)?.Value.ToDouble();

        /// <summary>
        /// Gets how the components of each pixel are stored.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration => (TiffPlanarConfiguration?)this.ExifProfile.GetValue(ExifTag.PlanarConfiguration)?.Value ?? DefaultPlanarConfiguration;

        /// <summary>
        /// Gets the unit of measurement for XResolution and YResolution.
        /// </summary>
        public PixelResolutionUnit ResolutionUnit => UnitConverter.ExifProfileToResolutionUnit(this.ExifProfile);

        /// <summary>
        /// Gets a color map for palette color images.
        /// </summary>
        public ushort[] ColorMap => this.ExifProfile.GetValue(ExifTag.ColorMap)?.Value;

        /// <summary>
        /// Gets the description of extra components.
        /// </summary>
        public ushort[] ExtraSamples => this.ExifProfile.GetValue(ExifTag.ExtraSamples)?.Value;

        /// <summary>
        /// Gets a mathematical operator that is applied to the image data before an encoding scheme is applied.
        /// </summary>
        public TiffPredictor Predictor => (TiffPredictor?)this.ExifProfile.GetValue(ExifTag.Predictor)?.Value ?? DefaultPredictor;

        /// <summary>
        /// Gets the specifies how to interpret each data sample in a pixel.
        /// <see cref="SamplesPerPixel"/>
        /// </summary>
        public TiffSampleFormat[] SampleFormat => this.ExifProfile.GetValue(ExifTag.SampleFormat)?.Value?.Select(a => (TiffSampleFormat)a).ToArray();

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffFrameMetadata() { ExifProfile = this.ExifProfile.DeepClone() };

        private static bool IsFormatTag(ExifTagValue tag)
        {
            switch (tag)
            {
                case ExifTagValue.ImageWidth:
                case ExifTagValue.ImageLength:
                case ExifTagValue.ResolutionUnit:
                case ExifTagValue.XResolution:
                case ExifTagValue.YResolution:
                case ExifTagValue.Predictor:
                case ExifTagValue.PlanarConfiguration:
                case ExifTagValue.PhotometricInterpretation:
                case ExifTagValue.BitsPerSample:
                case ExifTagValue.ColorMap:
                    return true;
            }

            return false;
        }
    }
}

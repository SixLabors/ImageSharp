// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the frame.
    /// </summary>
    public class TiffFrameMetadata : IDeepCloneable
    {
        // 2 (Inch)
        internal const ushort DefaultResolutionUnit = 2;

        private const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

        private const TiffPredictor DefaultPredictor = TiffPredictor.None;

        private ExifProfile frameTags;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        public TiffFrameMetadata()
        {
        }

        /// <summary>
        /// Gets the Tiff directory tags.
        /// </summary>
        public ExifProfile FrameTags
        {
            get => this.frameTags ??= new ExifProfile();
            internal set => this.frameTags = value;
        }

        /// <summary>Gets a general indication of the kind of data contained in this subfile.</summary>
        /// <value>A general indication of the kind of data contained in this subfile.</value>
        public TiffNewSubfileType SubfileType => this.GetSingleEnum<TiffNewSubfileType, uint>(ExifTag.SubfileType, TiffNewSubfileType.FullImage);

        /// <summary>Gets a general indication of the kind of data contained in this subfile.</summary>
        /// <value>A general indication of the kind of data contained in this subfile.</value>
        public TiffSubfileType? OldSubfileType => this.GetSingleEnumNullable<TiffSubfileType, ushort>(ExifTag.OldSubfileType);

        /// <summary>
        /// Gets the number of columns in the image, i.e., the number of pixels per row.
        /// </summary>
        public Number Width => this.GetSingle<Number>(ExifTag.ImageWidth);

        /// <summary>
        /// Gets the number of rows of pixels in the image.
        /// </summary>
        public Number Height => this.GetSingle<Number>(ExifTag.ImageLength);

        /// <summary>
        /// Gets the number of bits per component.
        /// </summary>
        public ushort[] BitsPerSample
        {
            get
            {
                var bits = this.GetArray<ushort>(ExifTag.BitsPerSample, true);
                if (bits == null)
                {
                    if (this.PhotometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero
                        || this.PhotometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                    {
                        bits = new[] { (ushort)1 };
                    }
                    else
                    {
                        TiffThrowHelper.ThrowNotSupported("The TIFF BitsPerSample entry is missing.");
                    }
                }

                return bits;
            }
        }

        internal int BitsPerPixel
        {
            get
            {
                int bitsPerPixel = 0;
                foreach (var bits in this.BitsPerSample)
                {
                    bitsPerPixel += bits;
                }

                return bitsPerPixel;
            }
        }

        /// <summary>Gets the compression scheme used on the image data.</summary>
        /// <value>The compression scheme used on the image data.</value>
        public TiffCompression Compression => this.GetSingleEnum<TiffCompression, ushort>(ExifTag.Compression);

        /// <summary>
        /// Gets the color space of the image data.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation => this.GetSingleEnum<TiffPhotometricInterpretation, ushort>(ExifTag.PhotometricInterpretation);

        /// <summary>
        /// Gets the logical order of bits within a byte.
        /// </summary>
        internal TiffFillOrder FillOrder => this.GetSingleEnum<TiffFillOrder, ushort>(ExifTag.FillOrder, TiffFillOrder.MostSignificantBitFirst);

        /// <summary>
        /// Gets or sets the a string that describes the subject of the image.
        /// </summary>
        public string ImageDescription
        {
            get => this.GetString(ExifTag.ImageDescription);
            set => this.SetString(ExifTag.ImageDescription, value);
        }

        /// <summary>
        /// Gets or sets the scanner manufacturer.
        /// </summary>
        public string Make
        {
            get => this.GetString(ExifTag.Make);
            set => this.SetString(ExifTag.Make, value);
        }

        /// <summary>
        /// Gets or sets the scanner model name or number.
        /// </summary>
        public string Model
        {
            get => this.GetString(ExifTag.Model);
            set => this.SetString(ExifTag.Model, value);
        }

        /// <summary>Gets for each strip, the byte offset of that strip..</summary>
        public Number[] StripOffsets => this.GetArray<Number>(ExifTag.StripOffsets);

        /// <summary>
        /// Gets the number of components per pixel.
        /// </summary>
        public ushort SamplesPerPixel => this.GetSingle<ushort>(ExifTag.SamplesPerPixel);

        /// <summary>
        /// Gets the number of rows per strip.
        /// </summary>
        public Number RowsPerStrip => this.GetSingle<Number>(ExifTag.RowsPerStrip);

        /// <summary>
        /// Gets for each strip, the number of bytes in the strip after compression.
        /// </summary>
        public Number[] StripByteCounts => this.GetArray<Number>(ExifTag.StripByteCounts);

        /// <summary>Gets the resolution of the image in x- direction.</summary>
        /// <value>The density of the image in x- direction.</value>
        public double? HorizontalResolution => this.GetResolution(ExifTag.XResolution);

        /// <summary>
        /// Gets the resolution of the image in y- direction.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        public double? VerticalResolution => this.GetResolution(ExifTag.YResolution);

        /// <summary>
        /// Gets how the components of each pixel are stored.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration => this.GetSingleEnum<TiffPlanarConfiguration, ushort>(ExifTag.PlanarConfiguration, DefaultPlanarConfiguration);

        /// <summary>
        /// Gets the unit of measurement for XResolution and YResolution.
        /// </summary>
        public PixelResolutionUnit ResolutionUnit => this.GetResolutionUnit();

        /// <summary>
        /// Gets or sets the name and version number of the software package(s) used to create the image.
        /// </summary>
        public string Software
        {
            get => this.GetString(ExifTag.Software);
            set => this.SetString(ExifTag.Software, value);
        }

        /// <summary>
        /// Gets or sets the date and time of image creation.
        /// </summary>
        public string DateTime
        {
            get => this.GetString(ExifTag.DateTime);
            set => this.SetString(ExifTag.DateTime, value);
        }

        /// <summary>
        /// Gets or sets the person who created the image.
        /// </summary>
        public string Artist
        {
            get => this.GetString(ExifTag.Artist);
            set => this.SetString(ExifTag.Artist, value);
        }

        /// <summary>
        /// Gets or sets the computer and/or operating system in use at the time of image creation.
        /// </summary>
        public string HostComputer
        {
            get => this.GetString(ExifTag.HostComputer);
            set => this.SetString(ExifTag.HostComputer, value);
        }

        /// <summary>
        /// Gets a color map for palette color images.
        /// </summary>
        public ushort[] ColorMap => this.GetArray<ushort>(ExifTag.ColorMap, true);

        /// <summary>
        /// Gets the description of extra components.
        /// </summary>
        public ushort[] ExtraSamples => this.GetArray<ushort>(ExifTag.ExtraSamples, true);

        /// <summary>
        /// Gets or sets the copyright notice.
        /// </summary>
        public string Copyright
        {
            get => this.GetString(ExifTag.Copyright);
            set => this.SetString(ExifTag.Copyright, value);
        }

        /// <summary>
        /// Gets a mathematical operator that is applied to the image data before an encoding scheme is applied.
        /// </summary>
        public TiffPredictor Predictor => this.GetSingleEnum<TiffPredictor, ushort>(ExifTag.Predictor, DefaultPredictor);

        /// <summary>
        /// Gets the specifies how to interpret each data sample in a pixel.
        /// <see cref="SamplesPerPixel"/>
        /// </summary>
        public TiffSampleFormat[] SampleFormat => this.GetEnumArray<TiffSampleFormat, ushort>(ExifTag.SampleFormat, true);

        /// <summary>
        /// Clears the metadata.
        /// </summary>
        public void ClearMetadata()
        {
            var tags = new List<IExifValue>();
            foreach (IExifValue entry in this.FrameTags.Values)
            {
                switch ((ExifTagValue)(ushort)entry.Tag)
                {
                    case ExifTagValue.ImageWidth:
                    case ExifTagValue.ImageLength:
                    case ExifTagValue.ResolutionUnit:
                    case ExifTagValue.XResolution:
                    case ExifTagValue.YResolution:
                    //// image format tags
                    case ExifTagValue.Predictor:
                    case ExifTagValue.PlanarConfiguration:
                    case ExifTagValue.PhotometricInterpretation:
                    case ExifTagValue.BitsPerSample:
                    case ExifTagValue.ColorMap:
                        tags.Add(entry);
                        break;
                }
            }

            var profile = new ExifProfile();
            profile.InitializeInternal(tags);
            this.FrameTags = profile;
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffFrameMetadata() { FrameTags = this.FrameTags.DeepClone() };
    }
}

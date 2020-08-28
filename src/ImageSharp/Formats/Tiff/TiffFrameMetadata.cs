// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the frame.
    /// </summary>
    public class TiffFrameMetadata : IDeepCloneable
    {
        private const TiffResolutionUnit DefaultResolutionUnit = TiffResolutionUnit.Inch;

        private const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

        private const TiffPredictor DefaultPredictor = TiffPredictor.None;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        public TiffFrameMetadata()
        {
        }

        /// <summary>
        /// Gets or sets the Tiff directory tags list.
        /// </summary>
        public IList<IExifValue> Tags { get; set; }

        /// <summary>Gets a general indication of the kind of data contained in this subfile.</summary>
        /// <value>A general indication of the kind of data contained in this subfile.</value>
        public TiffNewSubfileType NewSubfileType => this.GetSingleEnum<TiffNewSubfileType, uint>(ExifTag.SubfileType, TiffNewSubfileType.FullImage);

        /// <summary>Gets a general indication of the kind of data contained in this subfile.</summary>
        /// <value>A general indication of the kind of data contained in this subfile.</value>
        public TiffSubfileType? SubfileType => this.GetSingleEnumNullable<TiffSubfileType, uint>(ExifTag.OldSubfileType);

        /// <summary>
        /// Gets the number of columns in the image, i.e., the number of pixels per row.
        /// </summary>
        public uint Width => this.GetSingle<uint>(ExifTag.ImageWidth);

        /// <summary>
        /// Gets the number of rows of pixels in the image.
        /// </summary>
        public uint Height => this.GetSingle<uint>(ExifTag.ImageLength);

        /// <summary>
        /// Gets the number of bits per component.
        /// </summary>
        public ushort[] BitsPerSample => this.GetArray<ushort>(ExifTag.BitsPerSample, true);

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
        /// Gets the a string that describes the subject of the image.
        /// </summary>
        public string ImageDescription => this.GetString(ExifTag.ImageDescription);

        /// <summary>
        /// Gets the scanner manufacturer.
        /// </summary>
        public string Make => this.GetString(ExifTag.Make);

        /// <summary>
        /// Gets the scanner model name or number.
        /// </summary>
        public string Model => this.GetString(ExifTag.Model);

        /// <summary>Gets for each strip, the byte offset of that strip..</summary>
        public uint[] StripOffsets => this.GetArray<uint>(ExifTag.StripOffsets);

        /// <summary>
        /// Gets the number of components per pixel.
        /// </summary>
        public ushort SamplesPerPixel => this.GetSingle<ushort>(ExifTag.SamplesPerPixel);

        /// <summary>
        /// Gets the number of rows per strip.
        /// </summary>
        public uint RowsPerStrip => this.GetSingle<uint>(ExifTag.RowsPerStrip);

        /// <summary>
        /// Gets for each strip, the number of bytes in the strip after compression.
        /// </summary>
        public uint[] StripByteCounts => this.GetArray<uint>(ExifTag.StripByteCounts);

        /// <summary>Gets the resolution of the image in x- direction.</summary>
        /// <value>The density of the image in x- direction.</value>
        public double? HorizontalResolution
        {
            get
            {
                if (this.ResolutionUnit != TiffResolutionUnit.None)
                {
                    double resolutionUnitFactor = this.ResolutionUnit == TiffResolutionUnit.Centimeter ? 2.54 : 1.0;

                    if (this.TryGetSingle(ExifTag.XResolution, out Rational xResolution))
                    {
                        return xResolution.ToDouble() * resolutionUnitFactor;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the resolution of the image in y- direction.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        public double? VerticalResolution
        {
            get
            {
                if (this.ResolutionUnit != TiffResolutionUnit.None)
                {
                    double resolutionUnitFactor = this.ResolutionUnit == TiffResolutionUnit.Centimeter ? 2.54 : 1.0;

                    if (this.TryGetSingle(ExifTag.YResolution, out Rational yResolution))
                    {
                        return yResolution.ToDouble() * resolutionUnitFactor;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets how the components of each pixel are stored.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration => this.GetSingleEnum<TiffPlanarConfiguration, ushort>(ExifTag.PlanarConfiguration, DefaultPlanarConfiguration);

        /// <summary>
        /// Gets the unit of measurement for XResolution and YResolution.
        /// </summary>
        public TiffResolutionUnit ResolutionUnit => this.GetSingleEnum<TiffResolutionUnit, ushort>(ExifTag.ResolutionUnit, DefaultResolutionUnit);

        /// <summary>
        /// Gets the name and version number of the software package(s) used to create the image.
        /// </summary>
        public string Software => this.GetString(ExifTag.Software);

        /// <summary>
        /// Gets the date and time of image creation.
        /// </summary>
        public string DateTime => this.GetString(ExifTag.DateTime);

        /// <summary>
        /// Gets the person who created the image.
        /// </summary>
        public string Artist => this.GetString(ExifTag.Artist);

        /// <summary>
        /// Gets the computer and/or operating system in use at the time of image creation.
        /// </summary>
        public string HostComputer => this.GetString(ExifTag.HostComputer);

        /// <summary>
        /// Gets a color map for palette color images.
        /// </summary>
        public ushort[] ColorMap => this.GetArray<ushort>(ExifTag.ColorMap, true);

        /// <summary>
        /// Gets the description of extra components.
        /// </summary>
        public ushort[] ExtraSamples => this.GetArray<ushort>(ExifTag.ExtraSamples, true);

        /// <summary>
        /// Gets the copyright notice.
        /// </summary>
        public string Copyright => this.GetString(ExifTag.Copyright);

        /// <summary>
        /// Gets a mathematical operator that is applied to the image data before an encoding scheme is applied.
        /// </summary>
        public TiffPredictor Predictor => this.GetSingleEnum<TiffPredictor, ushort>(ExifTag.Predictor, DefaultPredictor);

        /// <summary>
        /// Gets the specifies how to interpret each data sample in a pixel.
        /// <see cref="SamplesPerPixel"/>
        /// </summary>
        public TiffSampleFormat[] SampleFormat => this.GetEnumArray<TiffSampleFormat, ushort>(ExifTag.SampleFormat, true);

        internal T[] GetArray<T>(ExifTag tag, bool optional = false)
            where T : struct
        {
            if (this.TryGetArray(tag, out T[] result))
            {
                return result;
            }

            if (!optional)
            {
                throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
            }

            return null;
        }

        private bool TryGetArray<T>(ExifTag tag, out T[] result)
            where T : struct
        {
            foreach (IExifValue entry in this.Tags)
            {
                if (entry.Tag == tag)
                {
                    DebugGuard.IsTrue(entry.IsArray, "Expected array entry");

                    result = (T[])entry.GetValue();
                    return true;
                }
            }

            result = null;
            return false;
        }

        private TEnum[] GetEnumArray<TEnum, TTagValue>(ExifTag tag, bool optional = false)
          where TEnum : struct
          where TTagValue : struct
        {
            if (this.TryGetArray(tag, out TTagValue[] result))
            {
                // todo: improve
                return result.Select(a => (TEnum)(object)a).ToArray();
            }

            if (!optional)
            {
                throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
            }

            return null;
        }

        private string GetString(ExifTag tag)
        {
            foreach (IExifValue entry in this.Tags)
            {
                if (entry.Tag == tag)
                {
                    DebugGuard.IsTrue(entry.DataType == ExifDataType.Ascii, "Expected string entry");
                    object value = entry.GetValue();
                    DebugGuard.IsTrue(value is string, "Expected string entry");

                    return (string)value;
                }
            }

            return null;
        }

        private TEnum? GetSingleEnumNullable<TEnum, TTagValue>(ExifTag tag)
          where TEnum : struct
          where TTagValue : struct
        {
            if (!this.TryGetSingle(tag, out TTagValue value))
            {
                return null;
            }

            return (TEnum)(object)value;
        }

        private TEnum GetSingleEnum<TEnum, TTagValue>(ExifTag tag, TEnum? defaultValue = null)
            where TEnum : struct
            where TTagValue : struct
        => this.GetSingleEnumNullable<TEnum, TTagValue>(tag) ?? (defaultValue != null ? defaultValue.Value : throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag)));

        private T GetSingle<T>(ExifTag tag)
            where T : struct
        {
            if (this.TryGetSingle(tag, out T result))
            {
                return result;
            }

            throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
        }

        private bool TryGetSingle<T>(ExifTag tag, out T result)
            where T : struct
        {
            foreach (IExifValue entry in this.Tags)
            {
                if (entry.Tag == tag)
                {
                    DebugGuard.IsTrue(!entry.IsArray, "Expected non array entry");

                    object value = entry.GetValue();

                    result = (T)value;
                    return true;
                }
            }

            result = default;
            return false;
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone()
        {
            var tags = new List<IExifValue>();
            foreach (IExifValue entry in this.Tags)
            {
                tags.Add(entry.DeepClone());
            }

            return new TiffFrameMetadata() { Tags = tags };
        }
    }
}

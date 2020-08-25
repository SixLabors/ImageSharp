// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
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
        public uint Width => this.GetSingleUInt(ExifTag.ImageWidth);

        /// <summary>
        /// Gets the number of rows of pixels in the image.
        /// </summary>
        public uint Height => this.GetSingleUInt(ExifTag.ImageLength);

        /// <summary>
        /// Gets the number of bits per component.
        /// </summary>
        public ushort[] BitsPerSample => this.GetArrayValue<ushort>(ExifTag.BitsPerSample, true);

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
        public uint[] StripOffsets => this.GetArrayValue<uint>(ExifTag.StripOffsets);

        /// <summary>
        /// Gets the number of rows per strip.
        /// </summary>
        public uint RowsPerStrip => this.GetSingleUInt(ExifTag.RowsPerStrip);

        /// <summary>
        /// Gets for each strip, the number of bytes in the strip after compression.
        /// </summary>
        public uint[] StripByteCounts => this.GetArrayValue<uint>(ExifTag.StripByteCounts);

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
        public ushort[] ColorMap => this.GetArrayValue<ushort>(ExifTag.ColorMap, true);

        /// <summary>
        /// Gets the description of extra components.
        /// </summary>
        public ushort[] ExtraSamples => this.GetArrayValue<ushort>(ExifTag.ExtraSamples, true);

        /// <summary>
        /// Gets the copyright notice.
        /// </summary>
        public string Copyright => this.GetString(ExifTag.Copyright);

        internal T[] GetArrayValue<T>(ExifTag tag, bool optional = false)
            where T : struct
        {
            if (this.TryGetArrayValue(tag, out T[] result))
            {
                return result;
            }

            if (!optional)
            {
                throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
            }

            return null;
        }

        private bool TryGetArrayValue<T>(ExifTag tag, out T[] result)
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
        {
            if (!this.TryGetSingle(tag, out TTagValue value))
            {
                if (defaultValue != null)
                {
                    return defaultValue.Value;
                }

                throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
            }

            return (TEnum)(object)value;
        }

        /*
        private TEnum GetSingleEnum<TEnum, TEnumParent, TTagValue>(ExifTag tag, TEnum? defaultValue = null)
       where TEnum : struct
       where TEnumParent : struct
       where TTagValue : struct
        {
            if (!this.TryGetSingle(tag, out TTagValue value))
            {
                if (defaultValue != null)
                {
                    return defaultValue.Value;
                }

                throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
            }

            return (TEnum)(object)(TEnumParent)Convert.ChangeType(value, typeof(TEnumParent));
        } */

        private uint GetSingleUInt(ExifTag tag)
        {
            if (this.TryGetSingle(tag, out uint result))
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

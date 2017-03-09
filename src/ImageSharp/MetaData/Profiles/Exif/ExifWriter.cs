// <copyright file="ExifWriter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;

    /// <summary>
    /// Contains methods for writing EXIF metadata.
    /// </summary>
    internal sealed class ExifWriter
    {
        /// <summary>
        /// The start index.
        /// </summary>
        private const int StartIndex = 6;

        /// <summary>
        /// The collection if Image File Directory tags
        /// </summary>
        private static readonly ExifTag[] IfdTags =
        {
            ExifTag.SubfileType,
            ExifTag.OldSubfileType,
            ExifTag.ImageWidth,
            ExifTag.ImageLength,
            ExifTag.BitsPerSample,
            ExifTag.Compression,
            ExifTag.PhotometricInterpretation,
            ExifTag.Thresholding,
            ExifTag.CellWidth,
            ExifTag.CellLength,
            ExifTag.FillOrder,
            ExifTag.DocumentName,
            ExifTag.ImageDescription,
            ExifTag.Make,
            ExifTag.Model,
            ExifTag.StripOffsets,
            ExifTag.Orientation,
            ExifTag.SamplesPerPixel,
            ExifTag.RowsPerStrip,
            ExifTag.StripByteCounts,
            ExifTag.MinSampleValue,
            ExifTag.MaxSampleValue,
            ExifTag.XResolution,
            ExifTag.YResolution,
            ExifTag.PlanarConfiguration,
            ExifTag.PageName,
            ExifTag.XPosition,
            ExifTag.YPosition,
            ExifTag.FreeOffsets,
            ExifTag.FreeByteCounts,
            ExifTag.GrayResponseUnit,
            ExifTag.GrayResponseCurve,
            ExifTag.T4Options,
            ExifTag.T6Options,
            ExifTag.ResolutionUnit,
            ExifTag.PageNumber,
            ExifTag.ColorResponseUnit,
            ExifTag.TransferFunction,
            ExifTag.Software,
            ExifTag.DateTime,
            ExifTag.Artist,
            ExifTag.HostComputer,
            ExifTag.Predictor,
            ExifTag.WhitePoint,
            ExifTag.PrimaryChromaticities,
            ExifTag.ColorMap,
            ExifTag.HalftoneHints,
            ExifTag.TileWidth,
            ExifTag.TileLength,
            ExifTag.TileOffsets,
            ExifTag.TileByteCounts,
            ExifTag.BadFaxLines,
            ExifTag.CleanFaxData,
            ExifTag.ConsecutiveBadFaxLines,
            ExifTag.InkSet,
            ExifTag.InkNames,
            ExifTag.NumberOfInks,
            ExifTag.DotRange,
            ExifTag.TargetPrinter,
            ExifTag.ExtraSamples,
            ExifTag.SampleFormat,
            ExifTag.SMinSampleValue,
            ExifTag.SMaxSampleValue,
            ExifTag.TransferRange,
            ExifTag.ClipPath,
            ExifTag.XClipPathUnits,
            ExifTag.YClipPathUnits,
            ExifTag.Indexed,
            ExifTag.JPEGTables,
            ExifTag.OPIProxy,
            ExifTag.ProfileType,
            ExifTag.FaxProfile,
            ExifTag.CodingMethods,
            ExifTag.VersionYear,
            ExifTag.ModeNumber,
            ExifTag.Decode,
            ExifTag.DefaultImageColor,
            ExifTag.T82ptions,
            ExifTag.JPEGProc,
            ExifTag.JPEGInterchangeFormat,
            ExifTag.JPEGInterchangeFormatLength,
            ExifTag.JPEGRestartInterval,
            ExifTag.JPEGLosslessPredictors,
            ExifTag.JPEGPointTransforms,
            ExifTag.JPEGQTables,
            ExifTag.JPEGDCTables,
            ExifTag.JPEGACTables,
            ExifTag.YCbCrCoefficients,
            ExifTag.YCbCrSubsampling,
            ExifTag.YCbCrSubsampling,
            ExifTag.YCbCrPositioning,
            ExifTag.ReferenceBlackWhite,
            ExifTag.StripRowCounts,
            ExifTag.XMP,
            ExifTag.Rating,
            ExifTag.RatingPercent,
            ExifTag.ImageID,
            ExifTag.CFARepeatPatternDim,
            ExifTag.CFAPattern2,
            ExifTag.BatteryLevel,
            ExifTag.Copyright,
            ExifTag.MDFileTag,
            ExifTag.MDScalePixel,
            ExifTag.MDLabName,
            ExifTag.MDSampleInfo,
            ExifTag.MDPrepDate,
            ExifTag.MDPrepTime,
            ExifTag.MDFileUnits,
            ExifTag.PixelScale,
            ExifTag.IntergraphPacketData,
            ExifTag.IntergraphRegisters,
            ExifTag.IntergraphMatrix,
            ExifTag.ModelTiePoint,
            ExifTag.SEMInfo,
            ExifTag.ModelTransform,
            ExifTag.ImageLayer,
            ExifTag.FaxRecvParams,
            ExifTag.FaxSubaddress,
            ExifTag.FaxRecvTime,
            ExifTag.ImageSourceData,
            ExifTag.XPTitle,
            ExifTag.XPComment,
            ExifTag.XPAuthor,
            ExifTag.XPKeywords,
            ExifTag.XPSubject,
            ExifTag.GDALMetadata,
            ExifTag.GDALNoData
        };

        /// <summary>
        /// The collection of Exif tags
        /// </summary>
        private static readonly ExifTag[] ExifTags =
        {
            ExifTag.ExposureTime,
            ExifTag.FNumber,
            ExifTag.ExposureProgram,
            ExifTag.SpectralSensitivity,
            ExifTag.ISOSpeedRatings,
            ExifTag.OECF,
            ExifTag.Interlace,
            ExifTag.TimeZoneOffset,
            ExifTag.SelfTimerMode,
            ExifTag.SensitivityType,
            ExifTag.StandardOutputSensitivity,
            ExifTag.RecommendedExposureIndex,
            ExifTag.ISOSpeed,
            ExifTag.ISOSpeedLatitudeyyy,
            ExifTag.ISOSpeedLatitudezzz,
            ExifTag.ExifVersion,
            ExifTag.DateTimeOriginal,
            ExifTag.DateTimeDigitized,
            ExifTag.OffsetTime,
            ExifTag.OffsetTimeOriginal,
            ExifTag.OffsetTimeDigitized,
            ExifTag.ComponentsConfiguration,
            ExifTag.CompressedBitsPerPixel,
            ExifTag.ShutterSpeedValue,
            ExifTag.ApertureValue,
            ExifTag.BrightnessValue,
            ExifTag.ExposureBiasValue,
            ExifTag.MaxApertureValue,
            ExifTag.SubjectDistance,
            ExifTag.MeteringMode,
            ExifTag.LightSource,
            ExifTag.Flash,
            ExifTag.FocalLength,
            ExifTag.FlashEnergy2,
            ExifTag.SpatialFrequencyResponse2,
            ExifTag.Noise,
            ExifTag.FocalPlaneXResolution2,
            ExifTag.FocalPlaneYResolution2,
            ExifTag.FocalPlaneResolutionUnit2,
            ExifTag.ImageNumber,
            ExifTag.SecurityClassification,
            ExifTag.ImageHistory,
            ExifTag.SubjectArea,
            ExifTag.ExposureIndex2,
            ExifTag.TIFFEPStandardID,
            ExifTag.SensingMethod2,
            ExifTag.MakerNote,
            ExifTag.UserComment,
            ExifTag.SubsecTime,
            ExifTag.SubsecTimeOriginal,
            ExifTag.SubsecTimeDigitized,
            ExifTag.AmbientTemperature,
            ExifTag.Humidity,
            ExifTag.Pressure,
            ExifTag.WaterDepth,
            ExifTag.Acceleration,
            ExifTag.CameraElevationAngle,
            ExifTag.FlashpixVersion,
            ExifTag.ColorSpace,
            ExifTag.PixelXDimension,
            ExifTag.PixelYDimension,
            ExifTag.RelatedSoundFile,
            ExifTag.FlashEnergy,
            ExifTag.SpatialFrequencyResponse,
            ExifTag.FocalPlaneXResolution,
            ExifTag.FocalPlaneYResolution,
            ExifTag.FocalPlaneResolutionUnit,
            ExifTag.SubjectLocation,
            ExifTag.ExposureIndex,
            ExifTag.SensingMethod,
            ExifTag.FileSource,
            ExifTag.SceneType,
            ExifTag.CFAPattern,
            ExifTag.CustomRendered,
            ExifTag.ExposureMode,
            ExifTag.WhiteBalance,
            ExifTag.DigitalZoomRatio,
            ExifTag.FocalLengthIn35mmFilm,
            ExifTag.SceneCaptureType,
            ExifTag.GainControl,
            ExifTag.Contrast,
            ExifTag.Saturation,
            ExifTag.Sharpness,
            ExifTag.DeviceSettingDescription,
            ExifTag.SubjectDistanceRange,
            ExifTag.ImageUniqueID,
            ExifTag.OwnerName,
            ExifTag.SerialNumber,
            ExifTag.LensInfo,
            ExifTag.LensMake,
            ExifTag.LensModel,
            ExifTag.LensSerialNumber
          };

        /// <summary>
        /// The collection of GPS tags
        /// </summary>
        private static readonly ExifTag[] GPSTags =
        {
            ExifTag.GPSVersionID,
            ExifTag.GPSLatitudeRef,
            ExifTag.GPSLatitude,
            ExifTag.GPSLongitudeRef,
            ExifTag.GPSLongitude,
            ExifTag.GPSAltitudeRef,
            ExifTag.GPSAltitude,
            ExifTag.GPSTimestamp,
            ExifTag.GPSSatellites,
            ExifTag.GPSStatus,
            ExifTag.GPSMeasureMode,
            ExifTag.GPSDOP,
            ExifTag.GPSSpeedRef,
            ExifTag.GPSSpeed,
            ExifTag.GPSTrackRef,
            ExifTag.GPSTrack,
            ExifTag.GPSImgDirectionRef,
            ExifTag.GPSImgDirection,
            ExifTag.GPSMapDatum,
            ExifTag.GPSDestLatitudeRef,
            ExifTag.GPSDestLatitude,
            ExifTag.GPSDestLongitudeRef,
            ExifTag.GPSDestLongitude,
            ExifTag.GPSDestBearingRef,
            ExifTag.GPSDestBearing,
            ExifTag.GPSDestDistanceRef,
            ExifTag.GPSDestDistance,
            ExifTag.GPSProcessingMethod,
            ExifTag.GPSAreaInformation,
            ExifTag.GPSDateStamp,
            ExifTag.GPSDifferential
        };

        /// <summary>
        /// Which parts will be written.
        /// </summary>
        private ExifParts allowedParts;
        private Collection<ExifValue> values;
        private Collection<int> dataOffsets;
        private Collection<int> ifdIndexes;
        private Collection<int> exifIndexes;
        private Collection<int> gpsIndexes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifWriter"/> class.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="allowedParts">The allowed parts.</param>
        public ExifWriter(Collection<ExifValue> values, ExifParts allowedParts)
        {
            this.values = values;
            this.allowedParts = allowedParts;
            this.ifdIndexes = this.GetIndexes(ExifParts.IfdTags, IfdTags);
            this.exifIndexes = this.GetIndexes(ExifParts.ExifTags, ExifTags);
            this.gpsIndexes = this.GetIndexes(ExifParts.GPSTags, GPSTags);
        }

        /// <summary>
        /// Returns the EXIF data.
        /// </summary>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        public byte[] GetData()
        {
            uint length;
            int exifIndex = -1;
            int gpsIndex = -1;

            if (this.exifIndexes.Count > 0)
            {
                exifIndex = (int)this.GetIndex(this.ifdIndexes, ExifTag.SubIFDOffset);
            }

            if (this.gpsIndexes.Count > 0)
            {
                gpsIndex = (int)this.GetIndex(this.ifdIndexes, ExifTag.GPSIFDOffset);
            }

            uint ifdLength = 2 + this.GetLength(this.ifdIndexes) + 4;
            uint exifLength = this.GetLength(this.exifIndexes);
            uint gpsLength = this.GetLength(this.gpsIndexes);

            if (exifLength > 0)
            {
                exifLength += 2;
            }

            if (gpsLength > 0)
            {
                gpsLength += 2;
            }

            length = ifdLength + exifLength + gpsLength;

            if (length == 6)
            {
                return null;
            }

            length += 10 + 4 + 2;

            byte[] result = new byte[length];
            result[0] = (byte)'E';
            result[1] = (byte)'x';
            result[2] = (byte)'i';
            result[3] = (byte)'f';
            result[4] = 0x00;
            result[5] = 0x00;
            result[6] = (byte)'I';
            result[7] = (byte)'I';
            result[8] = 0x2A;
            result[9] = 0x00;

            int i = 10;
            uint ifdOffset = ((uint)i - StartIndex) + 4;
            uint thumbnailOffset = ifdOffset + ifdLength + exifLength + gpsLength;

            if (exifLength > 0)
            {
                this.values[exifIndex].Value = ifdOffset + ifdLength;
            }

            if (gpsLength > 0)
            {
                this.values[gpsIndex].Value = ifdOffset + ifdLength + exifLength;
            }

            i = Write(BitConverter.GetBytes(ifdOffset), result, i);
            i = this.WriteHeaders(this.ifdIndexes, result, i);
            i = Write(BitConverter.GetBytes(thumbnailOffset), result, i);
            i = this.WriteData(this.ifdIndexes, result, i);

            if (exifLength > 0)
            {
                i = this.WriteHeaders(this.exifIndexes, result, i);
                i = this.WriteData(this.exifIndexes, result, i);
            }

            if (gpsLength > 0)
            {
                i = this.WriteHeaders(this.gpsIndexes, result, i);
                i = this.WriteData(this.gpsIndexes, result, i);
            }

            Write(BitConverter.GetBytes((ushort)0), result, i);

            return result;
        }

        private static int Write(byte[] source, byte[] destination, int offset)
        {
            Buffer.BlockCopy(source, 0, destination, offset, source.Length);

            return offset + source.Length;
        }

        private int GetIndex(Collection<int> indexes, ExifTag tag)
        {
            foreach (int index in indexes)
            {
                if (this.values[index].Tag == tag)
                {
                    return index;
                }
            }

            int newIndex = this.values.Count;
            indexes.Add(newIndex);
            this.values.Add(ExifValue.Create(tag, null));
            return newIndex;
        }

        private Collection<int> GetIndexes(ExifParts part, ExifTag[] tags)
        {
            if (((int)this.allowedParts & (int)part) == 0)
            {
                return new Collection<int>();
            }

            Collection<int> result = new Collection<int>();
            for (int i = 0; i < this.values.Count; i++)
            {
                ExifValue value = this.values[i];

                if (!value.HasValue)
                {
                    continue;
                }

                int index = Array.IndexOf(tags, value.Tag);
                if (index > -1)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private uint GetLength(IEnumerable<int> indexes)
        {
            uint length = 0;

            foreach (int index in indexes)
            {
                uint valueLength = (uint)this.values[index].Length;

                if (valueLength > 4)
                {
                    length += 12 + valueLength;
                }
                else
                {
                    length += 12;
                }
            }

            return length;
        }

        private int WriteArray(ExifValue value, byte[] destination, int offset)
        {
            if (value.DataType == ExifDataType.Ascii)
            {
                return this.WriteValue(ExifDataType.Ascii, value.Value, destination, offset);
            }

            int newOffset = offset;
            foreach (object obj in (Array)value.Value)
            {
                newOffset = this.WriteValue(value.DataType, obj, destination, newOffset);
            }

            return newOffset;
        }

        private int WriteData(Collection<int> indexes, byte[] destination, int offset)
        {
            if (this.dataOffsets.Count == 0)
            {
                return offset;
            }

            int newOffset = offset;

            int i = 0;
            foreach (int index in indexes)
            {
                ExifValue value = this.values[index];
                if (value.Length > 4)
                {
                    Write(BitConverter.GetBytes(newOffset - StartIndex), destination, this.dataOffsets[i++]);
                    newOffset = this.WriteValue(value, destination, newOffset);
                }
            }

            return newOffset;
        }

        private int WriteHeaders(Collection<int> indexes, byte[] destination, int offset)
        {
            this.dataOffsets = new Collection<int>();

            int newOffset = Write(BitConverter.GetBytes((ushort)indexes.Count), destination, offset);

            if (indexes.Count == 0)
            {
                return newOffset;
            }

            foreach (int index in indexes)
            {
                ExifValue value = this.values[index];
                newOffset = Write(BitConverter.GetBytes((ushort)value.Tag), destination, newOffset);
                newOffset = Write(BitConverter.GetBytes((ushort)value.DataType), destination, newOffset);
                newOffset = Write(BitConverter.GetBytes((uint)value.NumberOfComponents), destination, newOffset);

                if (value.Length > 4)
                {
                    this.dataOffsets.Add(newOffset);
                }
                else
                {
                    this.WriteValue(value, destination, newOffset);
                }

                newOffset += 4;
            }

            return newOffset;
        }

        private int WriteRational(Rational value, byte[] destination, int offset)
        {
            Write(BitConverter.GetBytes(value.Numerator), destination, offset);
            Write(BitConverter.GetBytes(value.Denominator), destination, offset + 4);

            return offset + 8;
        }

        private int WriteSignedRational(SignedRational value, byte[] destination, int offset)
        {
            Write(BitConverter.GetBytes(value.Numerator), destination, offset);
            Write(BitConverter.GetBytes(value.Denominator), destination, offset + 4);

            return offset + 8;
        }

        private int WriteValue(ExifDataType dataType, object value, byte[] destination, int offset)
        {
            switch (dataType)
            {
                case ExifDataType.Ascii:
                    return Write(Encoding.UTF8.GetBytes((string)value), destination, offset);
                case ExifDataType.Byte:
                case ExifDataType.Undefined:
                    destination[offset] = (byte)value;
                    return offset + 1;
                case ExifDataType.DoubleFloat:
                    return Write(BitConverter.GetBytes((double)value), destination, offset);
                case ExifDataType.Short:
                    return Write(BitConverter.GetBytes((ushort)value), destination, offset);
                case ExifDataType.Long:
                    return Write(BitConverter.GetBytes((uint)value), destination, offset);
                case ExifDataType.Rational:
                    return this.WriteRational((Rational)value, destination, offset);
                case ExifDataType.SignedByte:
                    destination[offset] = unchecked((byte)((sbyte)value));
                    return offset + 1;
                case ExifDataType.SignedLong:
                    return Write(BitConverter.GetBytes((int)value), destination, offset);
                case ExifDataType.SignedShort:
                    return Write(BitConverter.GetBytes((short)value), destination, offset);
                case ExifDataType.SignedRational:
                    return this.WriteSignedRational((SignedRational)value, destination, offset);
                case ExifDataType.SingleFloat:
                    return Write(BitConverter.GetBytes((float)value), destination, offset);
                default:
                    throw new NotImplementedException();
            }
        }

        private int WriteValue(ExifValue value, byte[] destination, int offset)
        {
            if (value.IsArray && value.DataType != ExifDataType.Ascii)
            {
                return this.WriteArray(value, destination, offset);
            }

            return this.WriteValue(value.DataType, value.Value, destination, offset);
        }
    }
}
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal static partial class ExifValues
    {
        public static ExifValue Create(ExifTagValue tag) => (ExifValue)CreateValue(tag);

        public static ExifValue Create(ExifTag tag) => (ExifValue)CreateValue((ExifTagValue)(ushort)tag);

        public static ExifValue Create(ExifTagValue tag, ExifDataType dataType, uint numberOfComponents)
        {
            bool isArray = numberOfComponents != 1;

            switch (dataType)
            {
                case ExifDataType.Byte: return isArray ? (ExifValue)new ExifByteArray(tag, dataType) : new ExifByte(tag, dataType);
                case ExifDataType.DoubleFloat: return isArray ? (ExifValue)new ExifDoubleArray(tag) : new ExifDouble(tag);
                case ExifDataType.SingleFloat: return isArray ? (ExifValue)new ExifFloatArray(tag) : new ExifFloat(tag);
                case ExifDataType.Long: return isArray ? (ExifValue)new ExifLongArray(tag) : new ExifLong(tag);
                case ExifDataType.Rational: return isArray ? (ExifValue)new ExifRationalArray(tag) : new ExifRational(tag);
                case ExifDataType.Short: return isArray ? (ExifValue)new ExifShortArray(tag) : new ExifShort(tag);
                case ExifDataType.SignedByte: return isArray ? (ExifValue)new ExifSignedByteArray(tag) : new ExifSignedByte(tag);
                case ExifDataType.SignedLong: return isArray ? (ExifValue)new ExifSignedLongArray(tag) : new ExifSignedLong(tag);
                case ExifDataType.SignedRational: return isArray ? (ExifValue)new ExifSignedRationalArray(tag) : new ExifSignedRational(tag);
                case ExifDataType.SignedShort: return isArray ? (ExifValue)new ExifSignedShortArray(tag) : new ExifSignedShort(tag);
                case ExifDataType.Ascii: return new ExifString(tag);
                case ExifDataType.Undefined: return isArray ? (ExifValue)new ExifByteArray(tag, dataType) : new ExifByte(tag, dataType);
                default: return null;
            }
        }

        private static object CreateValue(ExifTagValue tag)
        {
            switch (tag)
            {
                case ExifTagValue.FaxProfile: return new ExifByte(ExifTag.FaxProfile, ExifDataType.Byte);
                case ExifTagValue.ModeNumber: return new ExifByte(ExifTag.ModeNumber, ExifDataType.Byte);
                case ExifTagValue.GPSAltitudeRef: return new ExifByte(ExifTag.GPSAltitudeRef, ExifDataType.Byte);

                case ExifTagValue.ClipPath: return new ExifByteArray(ExifTag.ClipPath, ExifDataType.Byte);
                case ExifTagValue.VersionYear: return new ExifByteArray(ExifTag.VersionYear, ExifDataType.Byte);
                case ExifTagValue.XMP: return new ExifByteArray(ExifTag.XMP, ExifDataType.Byte);
                case ExifTagValue.CFAPattern2: return new ExifByteArray(ExifTag.CFAPattern2, ExifDataType.Byte);
                case ExifTagValue.TIFFEPStandardID: return new ExifByteArray(ExifTag.TIFFEPStandardID, ExifDataType.Byte);
                case ExifTagValue.XPTitle: return new ExifByteArray(ExifTag.XPTitle, ExifDataType.Byte);
                case ExifTagValue.XPComment: return new ExifByteArray(ExifTag.XPComment, ExifDataType.Byte);
                case ExifTagValue.XPAuthor: return new ExifByteArray(ExifTag.XPAuthor, ExifDataType.Byte);
                case ExifTagValue.XPKeywords: return new ExifByteArray(ExifTag.XPKeywords, ExifDataType.Byte);
                case ExifTagValue.XPSubject: return new ExifByteArray(ExifTag.XPSubject, ExifDataType.Byte);
                case ExifTagValue.GPSVersionID: return new ExifByteArray(ExifTag.GPSVersionID, ExifDataType.Byte);

                case ExifTagValue.PixelScale: return new ExifDoubleArray(ExifTag.PixelScale);
                case ExifTagValue.IntergraphMatrix: return new ExifDoubleArray(ExifTag.IntergraphMatrix);
                case ExifTagValue.ModelTiePoint: return new ExifDoubleArray(ExifTag.ModelTiePoint);
                case ExifTagValue.ModelTransform: return new ExifDoubleArray(ExifTag.ModelTransform);

                case ExifTagValue.SubfileType: return new ExifLong(ExifTag.SubfileType);
                case ExifTagValue.SubIFDOffset: return new ExifLong(ExifTag.SubIFDOffset);
                case ExifTagValue.GPSIFDOffset: return new ExifLong(ExifTag.GPSIFDOffset);
                case ExifTagValue.T4Options: return new ExifLong(ExifTag.T4Options);
                case ExifTagValue.T6Options: return new ExifLong(ExifTag.T6Options);
                case ExifTagValue.XClipPathUnits: return new ExifLong(ExifTag.XClipPathUnits);
                case ExifTagValue.YClipPathUnits: return new ExifLong(ExifTag.YClipPathUnits);
                case ExifTagValue.ProfileType: return new ExifLong(ExifTag.ProfileType);
                case ExifTagValue.CodingMethods: return new ExifLong(ExifTag.CodingMethods);
                case ExifTagValue.T82ptions: return new ExifLong(ExifTag.T82ptions);
                case ExifTagValue.JPEGInterchangeFormat: return new ExifLong(ExifTag.JPEGInterchangeFormat);
                case ExifTagValue.JPEGInterchangeFormatLength: return new ExifLong(ExifTag.JPEGInterchangeFormatLength);
                case ExifTagValue.MDFileTag: return new ExifLong(ExifTag.MDFileTag);
                case ExifTagValue.StandardOutputSensitivity: return new ExifLong(ExifTag.StandardOutputSensitivity);
                case ExifTagValue.RecommendedExposureIndex: return new ExifLong(ExifTag.RecommendedExposureIndex);
                case ExifTagValue.ISOSpeed: return new ExifLong(ExifTag.ISOSpeed);
                case ExifTagValue.ISOSpeedLatitudeyyy: return new ExifLong(ExifTag.ISOSpeedLatitudeyyy);
                case ExifTagValue.ISOSpeedLatitudezzz: return new ExifLong(ExifTag.ISOSpeedLatitudezzz);
                case ExifTagValue.FaxRecvParams: return new ExifLong(ExifTag.FaxRecvParams);
                case ExifTagValue.FaxRecvTime: return new ExifLong(ExifTag.FaxRecvTime);
                case ExifTagValue.ImageNumber: return new ExifLong(ExifTag.ImageNumber);

                case ExifTagValue.FreeOffsets: return new ExifLongArray(ExifTag.FreeOffsets);
                case ExifTagValue.FreeByteCounts: return new ExifLongArray(ExifTag.FreeByteCounts);
                case ExifTagValue.ColorResponseUnit: return new ExifLongArray(ExifTag.ColorResponseUnit);
                case ExifTagValue.TileOffsets: return new ExifLongArray(ExifTag.TileOffsets);
                case ExifTagValue.SMinSampleValue: return new ExifLongArray(ExifTag.SMinSampleValue);
                case ExifTagValue.SMaxSampleValue: return new ExifLongArray(ExifTag.SMaxSampleValue);
                case ExifTagValue.JPEGQTables: return new ExifLongArray(ExifTag.JPEGQTables);
                case ExifTagValue.JPEGDCTables: return new ExifLongArray(ExifTag.JPEGDCTables);
                case ExifTagValue.JPEGACTables: return new ExifLongArray(ExifTag.JPEGACTables);
                case ExifTagValue.StripRowCounts: return new ExifLongArray(ExifTag.StripRowCounts);
                case ExifTagValue.IntergraphRegisters: return new ExifLongArray(ExifTag.IntergraphRegisters);
                case ExifTagValue.TimeZoneOffset: return new ExifLongArray(ExifTag.TimeZoneOffset);

                case ExifTagValue.ImageWidth: return new ExifNumber(ExifTag.ImageWidth);
                case ExifTagValue.ImageLength: return new ExifNumber(ExifTag.ImageLength);
                case ExifTagValue.TileWidth: return new ExifNumber(ExifTag.TileWidth);
                case ExifTagValue.TileLength: return new ExifNumber(ExifTag.TileLength);
                case ExifTagValue.BadFaxLines: return new ExifNumber(ExifTag.BadFaxLines);
                case ExifTagValue.ConsecutiveBadFaxLines: return new ExifNumber(ExifTag.ConsecutiveBadFaxLines);
                case ExifTagValue.PixelXDimension: return new ExifNumber(ExifTag.PixelXDimension);
                case ExifTagValue.PixelYDimension: return new ExifNumber(ExifTag.PixelYDimension);

                case ExifTagValue.StripOffsets: return new ExifNumberArray(ExifTag.StripOffsets);
                case ExifTagValue.TileByteCounts: return new ExifNumberArray(ExifTag.TileByteCounts);
                case ExifTagValue.ImageLayer: return new ExifNumberArray(ExifTag.ImageLayer);

                case ExifTagValue.XPosition: return new ExifRational(ExifTag.XPosition);
                case ExifTagValue.YPosition: return new ExifRational(ExifTag.YPosition);
                case ExifTagValue.XResolution: return new ExifRational(ExifTag.XResolution);
                case ExifTagValue.YResolution: return new ExifRational(ExifTag.YResolution);
                case ExifTagValue.BatteryLevel: return new ExifRational(ExifTag.BatteryLevel);
                case ExifTagValue.ExposureTime: return new ExifRational(ExifTag.ExposureTime);
                case ExifTagValue.FNumber: return new ExifRational(ExifTag.FNumber);
                case ExifTagValue.MDScalePixel: return new ExifRational(ExifTag.MDScalePixel);
                case ExifTagValue.CompressedBitsPerPixel: return new ExifRational(ExifTag.CompressedBitsPerPixel);
                case ExifTagValue.ApertureValue: return new ExifRational(ExifTag.ApertureValue);
                case ExifTagValue.MaxApertureValue: return new ExifRational(ExifTag.MaxApertureValue);
                case ExifTagValue.SubjectDistance: return new ExifRational(ExifTag.SubjectDistance);
                case ExifTagValue.FocalLength: return new ExifRational(ExifTag.FocalLength);
                case ExifTagValue.FlashEnergy2: return new ExifRational(ExifTag.FlashEnergy2);
                case ExifTagValue.FocalPlaneXResolution2: return new ExifRational(ExifTag.FocalPlaneXResolution2);
                case ExifTagValue.FocalPlaneYResolution2: return new ExifRational(ExifTag.FocalPlaneYResolution2);
                case ExifTagValue.ExposureIndex2: return new ExifRational(ExifTag.ExposureIndex2);
                case ExifTagValue.Humidity: return new ExifRational(ExifTag.Humidity);
                case ExifTagValue.Pressure: return new ExifRational(ExifTag.Pressure);
                case ExifTagValue.Acceleration: return new ExifRational(ExifTag.Acceleration);
                case ExifTagValue.FlashEnergy: return new ExifRational(ExifTag.FlashEnergy);
                case ExifTagValue.FocalPlaneXResolution: return new ExifRational(ExifTag.FocalPlaneXResolution);
                case ExifTagValue.FocalPlaneYResolution: return new ExifRational(ExifTag.FocalPlaneYResolution);
                case ExifTagValue.ExposureIndex: return new ExifRational(ExifTag.ExposureIndex);
                case ExifTagValue.DigitalZoomRatio: return new ExifRational(ExifTag.DigitalZoomRatio);
                case ExifTagValue.GPSAltitude: return new ExifRational(ExifTag.GPSAltitude);
                case ExifTagValue.GPSDOP: return new ExifRational(ExifTag.GPSDOP);
                case ExifTagValue.GPSSpeed: return new ExifRational(ExifTag.GPSSpeed);
                case ExifTagValue.GPSTrack: return new ExifRational(ExifTag.GPSTrack);
                case ExifTagValue.GPSImgDirection: return new ExifRational(ExifTag.GPSImgDirection);
                case ExifTagValue.GPSDestBearing: return new ExifRational(ExifTag.GPSDestBearing);
                case ExifTagValue.GPSDestDistance: return new ExifRational(ExifTag.GPSDestDistance);

                case ExifTagValue.WhitePoint: return new ExifRationalArray(ExifTag.WhitePoint);
                case ExifTagValue.PrimaryChromaticities: return new ExifRationalArray(ExifTag.PrimaryChromaticities);
                case ExifTagValue.YCbCrCoefficients: return new ExifRationalArray(ExifTag.YCbCrCoefficients);
                case ExifTagValue.ReferenceBlackWhite: return new ExifRationalArray(ExifTag.ReferenceBlackWhite);
                case ExifTagValue.GPSLatitude: return new ExifRationalArray(ExifTag.GPSLatitude);
                case ExifTagValue.GPSLongitude: return new ExifRationalArray(ExifTag.GPSLongitude);
                case ExifTagValue.GPSTimestamp: return new ExifRationalArray(ExifTag.GPSTimestamp);
                case ExifTagValue.GPSDestLatitude: return new ExifRationalArray(ExifTag.GPSDestLatitude);
                case ExifTagValue.GPSDestLongitude: return new ExifRationalArray(ExifTag.GPSDestLongitude);
                case ExifTagValue.LensSpecification: return new ExifRationalArray(ExifTag.LensSpecification);

                case ExifTagValue.OldSubfileType: return new ExifShort(ExifTag.OldSubfileType);
                case ExifTagValue.Compression: return new ExifShort(ExifTag.Compression);
                case ExifTagValue.PhotometricInterpretation: return new ExifShort(ExifTag.PhotometricInterpretation);
                case ExifTagValue.Thresholding: return new ExifShort(ExifTag.Thresholding);
                case ExifTagValue.CellWidth: return new ExifShort(ExifTag.CellWidth);
                case ExifTagValue.CellLength: return new ExifShort(ExifTag.CellLength);
                case ExifTagValue.FillOrder: return new ExifShort(ExifTag.FillOrder);
                case ExifTagValue.Orientation: return new ExifShort(ExifTag.Orientation);
                case ExifTagValue.SamplesPerPixel: return new ExifShort(ExifTag.SamplesPerPixel);
                case ExifTagValue.PlanarConfiguration: return new ExifShort(ExifTag.PlanarConfiguration);
                case ExifTagValue.GrayResponseUnit: return new ExifShort(ExifTag.GrayResponseUnit);
                case ExifTagValue.ResolutionUnit: return new ExifShort(ExifTag.ResolutionUnit);
                case ExifTagValue.CleanFaxData: return new ExifShort(ExifTag.CleanFaxData);
                case ExifTagValue.InkSet: return new ExifShort(ExifTag.InkSet);
                case ExifTagValue.NumberOfInks: return new ExifShort(ExifTag.NumberOfInks);
                case ExifTagValue.DotRange: return new ExifShort(ExifTag.DotRange);
                case ExifTagValue.Indexed: return new ExifShort(ExifTag.Indexed);
                case ExifTagValue.OPIProxy: return new ExifShort(ExifTag.OPIProxy);
                case ExifTagValue.JPEGProc: return new ExifShort(ExifTag.JPEGProc);
                case ExifTagValue.JPEGRestartInterval: return new ExifShort(ExifTag.JPEGRestartInterval);
                case ExifTagValue.YCbCrPositioning: return new ExifShort(ExifTag.YCbCrPositioning);
                case ExifTagValue.Rating: return new ExifShort(ExifTag.Rating);
                case ExifTagValue.RatingPercent: return new ExifShort(ExifTag.RatingPercent);
                case ExifTagValue.ExposureProgram: return new ExifShort(ExifTag.ExposureProgram);
                case ExifTagValue.Interlace: return new ExifShort(ExifTag.Interlace);
                case ExifTagValue.SelfTimerMode: return new ExifShort(ExifTag.SelfTimerMode);
                case ExifTagValue.SensitivityType: return new ExifShort(ExifTag.SensitivityType);
                case ExifTagValue.MeteringMode: return new ExifShort(ExifTag.MeteringMode);
                case ExifTagValue.LightSource: return new ExifShort(ExifTag.LightSource);
                case ExifTagValue.FocalPlaneResolutionUnit2: return new ExifShort(ExifTag.FocalPlaneResolutionUnit2);
                case ExifTagValue.SensingMethod2: return new ExifShort(ExifTag.SensingMethod2);
                case ExifTagValue.Flash: return new ExifShort(ExifTag.Flash);
                case ExifTagValue.ColorSpace: return new ExifShort(ExifTag.ColorSpace);
                case ExifTagValue.FocalPlaneResolutionUnit: return new ExifShort(ExifTag.FocalPlaneResolutionUnit);
                case ExifTagValue.SensingMethod: return new ExifShort(ExifTag.SensingMethod);
                case ExifTagValue.CustomRendered: return new ExifShort(ExifTag.CustomRendered);
                case ExifTagValue.ExposureMode: return new ExifShort(ExifTag.ExposureMode);
                case ExifTagValue.WhiteBalance: return new ExifShort(ExifTag.WhiteBalance);
                case ExifTagValue.FocalLengthIn35mmFilm: return new ExifShort(ExifTag.FocalLengthIn35mmFilm);
                case ExifTagValue.SceneCaptureType: return new ExifShort(ExifTag.SceneCaptureType);
                case ExifTagValue.GainControl: return new ExifShort(ExifTag.GainControl);
                case ExifTagValue.Contrast: return new ExifShort(ExifTag.Contrast);
                case ExifTagValue.Saturation: return new ExifShort(ExifTag.Saturation);
                case ExifTagValue.Sharpness: return new ExifShort(ExifTag.Sharpness);
                case ExifTagValue.SubjectDistanceRange: return new ExifShort(ExifTag.SubjectDistanceRange);
                case ExifTagValue.GPSDifferential: return new ExifShort(ExifTag.GPSDifferential);

                case ExifTagValue.BitsPerSample: return new ExifShortArray(ExifTag.BitsPerSample);
                case ExifTagValue.MinSampleValue: return new ExifShortArray(ExifTag.MinSampleValue);
                case ExifTagValue.MaxSampleValue: return new ExifShortArray(ExifTag.MaxSampleValue);
                case ExifTagValue.GrayResponseCurve: return new ExifShortArray(ExifTag.GrayResponseCurve);
                case ExifTagValue.ColorMap: return new ExifShortArray(ExifTag.ColorMap);
                case ExifTagValue.ExtraSamples: return new ExifShortArray(ExifTag.ExtraSamples);
                case ExifTagValue.PageNumber: return new ExifShortArray(ExifTag.PageNumber);
                case ExifTagValue.TransferFunction: return new ExifShortArray(ExifTag.TransferFunction);
                case ExifTagValue.Predictor: return new ExifShortArray(ExifTag.Predictor);
                case ExifTagValue.HalftoneHints: return new ExifShortArray(ExifTag.HalftoneHints);
                case ExifTagValue.SampleFormat: return new ExifShortArray(ExifTag.SampleFormat);
                case ExifTagValue.TransferRange: return new ExifShortArray(ExifTag.TransferRange);
                case ExifTagValue.DefaultImageColor: return new ExifShortArray(ExifTag.DefaultImageColor);
                case ExifTagValue.JPEGLosslessPredictors: return new ExifShortArray(ExifTag.JPEGLosslessPredictors);
                case ExifTagValue.JPEGPointTransforms: return new ExifShortArray(ExifTag.JPEGPointTransforms);
                case ExifTagValue.YCbCrSubsampling: return new ExifShortArray(ExifTag.YCbCrSubsampling);
                case ExifTagValue.CFARepeatPatternDim: return new ExifShortArray(ExifTag.CFARepeatPatternDim);
                case ExifTagValue.IntergraphPacketData: return new ExifShortArray(ExifTag.IntergraphPacketData);
                case ExifTagValue.ISOSpeedRatings: return new ExifShortArray(ExifTag.ISOSpeedRatings);
                case ExifTagValue.SubjectArea: return new ExifShortArray(ExifTag.SubjectArea);
                case ExifTagValue.SubjectLocation: return new ExifShortArray(ExifTag.SubjectLocation);

                case ExifTagValue.ShutterSpeedValue: return new ExifSignedRational(ExifTag.ShutterSpeedValue);
                case ExifTagValue.BrightnessValue: return new ExifSignedRational(ExifTag.BrightnessValue);
                case ExifTagValue.ExposureBiasValue: return new ExifSignedRational(ExifTag.ExposureBiasValue);
                case ExifTagValue.AmbientTemperature: return new ExifSignedRational(ExifTag.AmbientTemperature);
                case ExifTagValue.WaterDepth: return new ExifSignedRational(ExifTag.WaterDepth);
                case ExifTagValue.CameraElevationAngle: return new ExifSignedRational(ExifTag.CameraElevationAngle);

                case ExifTagValue.Decode: return new ExifSignedRationalArray(ExifTag.Decode);

                case ExifTagValue.ImageDescription: return new ExifString(ExifTag.ImageDescription);
                case ExifTagValue.Make: return new ExifString(ExifTag.Make);
                case ExifTagValue.Model: return new ExifString(ExifTag.Model);
                case ExifTagValue.Software: return new ExifString(ExifTag.Software);
                case ExifTagValue.DateTime: return new ExifString(ExifTag.DateTime);
                case ExifTagValue.Artist: return new ExifString(ExifTag.Artist);
                case ExifTagValue.HostComputer: return new ExifString(ExifTag.HostComputer);
                case ExifTagValue.Copyright: return new ExifString(ExifTag.Copyright);
                case ExifTagValue.DocumentName: return new ExifString(ExifTag.DocumentName);
                case ExifTagValue.PageName: return new ExifString(ExifTag.PageName);
                case ExifTagValue.InkNames: return new ExifString(ExifTag.InkNames);
                case ExifTagValue.TargetPrinter: return new ExifString(ExifTag.TargetPrinter);
                case ExifTagValue.ImageID: return new ExifString(ExifTag.ImageID);
                case ExifTagValue.MDLabName: return new ExifString(ExifTag.MDLabName);
                case ExifTagValue.MDSampleInfo: return new ExifString(ExifTag.MDSampleInfo);
                case ExifTagValue.MDPrepDate: return new ExifString(ExifTag.MDPrepDate);
                case ExifTagValue.MDPrepTime: return new ExifString(ExifTag.MDPrepTime);
                case ExifTagValue.MDFileUnits: return new ExifString(ExifTag.MDFileUnits);
                case ExifTagValue.SEMInfo: return new ExifString(ExifTag.SEMInfo);
                case ExifTagValue.SpectralSensitivity: return new ExifString(ExifTag.SpectralSensitivity);
                case ExifTagValue.DateTimeOriginal: return new ExifString(ExifTag.DateTimeOriginal);
                case ExifTagValue.DateTimeDigitized: return new ExifString(ExifTag.DateTimeDigitized);
                case ExifTagValue.SubsecTime: return new ExifString(ExifTag.SubsecTime);
                case ExifTagValue.SubsecTimeOriginal: return new ExifString(ExifTag.SubsecTimeOriginal);
                case ExifTagValue.SubsecTimeDigitized: return new ExifString(ExifTag.SubsecTimeDigitized);
                case ExifTagValue.RelatedSoundFile: return new ExifString(ExifTag.RelatedSoundFile);
                case ExifTagValue.FaxSubaddress: return new ExifString(ExifTag.FaxSubaddress);
                case ExifTagValue.OffsetTime: return new ExifString(ExifTag.OffsetTime);
                case ExifTagValue.OffsetTimeOriginal: return new ExifString(ExifTag.OffsetTimeOriginal);
                case ExifTagValue.OffsetTimeDigitized: return new ExifString(ExifTag.OffsetTimeDigitized);
                case ExifTagValue.SecurityClassification: return new ExifString(ExifTag.SecurityClassification);
                case ExifTagValue.ImageHistory: return new ExifString(ExifTag.ImageHistory);
                case ExifTagValue.ImageUniqueID: return new ExifString(ExifTag.ImageUniqueID);
                case ExifTagValue.OwnerName: return new ExifString(ExifTag.OwnerName);
                case ExifTagValue.SerialNumber: return new ExifString(ExifTag.SerialNumber);
                case ExifTagValue.LensMake: return new ExifString(ExifTag.LensMake);
                case ExifTagValue.LensModel: return new ExifString(ExifTag.LensModel);
                case ExifTagValue.LensSerialNumber: return new ExifString(ExifTag.LensSerialNumber);
                case ExifTagValue.GDALMetadata: return new ExifString(ExifTag.GDALMetadata);
                case ExifTagValue.GDALNoData: return new ExifString(ExifTag.GDALNoData);
                case ExifTagValue.GPSLatitudeRef: return new ExifString(ExifTag.GPSLatitudeRef);
                case ExifTagValue.GPSLongitudeRef: return new ExifString(ExifTag.GPSLongitudeRef);
                case ExifTagValue.GPSSatellites: return new ExifString(ExifTag.GPSSatellites);
                case ExifTagValue.GPSStatus: return new ExifString(ExifTag.GPSStatus);
                case ExifTagValue.GPSMeasureMode: return new ExifString(ExifTag.GPSMeasureMode);
                case ExifTagValue.GPSSpeedRef: return new ExifString(ExifTag.GPSSpeedRef);
                case ExifTagValue.GPSTrackRef: return new ExifString(ExifTag.GPSTrackRef);
                case ExifTagValue.GPSImgDirectionRef: return new ExifString(ExifTag.GPSImgDirectionRef);
                case ExifTagValue.GPSMapDatum: return new ExifString(ExifTag.GPSMapDatum);
                case ExifTagValue.GPSDestLatitudeRef: return new ExifString(ExifTag.GPSDestLatitudeRef);
                case ExifTagValue.GPSDestLongitudeRef: return new ExifString(ExifTag.GPSDestLongitudeRef);
                case ExifTagValue.GPSDestBearingRef: return new ExifString(ExifTag.GPSDestBearingRef);
                case ExifTagValue.GPSDestDistanceRef: return new ExifString(ExifTag.GPSDestDistanceRef);
                case ExifTagValue.GPSDateStamp: return new ExifString(ExifTag.GPSDateStamp);

                case ExifTagValue.FileSource: return new ExifByte(ExifTag.FileSource, ExifDataType.Undefined);
                case ExifTagValue.SceneType: return new ExifByte(ExifTag.SceneType, ExifDataType.Undefined);

                case ExifTagValue.JPEGTables: return new ExifByteArray(ExifTag.JPEGTables, ExifDataType.Undefined);
                case ExifTagValue.OECF: return new ExifByteArray(ExifTag.OECF, ExifDataType.Undefined);
                case ExifTagValue.ExifVersion: return new ExifByteArray(ExifTag.ExifVersion, ExifDataType.Undefined);
                case ExifTagValue.ComponentsConfiguration: return new ExifByteArray(ExifTag.ComponentsConfiguration, ExifDataType.Undefined);
                case ExifTagValue.MakerNote: return new ExifByteArray(ExifTag.MakerNote, ExifDataType.Undefined);
                case ExifTagValue.UserComment: return new ExifByteArray(ExifTag.UserComment, ExifDataType.Undefined);
                case ExifTagValue.FlashpixVersion: return new ExifByteArray(ExifTag.FlashpixVersion, ExifDataType.Undefined);
                case ExifTagValue.SpatialFrequencyResponse: return new ExifByteArray(ExifTag.SpatialFrequencyResponse, ExifDataType.Undefined);
                case ExifTagValue.SpatialFrequencyResponse2: return new ExifByteArray(ExifTag.SpatialFrequencyResponse2, ExifDataType.Undefined);
                case ExifTagValue.Noise: return new ExifByteArray(ExifTag.Noise, ExifDataType.Undefined);
                case ExifTagValue.CFAPattern: return new ExifByteArray(ExifTag.CFAPattern, ExifDataType.Undefined);
                case ExifTagValue.DeviceSettingDescription: return new ExifByteArray(ExifTag.DeviceSettingDescription, ExifDataType.Undefined);
                case ExifTagValue.ImageSourceData: return new ExifByteArray(ExifTag.ImageSourceData, ExifDataType.Undefined);
                case ExifTagValue.GPSProcessingMethod: return new ExifByteArray(ExifTag.GPSProcessingMethod, ExifDataType.Undefined);
                case ExifTagValue.GPSAreaInformation: return new ExifByteArray(ExifTag.GPSAreaInformation, ExifDataType.Undefined);

                default: return null;
            }
        }
    }
}

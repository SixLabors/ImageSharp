// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Exif.Values
{
    public class ExifValuesTests
    {
        public static TheoryData<ExifTag> ByteTags => new TheoryData<ExifTag>
        {
            { ExifTag.FaxProfile },
            { ExifTag.ModeNumber },
            { ExifTag.GPSAltitudeRef }
        };

        public static TheoryData<ExifTag> ByteArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.ClipPath },
            { ExifTag.VersionYear },
            { ExifTag.XMP },
            { ExifTag.CFAPattern2 },
            { ExifTag.TIFFEPStandardID },
            { ExifTag.XPTitle },
            { ExifTag.XPComment },
            { ExifTag.XPAuthor },
            { ExifTag.XPKeywords },
            { ExifTag.XPSubject },
            { ExifTag.GPSVersionID },
        };

        public static TheoryData<ExifTag> DoubleArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.PixelScale },
            { ExifTag.IntergraphMatrix },
            { ExifTag.ModelTiePoint },
            { ExifTag.ModelTransform }
        };

        public static TheoryData<ExifTag> LongTags => new TheoryData<ExifTag>
        {
            { ExifTag.SubfileType },
            { ExifTag.SubIFDOffset },
            { ExifTag.GPSIFDOffset },
            { ExifTag.T4Options },
            { ExifTag.T6Options },
            { ExifTag.XClipPathUnits },
            { ExifTag.YClipPathUnits },
            { ExifTag.ProfileType },
            { ExifTag.CodingMethods },
            { ExifTag.T82ptions },
            { ExifTag.JPEGInterchangeFormat },
            { ExifTag.JPEGInterchangeFormatLength },
            { ExifTag.MDFileTag },
            { ExifTag.StandardOutputSensitivity },
            { ExifTag.RecommendedExposureIndex },
            { ExifTag.ISOSpeed },
            { ExifTag.ISOSpeedLatitudeyyy },
            { ExifTag.ISOSpeedLatitudezzz },
            { ExifTag.FaxRecvParams },
            { ExifTag.FaxRecvTime },
            { ExifTag.ImageNumber },
        };

        public static TheoryData<ExifTag> LongArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.FreeOffsets },
            { ExifTag.FreeByteCounts },
            { ExifTag.ColorResponseUnit },
            { ExifTag.TileOffsets },
            { ExifTag.SMinSampleValue },
            { ExifTag.SMaxSampleValue },
            { ExifTag.JPEGQTables },
            { ExifTag.JPEGDCTables },
            { ExifTag.JPEGACTables },
            { ExifTag.StripRowCounts },
            { ExifTag.IntergraphRegisters },
            { ExifTag.TimeZoneOffset }
        };

        public static TheoryData<ExifTag> NumberTags => new TheoryData<ExifTag>
        {
            { ExifTag.ImageWidth },
            { ExifTag.ImageLength },
            { ExifTag.TileWidth },
            { ExifTag.TileLength },
            { ExifTag.BadFaxLines },
            { ExifTag.ConsecutiveBadFaxLines },
            { ExifTag.PixelXDimension },
            { ExifTag.PixelYDimension }
        };

        public static TheoryData<ExifTag> NumberArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.StripOffsets },
            { ExifTag.TileByteCounts },
            { ExifTag.ImageLayer }
        };

        public static TheoryData<ExifTag> RationalTags => new TheoryData<ExifTag>
        {
            { ExifTag.XPosition },
            { ExifTag.YPosition },
            { ExifTag.XResolution },
            { ExifTag.YResolution },
            { ExifTag.BatteryLevel },
            { ExifTag.ExposureTime },
            { ExifTag.FNumber },
            { ExifTag.MDScalePixel },
            { ExifTag.CompressedBitsPerPixel },
            { ExifTag.ApertureValue },
            { ExifTag.MaxApertureValue },
            { ExifTag.SubjectDistance },
            { ExifTag.FocalLength },
            { ExifTag.FlashEnergy2 },
            { ExifTag.FocalPlaneXResolution2 },
            { ExifTag.FocalPlaneYResolution2 },
            { ExifTag.ExposureIndex2 },
            { ExifTag.Humidity },
            { ExifTag.Pressure },
            { ExifTag.Acceleration },
            { ExifTag.FlashEnergy },
            { ExifTag.FocalPlaneXResolution },
            { ExifTag.FocalPlaneYResolution },
            { ExifTag.ExposureIndex },
            { ExifTag.DigitalZoomRatio },
            { ExifTag.GPSAltitude },
            { ExifTag.GPSDOP },
            { ExifTag.GPSSpeed },
            { ExifTag.GPSTrack },
            { ExifTag.GPSImgDirection },
            { ExifTag.GPSDestBearing },
            { ExifTag.GPSDestDistance },
        };

        public static TheoryData<ExifTag> RationalArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.WhitePoint },
            { ExifTag.PrimaryChromaticities },
            { ExifTag.YCbCrCoefficients },
            { ExifTag.ReferenceBlackWhite },
            { ExifTag.GPSLatitude },
            { ExifTag.GPSLongitude },
            { ExifTag.GPSTimestamp },
            { ExifTag.GPSDestLatitude },
            { ExifTag.GPSDestLongitude },
            { ExifTag.LensSpecification }
        };

        public static TheoryData<ExifTag> ShortTags => new TheoryData<ExifTag>
        {
            { ExifTag.OldSubfileType },
            { ExifTag.Compression },
            { ExifTag.PhotometricInterpretation },
            { ExifTag.Thresholding },
            { ExifTag.CellWidth },
            { ExifTag.CellLength },
            { ExifTag.FillOrder },
            { ExifTag.Orientation },
            { ExifTag.SamplesPerPixel },
            { ExifTag.PlanarConfiguration },
            { ExifTag.GrayResponseUnit },
            { ExifTag.ResolutionUnit },
            { ExifTag.CleanFaxData },
            { ExifTag.InkSet },
            { ExifTag.NumberOfInks },
            { ExifTag.DotRange },
            { ExifTag.Indexed },
            { ExifTag.OPIProxy },
            { ExifTag.JPEGProc },
            { ExifTag.JPEGRestartInterval },
            { ExifTag.YCbCrPositioning },
            { ExifTag.Rating },
            { ExifTag.RatingPercent },
            { ExifTag.ExposureProgram },
            { ExifTag.Interlace },
            { ExifTag.SelfTimerMode },
            { ExifTag.SensitivityType },
            { ExifTag.MeteringMode },
            { ExifTag.LightSource },
            { ExifTag.FocalPlaneResolutionUnit2 },
            { ExifTag.SensingMethod2 },
            { ExifTag.Flash },
            { ExifTag.ColorSpace },
            { ExifTag.FocalPlaneResolutionUnit },
            { ExifTag.SensingMethod },
            { ExifTag.CustomRendered },
            { ExifTag.ExposureMode },
            { ExifTag.WhiteBalance },
            { ExifTag.FocalLengthIn35mmFilm },
            { ExifTag.SceneCaptureType },
            { ExifTag.GainControl },
            { ExifTag.Contrast },
            { ExifTag.Saturation },
            { ExifTag.Sharpness },
            { ExifTag.SubjectDistanceRange },
            { ExifTag.GPSDifferential }
        };

        public static TheoryData<ExifTag> ShortArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.BitsPerSample },
            { ExifTag.MinSampleValue },
            { ExifTag.MaxSampleValue },
            { ExifTag.GrayResponseCurve },
            { ExifTag.ColorMap },
            { ExifTag.ExtraSamples },
            { ExifTag.PageNumber },
            { ExifTag.TransferFunction },
            { ExifTag.Predictor },
            { ExifTag.HalftoneHints },
            { ExifTag.SampleFormat },
            { ExifTag.TransferRange },
            { ExifTag.DefaultImageColor },
            { ExifTag.JPEGLosslessPredictors },
            { ExifTag.JPEGPointTransforms },
            { ExifTag.YCbCrSubsampling },
            { ExifTag.CFARepeatPatternDim },
            { ExifTag.IntergraphPacketData },
            { ExifTag.ISOSpeedRatings },
            { ExifTag.SubjectArea },
            { ExifTag.SubjectLocation }
        };

        public static TheoryData<ExifTag> SignedRationalTags => new TheoryData<ExifTag>
        {
            { ExifTag.ShutterSpeedValue },
            { ExifTag.BrightnessValue },
            { ExifTag.ExposureBiasValue },
            { ExifTag.AmbientTemperature },
            { ExifTag.WaterDepth },
            { ExifTag.CameraElevationAngle }
        };

        public static TheoryData<ExifTag> SignedRationalArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.Decode }
        };

        public static TheoryData<ExifTag> StringTags => new TheoryData<ExifTag>
        {
            { ExifTag.ImageDescription },
            { ExifTag.Make },
            { ExifTag.Model },
            { ExifTag.Software },
            { ExifTag.DateTime },
            { ExifTag.Artist },
            { ExifTag.HostComputer },
            { ExifTag.Copyright },
            { ExifTag.DocumentName },
            { ExifTag.PageName },
            { ExifTag.InkNames },
            { ExifTag.TargetPrinter },
            { ExifTag.ImageID },
            { ExifTag.MDLabName },
            { ExifTag.MDSampleInfo },
            { ExifTag.MDPrepDate },
            { ExifTag.MDPrepTime },
            { ExifTag.MDFileUnits },
            { ExifTag.SEMInfo },
            { ExifTag.SpectralSensitivity },
            { ExifTag.DateTimeOriginal },
            { ExifTag.DateTimeDigitized },
            { ExifTag.SubsecTime },
            { ExifTag.SubsecTimeOriginal },
            { ExifTag.SubsecTimeDigitized },
            { ExifTag.RelatedSoundFile },
            { ExifTag.FaxSubaddress },
            { ExifTag.OffsetTime },
            { ExifTag.OffsetTimeOriginal },
            { ExifTag.OffsetTimeDigitized },
            { ExifTag.SecurityClassification },
            { ExifTag.ImageHistory },
            { ExifTag.ImageUniqueID },
            { ExifTag.OwnerName },
            { ExifTag.SerialNumber },
            { ExifTag.LensMake },
            { ExifTag.LensModel },
            { ExifTag.LensSerialNumber },
            { ExifTag.GDALMetadata },
            { ExifTag.GDALNoData },
            { ExifTag.GPSLatitudeRef },
            { ExifTag.GPSLongitudeRef },
            { ExifTag.GPSSatellites },
            { ExifTag.GPSStatus },
            { ExifTag.GPSMeasureMode },
            { ExifTag.GPSSpeedRef },
            { ExifTag.GPSTrackRef },
            { ExifTag.GPSImgDirectionRef },
            { ExifTag.GPSMapDatum },
            { ExifTag.GPSDestLatitudeRef },
            { ExifTag.GPSDestLongitudeRef },
            { ExifTag.GPSDestBearingRef },
            { ExifTag.GPSDestDistanceRef },
            { ExifTag.GPSDateStamp }
        };

        public static TheoryData<ExifTag> UndefinedTags => new TheoryData<ExifTag>
        {
            { ExifTag.FileSource },
            { ExifTag.SceneType }
        };

        public static TheoryData<ExifTag> UndefinedArrayTags => new TheoryData<ExifTag>
        {
            { ExifTag.JPEGTables },
            { ExifTag.OECF },
            { ExifTag.ExifVersion },
            { ExifTag.ComponentsConfiguration },
            { ExifTag.MakerNote },
            { ExifTag.UserComment },
            { ExifTag.FlashpixVersion },
            { ExifTag.SpatialFrequencyResponse },
            { ExifTag.SpatialFrequencyResponse2 },
            { ExifTag.Noise },
            { ExifTag.CFAPattern },
            { ExifTag.DeviceSettingDescription },
            { ExifTag.ImageSourceData },
            { ExifTag.GPSProcessingMethod },
            { ExifTag.GPSAreaInformation }
        };

        [Theory]
        [MemberData(nameof(ByteTags))]
        public void ExifByteTests(ExifTag tag)
        {
            const byte expected = byte.MaxValue;
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue((int)expected));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifByte)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(ByteArrayTags))]
        public void ExifByteArrayTests(ExifTag tag)
        {
            byte[] expected = new[] { byte.MaxValue };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifByteArray)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(DoubleArrayTags))]
        public void ExifDoubleArrayTests(ExifTag tag)
        {
            double[] expected = new[] { double.MaxValue };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifDoubleArray)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(LongTags))]
        public void ExifLongTests(ExifTag tag)
        {
            const uint expected = uint.MaxValue;
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifLong)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(LongArrayTags))]
        public void ExifLongArrayTests(ExifTag tag)
        {
            uint[] expected = new[] { uint.MaxValue };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifLongArray)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(NumberTags))]
        public void ExifNumberTests(ExifTag tag)
        {
            Number expected = ushort.MaxValue;
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue((uint)expected));
            Assert.True(value.TrySetValue((int)expected));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifNumber)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(NumberArrayTags))]
        public void ExifNumberArrayTests(ExifTag tag)
        {
            Number[] expected = new[] { new Number(uint.MaxValue) };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifNumberArray)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(RationalTags))]
        public void ExifRationalTests(ExifTag tag)
        {
            var expected = new Rational(21, 42);
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(new SignedRational(expected.ToDouble())));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifRational)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(RationalArrayTags))]
        public void ExifRationalArrayTests(ExifTag tag)
        {
            Rational[] expected = new[] { new Rational(21, 42) };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifRationalArray)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(ShortTags))]
        public void ExifShortTests(ExifTag tag)
        {
            const ushort expected = (ushort)short.MaxValue;
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue((int)expected));
            Assert.True(value.TrySetValue((short)expected));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifShort)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(ShortArrayTags))]
        public void ExifShortArrayTests(ExifTag tag)
        {
            ushort[] expected = new[] { ushort.MaxValue };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifShortArray)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(SignedRationalTags))]
        public void ExifSignedRationalTests(ExifTag tag)
        {
            var expected = new SignedRational(21, 42);
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifSignedRational)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(SignedRationalArrayTags))]
        public void ExifSignedRationalArrayTests(ExifTag tag)
        {
            SignedRational[] expected = new[] { new SignedRational(21, 42) };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifSignedRationalArray)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(StringTags))]
        public void ExifStringTests(ExifTag tag)
        {
            const string expected = "ImageSharp";
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(0M));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifString)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(UndefinedTags))]
        public void ExifUndefinedTests(ExifTag tag)
        {
            const byte expected = byte.MaxValue;
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue((int)expected));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifByte)value;
            Assert.Equal(expected, typed.Value);
        }

        [Theory]
        [MemberData(nameof(UndefinedArrayTags))]
        public void ExifUndefinedArrayTests(ExifTag tag)
        {
            byte[] expected = new[] { byte.MaxValue };
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(expected.ToString()));
            Assert.True(value.TrySetValue(expected));

            var typed = (ExifByteArray)value;
            Assert.Equal(expected, typed.Value);
        }
    }
}

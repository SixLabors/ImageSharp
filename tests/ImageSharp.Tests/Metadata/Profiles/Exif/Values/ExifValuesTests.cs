// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Exif.Values;

[Trait("Profile", "Exif")]
public class ExifValuesTests
{
    public static TheoryData<ExifTag> ByteTags => new()
    {
        { (ExifTag)ExifTag.FaxProfile },
        { (ExifTag)ExifTag.ModeNumber },
        { (ExifTag)ExifTag.GPSAltitudeRef }
    };

    public static TheoryData<ExifTag> ByteArrayTags => new()
    {
        { (ExifTag)ExifTag.ClipPath },
        { (ExifTag)ExifTag.VersionYear },
        { (ExifTag)ExifTag.XMP },
        { (ExifTag)ExifTag.CFAPattern2 },
        { (ExifTag)ExifTag.TIFFEPStandardID },
        { (ExifTag)ExifTag.GPSVersionID },
    };

    public static TheoryData<ExifTag> DoubleArrayTags => new()
    {
        { (ExifTag)ExifTag.PixelScale },
        { (ExifTag)ExifTag.IntergraphMatrix },
        { (ExifTag)ExifTag.ModelTiePoint },
        { (ExifTag)ExifTag.ModelTransform }
    };

    public static TheoryData<ExifTag> LongTags => new()
    {
        { (ExifTag)ExifTag.SubfileType },
        { (ExifTag)ExifTag.SubIFDOffset },
        { (ExifTag)ExifTag.GPSIFDOffset },
        { (ExifTag)ExifTag.T4Options },
        { (ExifTag)ExifTag.T6Options },
        { (ExifTag)ExifTag.XClipPathUnits },
        { (ExifTag)ExifTag.YClipPathUnits },
        { (ExifTag)ExifTag.ProfileType },
        { (ExifTag)ExifTag.CodingMethods },
        { (ExifTag)ExifTag.T82ptions },
        { (ExifTag)ExifTag.JPEGInterchangeFormat },
        { (ExifTag)ExifTag.JPEGInterchangeFormatLength },
        { (ExifTag)ExifTag.MDFileTag },
        { (ExifTag)ExifTag.StandardOutputSensitivity },
        { (ExifTag)ExifTag.RecommendedExposureIndex },
        { (ExifTag)ExifTag.ISOSpeed },
        { (ExifTag)ExifTag.ISOSpeedLatitudeyyy },
        { (ExifTag)ExifTag.ISOSpeedLatitudezzz },
        { (ExifTag)ExifTag.FaxRecvParams },
        { (ExifTag)ExifTag.FaxRecvTime },
        { (ExifTag)ExifTag.ImageNumber },
    };

    public static TheoryData<ExifTag> LongArrayTags => new()
    {
        { (ExifTag)ExifTag.FreeOffsets },
        { (ExifTag)ExifTag.FreeByteCounts },
        { (ExifTag)ExifTag.ColorResponseUnit },
        { (ExifTag)ExifTag.SMinSampleValue },
        { (ExifTag)ExifTag.SMaxSampleValue },
        { (ExifTag)ExifTag.JPEGQTables },
        { (ExifTag)ExifTag.JPEGDCTables },
        { (ExifTag)ExifTag.JPEGACTables },
        { (ExifTag)ExifTag.StripRowCounts },
        { (ExifTag)ExifTag.IntergraphRegisters }
    };

    public static TheoryData<ExifTag> NumberTags => new()
    {
        { (ExifTag)ExifTag.ImageWidth },
        { (ExifTag)ExifTag.ImageLength },
        { (ExifTag)ExifTag.TileWidth },
        { (ExifTag)ExifTag.TileLength },
        { (ExifTag)ExifTag.BadFaxLines },
        { (ExifTag)ExifTag.ConsecutiveBadFaxLines },
        { (ExifTag)ExifTag.PixelXDimension },
        { (ExifTag)ExifTag.PixelYDimension }
    };

    public static TheoryData<ExifTag> NumberArrayTags => new()
    {
        { (ExifTag)ExifTag.StripOffsets },
        { (ExifTag)ExifTag.StripByteCounts },
        { (ExifTag)ExifTag.TileByteCounts },
        { (ExifTag)ExifTag.TileOffsets },
        { (ExifTag)ExifTag.ImageLayer }
    };

    public static TheoryData<ExifTag> RationalTags => new()
    {
        { (ExifTag)ExifTag.XPosition },
        { (ExifTag)ExifTag.YPosition },
        { (ExifTag)ExifTag.XResolution },
        { (ExifTag)ExifTag.YResolution },
        { (ExifTag)ExifTag.BatteryLevel },
        { (ExifTag)ExifTag.ExposureTime },
        { (ExifTag)ExifTag.FNumber },
        { (ExifTag)ExifTag.MDScalePixel },
        { (ExifTag)ExifTag.CompressedBitsPerPixel },
        { (ExifTag)ExifTag.ApertureValue },
        { (ExifTag)ExifTag.MaxApertureValue },
        { (ExifTag)ExifTag.SubjectDistance },
        { (ExifTag)ExifTag.FocalLength },
        { (ExifTag)ExifTag.FlashEnergy2 },
        { (ExifTag)ExifTag.FocalPlaneXResolution2 },
        { (ExifTag)ExifTag.FocalPlaneYResolution2 },
        { (ExifTag)ExifTag.ExposureIndex2 },
        { (ExifTag)ExifTag.Humidity },
        { (ExifTag)ExifTag.Pressure },
        { (ExifTag)ExifTag.Acceleration },
        { (ExifTag)ExifTag.FlashEnergy },
        { (ExifTag)ExifTag.FocalPlaneXResolution },
        { (ExifTag)ExifTag.FocalPlaneYResolution },
        { (ExifTag)ExifTag.ExposureIndex },
        { (ExifTag)ExifTag.DigitalZoomRatio },
        { (ExifTag)ExifTag.GPSAltitude },
        { (ExifTag)ExifTag.GPSDOP },
        { (ExifTag)ExifTag.GPSSpeed },
        { (ExifTag)ExifTag.GPSTrack },
        { (ExifTag)ExifTag.GPSImgDirection },
        { (ExifTag)ExifTag.GPSDestBearing },
        { (ExifTag)ExifTag.GPSDestDistance },
        { (ExifTag)ExifTag.GPSHPositioningError },
    };

    public static TheoryData<ExifTag> RationalArrayTags => new()
    {
        { (ExifTag)ExifTag.WhitePoint },
        { (ExifTag)ExifTag.PrimaryChromaticities },
        { (ExifTag)ExifTag.YCbCrCoefficients },
        { (ExifTag)ExifTag.ReferenceBlackWhite },
        { (ExifTag)ExifTag.GPSLatitude },
        { (ExifTag)ExifTag.GPSLongitude },
        { (ExifTag)ExifTag.GPSTimestamp },
        { (ExifTag)ExifTag.GPSDestLatitude },
        { (ExifTag)ExifTag.GPSDestLongitude },
        { (ExifTag)ExifTag.LensSpecification }
    };

    public static TheoryData<ExifTag> ShortTags => new()
    {
        { (ExifTag)ExifTag.OldSubfileType },
        { (ExifTag)ExifTag.Compression },
        { (ExifTag)ExifTag.PhotometricInterpretation },
        { (ExifTag)ExifTag.Thresholding },
        { (ExifTag)ExifTag.CellWidth },
        { (ExifTag)ExifTag.CellLength },
        { (ExifTag)ExifTag.FillOrder },
        { (ExifTag)ExifTag.Orientation },
        { (ExifTag)ExifTag.SamplesPerPixel },
        { (ExifTag)ExifTag.PlanarConfiguration },
        { (ExifTag)ExifTag.Predictor },
        { (ExifTag)ExifTag.GrayResponseUnit },
        { (ExifTag)ExifTag.ResolutionUnit },
        { (ExifTag)ExifTag.CleanFaxData },
        { (ExifTag)ExifTag.InkSet },
        { (ExifTag)ExifTag.NumberOfInks },
        { (ExifTag)ExifTag.DotRange },
        { (ExifTag)ExifTag.Indexed },
        { (ExifTag)ExifTag.OPIProxy },
        { (ExifTag)ExifTag.JPEGProc },
        { (ExifTag)ExifTag.JPEGRestartInterval },
        { (ExifTag)ExifTag.YCbCrPositioning },
        { (ExifTag)ExifTag.Rating },
        { (ExifTag)ExifTag.RatingPercent },
        { (ExifTag)ExifTag.ExposureProgram },
        { (ExifTag)ExifTag.Interlace },
        { (ExifTag)ExifTag.SelfTimerMode },
        { (ExifTag)ExifTag.SensitivityType },
        { (ExifTag)ExifTag.MeteringMode },
        { (ExifTag)ExifTag.LightSource },
        { (ExifTag)ExifTag.FocalPlaneResolutionUnit2 },
        { (ExifTag)ExifTag.SensingMethod2 },
        { (ExifTag)ExifTag.Flash },
        { (ExifTag)ExifTag.ColorSpace },
        { (ExifTag)ExifTag.FocalPlaneResolutionUnit },
        { (ExifTag)ExifTag.SensingMethod },
        { (ExifTag)ExifTag.CustomRendered },
        { (ExifTag)ExifTag.ExposureMode },
        { (ExifTag)ExifTag.WhiteBalance },
        { (ExifTag)ExifTag.FocalLengthIn35mmFilm },
        { (ExifTag)ExifTag.SceneCaptureType },
        { (ExifTag)ExifTag.GainControl },
        { (ExifTag)ExifTag.Contrast },
        { (ExifTag)ExifTag.Saturation },
        { (ExifTag)ExifTag.Sharpness },
        { (ExifTag)ExifTag.SubjectDistanceRange },
        { (ExifTag)ExifTag.GPSDifferential }
    };

    public static TheoryData<ExifTag> ShortArrayTags => new()
    {
        { (ExifTag)ExifTag.BitsPerSample },
        { (ExifTag)ExifTag.MinSampleValue },
        { (ExifTag)ExifTag.MaxSampleValue },
        { (ExifTag)ExifTag.GrayResponseCurve },
        { (ExifTag)ExifTag.ColorMap },
        { (ExifTag)ExifTag.ExtraSamples },
        { (ExifTag)ExifTag.PageNumber },
        { (ExifTag)ExifTag.TransferFunction },
        { (ExifTag)ExifTag.HalftoneHints },
        { (ExifTag)ExifTag.SampleFormat },
        { (ExifTag)ExifTag.TransferRange },
        { (ExifTag)ExifTag.DefaultImageColor },
        { (ExifTag)ExifTag.JPEGLosslessPredictors },
        { (ExifTag)ExifTag.JPEGPointTransforms },
        { (ExifTag)ExifTag.YCbCrSubsampling },
        { (ExifTag)ExifTag.CFARepeatPatternDim },
        { (ExifTag)ExifTag.IntergraphPacketData },
        { (ExifTag)ExifTag.ISOSpeedRatings },
        { (ExifTag)ExifTag.SubjectArea },
        { (ExifTag)ExifTag.SubjectLocation }
    };

    public static TheoryData<ExifTag> SignedRationalTags => new()
    {
        { (ExifTag)ExifTag.ShutterSpeedValue },
        { (ExifTag)ExifTag.BrightnessValue },
        { (ExifTag)ExifTag.ExposureBiasValue },
        { (ExifTag)ExifTag.AmbientTemperature },
        { (ExifTag)ExifTag.WaterDepth },
        { (ExifTag)ExifTag.CameraElevationAngle }
    };

    public static TheoryData<ExifTag> SignedRationalArrayTags => new()
    {
        { (ExifTag)ExifTag.Decode }
    };

    public static TheoryData<ExifTag> SignedShortArrayTags => new()
    {
        { (ExifTag)ExifTag.TimeZoneOffset }
    };

    public static TheoryData<ExifTag> StringTags => new()
    {
        { (ExifTag)ExifTag.ImageDescription },
        { (ExifTag)ExifTag.Make },
        { (ExifTag)ExifTag.Model },
        { (ExifTag)ExifTag.Software },
        { (ExifTag)ExifTag.DateTime },
        { (ExifTag)ExifTag.Artist },
        { (ExifTag)ExifTag.HostComputer },
        { (ExifTag)ExifTag.Copyright },
        { (ExifTag)ExifTag.DocumentName },
        { (ExifTag)ExifTag.PageName },
        { (ExifTag)ExifTag.InkNames },
        { (ExifTag)ExifTag.TargetPrinter },
        { (ExifTag)ExifTag.ImageID },
        { (ExifTag)ExifTag.MDLabName },
        { (ExifTag)ExifTag.MDSampleInfo },
        { (ExifTag)ExifTag.MDPrepDate },
        { (ExifTag)ExifTag.MDPrepTime },
        { (ExifTag)ExifTag.MDFileUnits },
        { (ExifTag)ExifTag.SEMInfo },
        { (ExifTag)ExifTag.SpectralSensitivity },
        { (ExifTag)ExifTag.DateTimeOriginal },
        { (ExifTag)ExifTag.DateTimeDigitized },
        { (ExifTag)ExifTag.SubsecTime },
        { (ExifTag)ExifTag.SubsecTimeOriginal },
        { (ExifTag)ExifTag.SubsecTimeDigitized },
        { (ExifTag)ExifTag.RelatedSoundFile },
        { (ExifTag)ExifTag.FaxSubaddress },
        { (ExifTag)ExifTag.OffsetTime },
        { (ExifTag)ExifTag.OffsetTimeOriginal },
        { (ExifTag)ExifTag.OffsetTimeDigitized },
        { (ExifTag)ExifTag.SecurityClassification },
        { (ExifTag)ExifTag.ImageHistory },
        { (ExifTag)ExifTag.ImageUniqueID },
        { (ExifTag)ExifTag.OwnerName },
        { (ExifTag)ExifTag.SerialNumber },
        { (ExifTag)ExifTag.LensMake },
        { (ExifTag)ExifTag.LensModel },
        { (ExifTag)ExifTag.LensSerialNumber },
        { (ExifTag)ExifTag.GDALMetadata },
        { (ExifTag)ExifTag.GDALNoData },
        { (ExifTag)ExifTag.GPSLatitudeRef },
        { (ExifTag)ExifTag.GPSLongitudeRef },
        { (ExifTag)ExifTag.GPSSatellites },
        { (ExifTag)ExifTag.GPSStatus },
        { (ExifTag)ExifTag.GPSMeasureMode },
        { (ExifTag)ExifTag.GPSSpeedRef },
        { (ExifTag)ExifTag.GPSTrackRef },
        { (ExifTag)ExifTag.GPSImgDirectionRef },
        { (ExifTag)ExifTag.GPSMapDatum },
        { (ExifTag)ExifTag.GPSDestLatitudeRef },
        { (ExifTag)ExifTag.GPSDestLongitudeRef },
        { (ExifTag)ExifTag.GPSDestBearingRef },
        { (ExifTag)ExifTag.GPSDestDistanceRef },
        { (ExifTag)ExifTag.GPSDateStamp },
    };

    public static TheoryData<ExifTag> UndefinedTags => new()
    {
        { (ExifTag)ExifTag.FileSource },
        { (ExifTag)ExifTag.SceneType }
    };

    public static TheoryData<ExifTag> UndefinedArrayTags => new()
    {
        { (ExifTag)ExifTag.JPEGTables },
        { (ExifTag)ExifTag.OECF },
        { (ExifTag)ExifTag.ExifVersion },
        { (ExifTag)ExifTag.ComponentsConfiguration },
        { (ExifTag)ExifTag.MakerNote },
        { (ExifTag)ExifTag.FlashpixVersion },
        { (ExifTag)ExifTag.SpatialFrequencyResponse },
        { (ExifTag)ExifTag.SpatialFrequencyResponse2 },
        { (ExifTag)ExifTag.Noise },
        { (ExifTag)ExifTag.CFAPattern },
        { (ExifTag)ExifTag.DeviceSettingDescription },
        { (ExifTag)ExifTag.ImageSourceData },
    };

    public static TheoryData<ExifTag> EncodedStringTags => new()
    {
        { (ExifTag)ExifTag.UserComment },
        { (ExifTag)ExifTag.GPSProcessingMethod },
        { (ExifTag)ExifTag.GPSAreaInformation }
    };

    public static TheoryData<ExifTag> Ucs2StringTags => new()
    {
        { (ExifTag)ExifTag.XPTitle },
        { (ExifTag)ExifTag.XPComment },
        { (ExifTag)ExifTag.XPAuthor },
        { (ExifTag)ExifTag.XPKeywords },
        { (ExifTag)ExifTag.XPSubject },
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

        ExifByte typed = (ExifByte)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(ByteArrayTags))]
    public void ExifByteArrayTests(ExifTag tag)
    {
        byte[] expected = [byte.MaxValue];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifByteArray typed = (ExifByteArray)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(DoubleArrayTags))]
    public void ExifDoubleArrayTests(ExifTag tag)
    {
        double[] expected = [double.MaxValue];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifDoubleArray typed = (ExifDoubleArray)value;
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

        ExifLong typed = (ExifLong)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(LongArrayTags))]
    public void ExifLongArrayTests(ExifTag tag)
    {
        uint[] expected = [uint.MaxValue];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifLongArray typed = (ExifLongArray)value;
        Assert.Equal(expected, typed.Value);
    }

    [Fact]
    public void NumberTests()
    {
        Number value1 = ushort.MaxValue;
        Number value2 = ushort.MaxValue;
        Assert.True(value1 == value2);

        value2 = short.MaxValue;
        Assert.True(value1 != value2);

        value1 = -1;
        value2 = -2;
        Assert.True(value1 > value2);

        value1 = -6;
        Assert.True(value1 <= value2);

        value1 = 10;
        value2 = 10;
        Assert.True(value1 >= value2);

        Assert.True(value1.Equals(value2));
        Assert.True(value1.GetHashCode() == value2.GetHashCode());

        value1 = 1;
        Assert.False(value1.Equals(value2));
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

        ExifNumber typed = (ExifNumber)value;
        Assert.Equal(expected, typed.Value);

        typed.Value = ushort.MaxValue + 1;
        Assert.True(expected < typed.Value);
    }

    [Theory]
    [MemberData(nameof(NumberArrayTags))]
    public void ExifNumberArrayTests(ExifTag tag)
    {
        Number[] expected = [new Number(uint.MaxValue)];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifNumberArray typed = (ExifNumberArray)value;
        Assert.Equal(expected, typed.Value);

        Assert.True(value.TrySetValue(int.MaxValue));
        Assert.Equal(new[] { (Number)int.MaxValue }, value.GetValue());

        Assert.True(value.TrySetValue(new[] { 1u, 2u, 5u }));
        Assert.Equal(new[] { (Number)1u, (Number)2u, (Number)5u }, value.GetValue());

        Assert.True(value.TrySetValue(new[] { (short)1, (short)2, (short)5 }));
        Assert.Equal(new[] { (Number)(short)1, (Number)(short)2, (Number)(short)5 }, value.GetValue());
    }

    [Theory]
    [MemberData(nameof(RationalTags))]
    public void ExifRationalTests(ExifTag tag)
    {
        Rational expected = new(21, 42);
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(new SignedRational(expected.ToDouble())));
        Assert.True(value.TrySetValue(expected));

        ExifRational typed = (ExifRational)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(RationalArrayTags))]
    public void ExifRationalArrayTests(ExifTag tag)
    {
        Rational[] expected = [new Rational(21, 42)];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifRationalArray typed = (ExifRationalArray)value;
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

        ExifShort typed = (ExifShort)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(ShortArrayTags))]
    public void ExifShortArrayTests(ExifTag tag)
    {
        ushort[] expected = [ushort.MaxValue];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifShortArray typed = (ExifShortArray)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(SignedRationalTags))]
    public void ExifSignedRationalTests(ExifTag tag)
    {
        SignedRational expected = new(21, 42);
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifSignedRational typed = (ExifSignedRational)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(SignedRationalArrayTags))]
    public void ExifSignedRationalArrayTests(ExifTag tag)
    {
        SignedRational[] expected = [new SignedRational(21, 42)];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifSignedRationalArray typed = (ExifSignedRationalArray)value;
        Assert.Equal(expected, typed.Value);
    }


    [Theory]
    [MemberData(nameof(SignedShortArrayTags))]
    public void ExifSignedShortArrayTests(ExifTag tag)
    {
        short[] expected = [21, 42];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifSignedShortArray typed = (ExifSignedShortArray)value;
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

        ExifString typed = (ExifString)value;
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

        ExifByte typed = (ExifByte)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(UndefinedArrayTags))]
    public void ExifUndefinedArrayTests(ExifTag tag)
    {
        byte[] expected = [byte.MaxValue];
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(expected.ToString()));
        Assert.True(value.TrySetValue(expected));

        ExifByteArray typed = (ExifByteArray)value;
        Assert.Equal(expected, typed.Value);
    }

    [Theory]
    [MemberData(nameof(EncodedStringTags))]
    public void ExifEncodedStringTests(ExifTag tag)
    {
        foreach (object code in Enum.GetValues(typeof(EncodedString.CharacterCode)))
        {
            EncodedString.CharacterCode charCode = (EncodedString.CharacterCode)code;

            Assert.Equal(ExifEncodedStringHelpers.CharacterCodeBytesLength, ExifEncodedStringHelpers.GetCodeBytes(charCode).Length);

            const string expectedText = "test string";
            EncodedString expected = new(charCode, expectedText);
            ExifValue value = ExifValues.Create(tag);

            Assert.False(value.TrySetValue(123));
            Assert.True(value.TrySetValue(expected));

            ExifEncodedString typed = (ExifEncodedString)value;
            Assert.Equal(expected, typed.Value);
            Assert.Equal(expectedText, (string)typed.Value);
            Assert.Equal(charCode, typed.Value.Code);
        }
    }

    [Theory]
    [MemberData(nameof(Ucs2StringTags))]
    public void ExifUcs2StringTests(ExifTag tag)
    {
        const string expected = "Dan Petitt";
        ExifValue value = ExifValues.Create(tag);

        Assert.False(value.TrySetValue(123));
        Assert.True(value.TrySetValue(expected));

        ExifUcs2String typed = (ExifUcs2String)value;
        Assert.Equal(expected, typed.Value);

        Assert.True(value.TrySetValue(Encoding.GetEncoding("UCS-2").GetBytes(expected)));
        Assert.Equal(expected, typed.Value);
    }
}

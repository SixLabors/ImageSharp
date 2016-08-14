// <copyright file="ExifTag.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// All exif tags from the Exif standard 2.2
    /// </summary>
    public enum ExifTag
    {
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 0xFFFF,

        /// <summary>
        /// SubIFDOffset
        /// </summary>
        SubIFDOffset = 0x8769,

        /// <summary>
        /// GPSIFDOffset
        /// </summary>
        GPSIFDOffset = 0x8825,


        /// <summary>
        /// ImageWidth
        /// </summary>
        ImageWidth = 0x0100,

        /// <summary>
        /// ImageLength
        /// </summary>
        ImageLength = 0x0101,

        /// <summary>
        /// BitsPerSample
        /// </summary>
        BitsPerSample = 0x0102,

        /// <summary>
        /// Compression
        /// </summary>
        Compression = 0x0103,

        /// <summary>
        /// PhotometricInterpretation
        /// </summary>
        PhotometricInterpretation = 0x0106,

        /// <summary>
        /// Threshholding
        /// </summary>
        Threshholding = 0x0107,

        /// <summary>
        /// CellWidth
        /// </summary>
        CellWidth = 0x0108,

        /// <summary>
        /// CellLength
        /// </summary>
        CellLength = 0x0109,

        /// <summary>
        /// FillOrder
        /// </summary>
        FillOrder = 0x010A,

        /// <summary>
        /// ImageDescription
        /// </summary>
        ImageDescription = 0x010E,

        /// <summary>
        /// Make
        /// </summary>
        Make = 0x010F,

        /// <summary>
        /// Model
        /// </summary>
        Model = 0x0110,

        /// <summary>
        /// StripOffsets
        /// </summary>
        StripOffsets = 0x0111,

        /// <summary>
        /// Orientation
        /// </summary>
        Orientation = 0x0112,

        /// <summary>
        /// SamplesPerPixel
        /// </summary>
        SamplesPerPixel = 0x0115,

        /// <summary>
        /// RowsPerStrip
        /// </summary>
        RowsPerStrip = 0x0116,

        /// <summary>
        /// StripByteCounts
        /// </summary>
        StripByteCounts = 0x0117,

        /// <summary>
        /// MinSampleValue
        /// </summary>
        MinSampleValue = 0x0118,

        /// <summary>
        /// MaxSampleValue
        /// </summary>
        MaxSampleValue = 0x0119,

        /// <summary>
        /// XResolution
        /// </summary>
        XResolution = 0x011A,

        /// <summary>
        /// YResolution
        /// </summary>
        YResolution = 0x011B,

        /// <summary>
        /// PlanarConfiguration
        /// </summary>
        PlanarConfiguration = 0x011C,

        /// <summary>
        /// FreeOffsets
        /// </summary>
        FreeOffsets = 0x0120,

        /// <summary>
        /// FreeByteCounts
        /// </summary>
        FreeByteCounts = 0x0121,

        /// <summary>
        /// GrayResponseUnit
        /// </summary>
        GrayResponseUnit = 0x0122,

        /// <summary>
        /// GrayResponseCurve
        /// </summary>
        GrayResponseCurve = 0x0123,

        /// <summary>
        /// ResolutionUnit
        /// </summary>
        [ExifTagDescription((ushort)1, "None")]
        [ExifTagDescription((ushort)2, "Inches")]
        [ExifTagDescription((ushort)3, "Cm")]
        ResolutionUnit = 0x0128,

        /// <summary>
        /// Software
        /// </summary>
        Software = 0x0131,

        /// <summary>
        /// DateTime
        /// </summary>
        DateTime = 0x0132,

        /// <summary>
        /// Artist
        /// </summary>
        Artist = 0x013B,

        /// <summary>
        /// HostComputer
        /// </summary>
        HostComputer = 0x013C,

        /// <summary>
        /// ColorMap
        /// </summary>
        ColorMap = 0x0140,

        /// <summary>
        /// ExtraSamples
        /// </summary>
        ExtraSamples = 0x0152,

        /// <summary>
        /// Copyright
        /// </summary>
        Copyright = 0x8298,


        /// <summary>
        /// DocumentName
        /// </summary>
        DocumentName = 0x010D,

        /// <summary>
        /// PageName
        /// </summary>
        PageName = 0x011D,

        /// <summary>
        /// XPosition
        /// </summary>
        XPosition = 0x011E,

        /// <summary>
        /// YPosition
        /// </summary>
        YPosition = 0x011F,

        /// <summary>
        /// T4Options
        /// </summary>
        T4Options = 0x0124,

        /// <summary>
        /// T6Options
        /// </summary>
        T6Options = 0x0125,

        /// <summary>
        /// PageNumber
        /// </summary>
        PageNumber = 0x0129,

        /// <summary>
        /// TransferFunction
        /// </summary>
        TransferFunction = 0x012D,

        /// <summary>
        /// Predictor
        /// </summary>
        Predictor = 0x013D,

        /// <summary>
        /// WhitePoint
        /// </summary>
        WhitePoint = 0x013E,

        /// <summary>
        /// PrimaryChromaticities
        /// </summary>
        PrimaryChromaticities = 0x013F,

        /// <summary>
        /// HalftoneHints
        /// </summary>
        HalftoneHints = 0x0141,

        /// <summary>
        /// TileWidth
        /// </summary>
        TileWidth = 0x0142,

        /// <summary>
        /// TileLength
        /// </summary>
        TileLength = 0x0143,

        /// <summary>
        /// TileOffsets
        /// </summary>
        TileOffsets = 0x0144,

        /// <summary>
        /// TileByteCounts
        /// </summary>
        TileByteCounts = 0x0145,

        /// <summary>
        /// BadFaxLines
        /// </summary>
        BadFaxLines = 0x0146,

        /// <summary>
        /// CleanFaxData
        /// </summary>
        CleanFaxData = 0x0147,

        /// <summary>
        /// ConsecutiveBadFaxLines
        /// </summary>
        ConsecutiveBadFaxLines = 0x0148,

        /// <summary>
        /// InkSet
        /// </summary>
        InkSet = 0x014C,

        /// <summary>
        /// InkNames
        /// </summary>
        InkNames = 0x014D,

        /// <summary>
        /// NumberOfInks
        /// </summary>
        NumberOfInks = 0x014E,

        /// <summary>
        /// DotRange
        /// </summary>
        DotRange = 0x0150,

        /// <summary>
        /// TargetPrinter
        /// </summary>
        TargetPrinter = 0x0151,

        /// <summary>
        /// SampleFormat
        /// </summary>
        SampleFormat = 0x0153,

        /// <summary>
        /// SMinSampleValue
        /// </summary>
        SMinSampleValue = 0x0154,

        /// <summary>
        /// SMaxSampleValue
        /// </summary>
        SMaxSampleValue = 0x0155,

        /// <summary>
        /// TransferRange
        /// </summary>
        TransferRange = 0x0156,

        /// <summary>
        /// ClipPath
        /// </summary>
        ClipPath = 0x0157,

        /// <summary>
        /// XClipPathUnits
        /// </summary>
        XClipPathUnits = 0x0158,

        /// <summary>
        /// YClipPathUnits
        /// </summary>
        YClipPathUnits = 0x0159,

        /// <summary>
        /// Indexed
        /// </summary>
        Indexed = 0x015A,

        /// <summary>
        /// JPEGTables
        /// </summary>
        JPEGTables = 0x015B,

        /// <summary>
        /// OPIProxy
        /// </summary>
        OPIProxy = 0x015F,

        /// <summary>
        /// ProfileType
        /// </summary>
        ProfileType = 0x0191,

        /// <summary>
        /// FaxProfile
        /// </summary>
        FaxProfile = 0x0192,

        /// <summary>
        /// CodingMethods
        /// </summary>
        CodingMethods = 0x0193,

        /// <summary>
        /// VersionYear
        /// </summary>
        VersionYear = 0x0194,

        /// <summary>
        /// ModeNumber
        /// </summary>
        ModeNumber = 0x0195,

        /// <summary>
        /// Decode
        /// </summary>
        Decode = 0x01B1,

        /// <summary>
        /// DefaultImageColor
        /// </summary>
        DefaultImageColor = 0x01B2,

        /// <summary>
        /// JPEGProc
        /// </summary>
        JPEGProc = 0x0200,

        /// <summary>
        /// JPEGInterchangeFormat
        /// </summary>
        JPEGInterchangeFormat = 0x0201,

        /// <summary>
        /// JPEGInterchangeFormatLength
        /// </summary>
        JPEGInterchangeFormatLength = 0x0202,

        /// <summary>
        /// JPEGRestartInterval
        /// </summary>
        JPEGRestartInterval = 0x0203,

        /// <summary>
        /// JPEGLosslessPredictors
        /// </summary>
        JPEGLosslessPredictors = 0x0205,

        /// <summary>
        /// JPEGPointTransforms
        /// </summary>
        JPEGPointTransforms = 0x0206,

        /// <summary>
        /// JPEGQTables
        /// </summary>
        JPEGQTables = 0x0207,

        /// <summary>
        /// JPEGDCTables
        /// </summary>
        JPEGDCTables = 0x0208,

        /// <summary>
        /// JPEGACTables
        /// </summary>
        JPEGACTables = 0x0209,

        /// <summary>
        /// YCbCrCoefficients
        /// </summary>
        YCbCrCoefficients = 0x0211,

        /// <summary>
        /// YCbCrSubsampling
        /// </summary>
        YCbCrSubsampling = 0x0212,

        /// <summary>
        /// YCbCrPositioning
        /// </summary>
        YCbCrPositioning = 0x0213,

        /// <summary>
        /// ReferenceBlackWhite
        /// </summary>
        ReferenceBlackWhite = 0x0214,

        /// <summary>
        /// StripRowCounts
        /// </summary>
        StripRowCounts = 0x022F,

        /// <summary>
        /// XMP
        /// </summary>
        XMP = 0x02BC,

        /// <summary>
        /// ImageID
        /// </summary>
        ImageID = 0x800D,

        /// <summary>
        /// ImageLayer
        /// </summary>
        ImageLayer = 0x87AC,


        /// <summary>
        /// ExposureTime
        /// </summary>
        ExposureTime = 0x829A,

        /// <summary>
        /// FNumber
        /// </summary>
        FNumber = 0x829D,

        /// <summary>
        /// ExposureProgram
        /// </summary>
        ExposureProgram = 0x8822,

        /// <summary>
        /// SpectralSensitivity
        /// </summary>
        SpectralSensitivity = 0x8824,

        /// <summary>
        /// ISOSpeedRatings
        /// </summary>
        ISOSpeedRatings = 0x8827,

        /// <summary>
        /// OECF
        /// </summary>
        OECF = 0x8828,

        /// <summary>
        /// ExifVersion
        /// </summary>
        ExifVersion = 0x9000,

        /// <summary>
        /// DateTimeOriginal
        /// </summary>
        DateTimeOriginal = 0x9003,

        /// <summary>
        /// DateTimeDigitized
        /// </summary>
        DateTimeDigitized = 0x9004,

        /// <summary>
        /// ComponentsConfiguration
        /// </summary>
        ComponentsConfiguration = 0x9101,

        /// <summary>
        /// CompressedBitsPerPixel
        /// </summary>
        CompressedBitsPerPixel = 0x9102,

        /// <summary>
        /// ShutterSpeedValue
        /// </summary>
        ShutterSpeedValue = 0x9201,

        /// <summary>
        /// ApertureValue
        /// </summary>
        ApertureValue = 0x9202,

        /// <summary>
        /// BrightnessValue
        /// </summary>
        BrightnessValue = 0x9203,

        /// <summary>
        /// ExposureBiasValue
        /// </summary>
        ExposureBiasValue = 0x9204,

        /// <summary>
        /// MaxApertureValue
        /// </summary>
        MaxApertureValue = 0x9205,

        /// <summary>
        /// SubjectDistance
        /// </summary>
        SubjectDistance = 0x9206,

        /// <summary>
        /// MeteringMode
        /// </summary>
        MeteringMode = 0x9207,

        /// <summary>
        /// LightSource
        /// </summary>
        LightSource = 0x9208,

        /// <summary>
        /// Flash
        /// </summary>
        Flash = 0x9209,

        /// <summary>
        /// FocalLength
        /// </summary>
        FocalLength = 0x920A,

        /// <summary>
        /// SubjectArea
        /// </summary>
        SubjectArea = 0x9214,

        /// <summary>
        /// MakerNote
        /// </summary>
        MakerNote = 0x927C,

        /// <summary>
        /// UserComment
        /// </summary>
        UserComment = 0x9286,

        /// <summary>
        /// SubsecTime
        /// </summary>
        SubsecTime = 0x9290,

        /// <summary>
        /// SubsecTimeOriginal
        /// </summary>
        SubsecTimeOriginal = 0x9291,

        /// <summary>
        /// SubsecTimeDigitized
        /// </summary>
        SubsecTimeDigitized = 0x9292,

        /// <summary>
        /// FlashpixVersion
        /// </summary>
        FlashpixVersion = 0xA000,

        /// <summary>
        /// ColorSpace
        /// </summary>
        ColorSpace = 0xA001,

        /// <summary>
        /// PixelXDimension
        /// </summary>
        PixelXDimension = 0xA002,

        /// <summary>
        /// PixelYDimension
        /// </summary>
        PixelYDimension = 0xA003,

        /// <summary>
        /// RelatedSoundFile
        /// </summary>
        RelatedSoundFile = 0xA004,

        /// <summary>
        /// FlashEnergy
        /// </summary>
        FlashEnergy = 0xA20B,

        /// <summary>
        /// SpatialFrequencyResponse
        /// </summary>
        SpatialFrequencyResponse = 0xA20C,

        /// <summary>
        /// FocalPlaneXResolution
        /// </summary>
        FocalPlaneXResolution = 0xA20E,

        /// <summary>
        /// FocalPlaneYResolution
        /// </summary>
        FocalPlaneYResolution = 0xA20F,

        /// <summary>
        /// FocalPlaneResolutionUnit
        /// </summary>
        FocalPlaneResolutionUnit = 0xA210,

        /// <summary>
        /// SubjectLocation
        /// </summary>
        SubjectLocation = 0xA214,

        /// <summary>
        /// ExposureIndex
        /// </summary>
        ExposureIndex = 0xA215,

        /// <summary>
        /// SensingMethod
        /// </summary>
        SensingMethod = 0xA217,

        /// <summary>
        /// FileSource
        /// </summary>
        FileSource = 0xA300,

        /// <summary>
        /// SceneType
        /// </summary>
        SceneType = 0xA301,

        /// <summary>
        /// CFAPattern
        /// </summary>
        CFAPattern = 0xA302,

        /// <summary>
        /// CustomRendered
        /// </summary>
        CustomRendered = 0xA401,

        /// <summary>
        /// ExposureMode
        /// </summary>
        ExposureMode = 0xA402,

        /// <summary>
        /// WhiteBalance
        /// </summary>
        WhiteBalance = 0xA403,

        /// <summary>
        /// DigitalZoomRatio
        /// </summary>
        DigitalZoomRatio = 0xA404,

        /// <summary>
        /// FocalLengthIn35mmFilm
        /// </summary>
        FocalLengthIn35mmFilm = 0xA405,

        /// <summary>
        /// SceneCaptureType
        /// </summary>
        SceneCaptureType = 0xA406,

        /// <summary>
        /// GainControl
        /// </summary>
        GainControl = 0xA407,

        /// <summary>
        /// Contrast
        /// </summary>
        Contrast = 0xA408,

        /// <summary>
        /// Saturation
        /// </summary>
        Saturation = 0xA409,

        /// <summary>
        /// Sharpness
        /// </summary>
        Sharpness = 0xA40A,

        /// <summary>
        /// DeviceSettingDescription
        /// </summary>
        DeviceSettingDescription = 0xA40B,

        /// <summary>
        /// SubjectDistanceRange
        /// </summary>
        SubjectDistanceRange = 0xA40C,

        /// <summary>
        /// ImageUniqueID
        /// </summary>
        ImageUniqueID = 0xA420,


        /// <summary>
        /// GPSVersionID
        /// </summary>
        GPSVersionID = 0x0000,

        /// <summary>
        /// GPSLatitudeRef
        /// </summary>
        GPSLatitudeRef = 0x0001,

        /// <summary>
        /// GPSLatitude
        /// </summary>
        GPSLatitude = 0x0002,

        /// <summary>
        /// GPSLongitudeRef
        /// </summary>
        GPSLongitudeRef = 0x0003,

        /// <summary>
        /// GPSLongitude
        /// </summary>
        GPSLongitude = 0x0004,

        /// <summary>
        /// GPSAltitudeRef
        /// </summary>
        GPSAltitudeRef = 0x0005,

        /// <summary>
        /// GPSAltitude
        /// </summary>
        GPSAltitude = 0x0006,

        /// <summary>
        /// GPSTimestamp
        /// </summary>
        GPSTimestamp = 0x0007,

        /// <summary>
        /// GPSSatellites
        /// </summary>
        GPSSatellites = 0x0008,

        /// <summary>
        /// GPSStatus
        /// </summary>
        GPSStatus = 0x0009,

        /// <summary>
        /// GPSMeasureMode
        /// </summary>
        GPSMeasureMode = 0x000A,

        /// <summary>
        /// GPSDOP
        /// </summary>
        GPSDOP = 0x000B,

        /// <summary>
        /// GPSSpeedRef
        /// </summary>
        GPSSpeedRef = 0x000C,

        /// <summary>
        /// GPSSpeed
        /// </summary>
        GPSSpeed = 0x000D,

        /// <summary>
        /// GPSTrackRef
        /// </summary>
        GPSTrackRef = 0x000E,

        /// <summary>
        /// GPSTrack
        /// </summary>
        GPSTrack = 0x000F,

        /// <summary>
        /// GPSImgDirectionRef
        /// </summary>
        GPSImgDirectionRef = 0x0010,

        /// <summary>
        /// GPSImgDirection
        /// </summary>
        GPSImgDirection = 0x0011,

        /// <summary>
        /// GPSMapDatum
        /// </summary>
        GPSMapDatum = 0x0012,

        /// <summary>
        /// GPSDestLatitudeRef
        /// </summary>
        GPSDestLatitudeRef = 0x0013,

        /// <summary>
        /// GPSDestLatitude
        /// </summary>
        GPSDestLatitude = 0x0014,

        /// <summary>
        /// GPSDestLongitudeRef
        /// </summary>
        GPSDestLongitudeRef = 0x0015,

        /// <summary>
        /// GPSDestLongitude
        /// </summary>
        GPSDestLongitude = 0x0016,

        /// <summary>
        /// GPSDestBearingRef
        /// </summary>
        GPSDestBearingRef = 0x0017,

        /// <summary>
        /// GPSDestBearing
        /// </summary>
        GPSDestBearing = 0x0018,

        /// <summary>
        /// GPSDestDistanceRef
        /// </summary>
        GPSDestDistanceRef = 0x0019,

        /// <summary>
        /// GPSDestDistance
        /// </summary>
        GPSDestDistance = 0x001A,

        /// <summary>
        /// GPSProcessingMethod
        /// </summary>
        GPSProcessingMethod = 0x001B,

        /// <summary>
        /// GPSAreaInformation
        /// </summary>
        GPSAreaInformation = 0x001C,

        /// <summary>
        /// GPSDateStamp
        /// </summary>
        GPSDateStamp = 0x001D,

        /// <summary>
        /// GPSDifferential
        /// </summary>
        GPSDifferential = 0x001E
    }
}
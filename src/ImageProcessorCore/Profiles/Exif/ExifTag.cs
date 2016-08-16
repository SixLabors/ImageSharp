// <copyright file="ExifTag.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// Descriptions from: http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/EXIF.html

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
        [ExifTagDescription((ushort)1, "Uncompressed")]
        [ExifTagDescription((ushort)2, "CCITT 1D")]
        [ExifTagDescription((ushort)3, "T4/Group 3 Fax")]
        [ExifTagDescription((ushort)4, "T6/Group 4 Fax")]
        [ExifTagDescription((ushort)5, "LZW")]
        [ExifTagDescription((ushort)6, "JPEG (old-style)")]
        [ExifTagDescription((ushort)7, "JPEG")]
        [ExifTagDescription((ushort)8, "Adobe Deflate")]
        [ExifTagDescription((ushort)9, "JBIG B&W")]
        [ExifTagDescription((ushort)10, "JBIG Color")]
        [ExifTagDescription((ushort)99, "JPEG")]
        [ExifTagDescription((ushort)262, "Kodak 262")]
        [ExifTagDescription((ushort)32766, "Next")]
        [ExifTagDescription((ushort)32767, "Sony ARW Compressed")]
        [ExifTagDescription((ushort)32769, "Packed RAW")]
        [ExifTagDescription((ushort)32770, "Samsung SRW Compressed")]
        [ExifTagDescription((ushort)32771, "CCIRLEW")]
        [ExifTagDescription((ushort)32772, "Samsung SRW Compressed 2")]
        [ExifTagDescription((ushort)32773, "PackBits")]
        [ExifTagDescription((ushort)32809, "Thunderscan")]
        [ExifTagDescription((ushort)32867, "Kodak KDC Compressed")]
        [ExifTagDescription((ushort)32895, "IT8CTPAD")]
        [ExifTagDescription((ushort)32896, "IT8LW")]
        [ExifTagDescription((ushort)32897, "IT8MP")]
        [ExifTagDescription((ushort)32898, "IT8BL")]
        [ExifTagDescription((ushort)32908, "PixarFilm")]
        [ExifTagDescription((ushort)32909, "PixarLog")]
        [ExifTagDescription((ushort)32946, "Deflate")]
        [ExifTagDescription((ushort)32947, "DCS")]
        [ExifTagDescription((ushort)34661, "JBIG")]
        [ExifTagDescription((ushort)34676, "SGILog")]
        [ExifTagDescription((ushort)34677, "SGILog24")]
        [ExifTagDescription((ushort)34712, "JPEG 2000")]
        [ExifTagDescription((ushort)34713, "Nikon NEF Compressed")]
        [ExifTagDescription((ushort)34715, "JBIG2 TIFF FX")]
        [ExifTagDescription((ushort)34718, "Microsoft Document Imaging (MDI) Binary Level Codec")]
        [ExifTagDescription((ushort)34719, "Microsoft Document Imaging (MDI) Progressive Transform Codec")]
        [ExifTagDescription((ushort)34720, "Microsoft Document Imaging (MDI) Vector")]
        [ExifTagDescription((ushort)34892, "Lossy JPEG")]
        [ExifTagDescription((ushort)65000, "Kodak DCR Compressed")]
        [ExifTagDescription((ushort)65535, "Pentax PEF Compressed")]
        Compression = 0x0103,

        /// <summary>
        /// PhotometricInterpretation
        /// </summary>
        [ExifTagDescription((ushort)0, "WhiteIsZero")]
        [ExifTagDescription((ushort)1, "BlackIsZero")]
        [ExifTagDescription((ushort)2, "RGB")]
        [ExifTagDescription((ushort)3, "RGB Palette")]
        [ExifTagDescription((ushort)4, "Transparency Mask")]
        [ExifTagDescription((ushort)5, "CMYK")]
        [ExifTagDescription((ushort)6, "YCbCr")]
        [ExifTagDescription((ushort)8, "CIELab")]
        [ExifTagDescription((ushort)9, "ICCLab")]
        [ExifTagDescription((ushort)10, "TULab")]
        [ExifTagDescription((ushort)32803, "Color Filter Array")]
        [ExifTagDescription((ushort)32844, "Pixar LogL")]
        [ExifTagDescription((ushort)32845, "Pixar LogLuv")]
        [ExifTagDescription((ushort)34892, "Linear Raw")]
        PhotometricInterpretation = 0x0106,

        /// <summary>
        /// Thresholding
        /// </summary>
        [ExifTagDescription((ushort)1, "No dithering or halftoning")]
        [ExifTagDescription((ushort)2, "Ordered dither or halftone")]
        [ExifTagDescription((ushort)3, "Randomized dither")]
        Thresholding = 0x0107,

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
        [ExifTagDescription((ushort)1, "Normal")]
        [ExifTagDescription((ushort)2, "Reversed")]
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
        [ExifTagDescription((ushort)1, "Horizontal (normal)")]
        [ExifTagDescription((ushort)2, "Mirror horizontal")]
        [ExifTagDescription((ushort)3, "Rotate 180")]
        [ExifTagDescription((ushort)4, "Mirror vertical")]
        [ExifTagDescription((ushort)5, "Mirror horizontal and rotate 270 CW")]
        [ExifTagDescription((ushort)6, "Rotate 90 CW")]
        [ExifTagDescription((ushort)7, "Mirror horizontal and rotate 90 CW")]
        [ExifTagDescription((ushort)8, "Rotate 270 CW")]
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
        [ExifTagDescription((ushort)1, "Chunky")]
        [ExifTagDescription((ushort)2, "Planar")]
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
        [ExifTagDescription((ushort)1, "0.1")]
        [ExifTagDescription((ushort)2, "0.001")]
        [ExifTagDescription((ushort)3, "0.0001")]
        [ExifTagDescription((ushort)4, "1e-05")]
        [ExifTagDescription((ushort)5, "1e-06")]
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
        [ExifTagDescription((ushort)3, "Centimeter")]
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
        [ExifTagDescription((uint)0, "2-Dimensional encoding")]
        [ExifTagDescription((uint)1, "Uncompressed")]
        [ExifTagDescription((uint)2, "Fill bits added")]
        T4Options = 0x0124,

        /// <summary>
        /// T6Options
        /// </summary>
        [ExifTagDescription((uint)1, "Uncompressed")]
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
        [ExifTagDescription((uint)0, "Clean")]
        [ExifTagDescription((uint)1, "Regenerated")]
        [ExifTagDescription((uint)2, "Unclean")]
        CleanFaxData = 0x0147,

        /// <summary>
        /// ConsecutiveBadFaxLines
        /// </summary>
        ConsecutiveBadFaxLines = 0x0148,

        /// <summary>
        /// InkSet
        /// </summary>
        [ExifTagDescription((ushort)1, "CMYK")]
        [ExifTagDescription((ushort)2, "Not CMYK")]
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
        [ExifTagDescription((ushort)1, "Unsigned")]
        [ExifTagDescription((ushort)2, "Signed")]
        [ExifTagDescription((ushort)3, "Float")]
        [ExifTagDescription((ushort)4, "Undefined")]
        [ExifTagDescription((ushort)5, "Complex")]
        [ExifTagDescription((ushort)6, "Complex")]
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
        [ExifTagDescription((ushort)0, "Not indexed")]
        [ExifTagDescription((ushort)1, "Indexed")]
        Indexed = 0x015A,

        /// <summary>
        /// JPEGTables
        /// </summary>
        JPEGTables = 0x015B,

        /// <summary>
        /// OPIProxy
        /// </summary>
        [ExifTagDescription((ushort)0, "Higher resolution image does not exist")]
        [ExifTagDescription((ushort)1, "Higher resolution image exists")]
        OPIProxy = 0x015F,

        /// <summary>
        /// ProfileType
        /// </summary>
        [ExifTagDescription((uint)0, "Unspecified")]
        [ExifTagDescription((uint)1, "Group 3 FAX")]
        ProfileType = 0x0191,

        /// <summary>
        /// FaxProfile
        /// </summary>
        [ExifTagDescription((byte)0, "Unknown")]
        [ExifTagDescription((byte)1, "Minimal B&W lossless, S")]
        [ExifTagDescription((byte)2, "Extended B&W lossless, F")]
        [ExifTagDescription((byte)3, "Lossless JBIG B&W, J")]
        [ExifTagDescription((byte)4, "Lossy color and grayscale, C")]
        [ExifTagDescription((byte)5, "Lossless color and grayscale, L")]
        [ExifTagDescription((byte)6, "Mixed raster content, M")]
        [ExifTagDescription((byte)7, "Profile T")]
        [ExifTagDescription((byte)255, "Multi Profiles")]
        FaxProfile = 0x0192,

        /// <summary>
        /// CodingMethods
        /// </summary>
        [ExifTagDescription((ulong)0, "Unspecified compression")]
        [ExifTagDescription((ulong)1, "Modified Huffman")]
        [ExifTagDescription((ulong)2, "Modified Read")]
        [ExifTagDescription((ulong)4, "Modified MR")]
        [ExifTagDescription((ulong)8, "JBIG")]
        [ExifTagDescription((ulong)16, "Baseline JPEG")]
        [ExifTagDescription((ulong)32, "JBIG color")]
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
        [ExifTagDescription((ushort)1, "Baseline")]
        [ExifTagDescription((ushort)14, "Lossless")]
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
        [ExifTagDescription((ushort)1, "Centered")]
        [ExifTagDescription((ushort)2, "Co-sited")]
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
        [ExifTagDescription((ushort)0, "Not Defined")]
        [ExifTagDescription((ushort)1, "Manual")]
        [ExifTagDescription((ushort)2, "Program AE")]
        [ExifTagDescription((ushort)3, "Aperture-priority AE")]
        [ExifTagDescription((ushort)4, "Shutter speed priority AE")]
        [ExifTagDescription((ushort)5, "Creative (Slow speed)")]
        [ExifTagDescription((ushort)6, "Action (High speed)")]
        [ExifTagDescription((ushort)7, "Portrait")]
        [ExifTagDescription((ushort)8, "Landscape")]
        [ExifTagDescription((ushort)9, "Bulb")]
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
        [ExifTagDescription((ushort)0, "Unknown")]
        [ExifTagDescription((ushort)1, "Average")]
        [ExifTagDescription((ushort)2, "Center-weighted average")]
        [ExifTagDescription((ushort)3, "Spot")]
        [ExifTagDescription((ushort)4, "Multi-spot")]
        [ExifTagDescription((ushort)5, "Multi-segment")]
        [ExifTagDescription((ushort)6, "Partial")]
        [ExifTagDescription((ushort)255, "Other")]
        MeteringMode = 0x9207,

        /// <summary>
        /// LightSource
        /// </summary>
        [ExifTagDescription((ushort)0, "Unknown")]
        [ExifTagDescription((ushort)1, "Daylight")]
        [ExifTagDescription((ushort)2, "Fluorescent")]
        [ExifTagDescription((ushort)3, "Tungsten (Incandescent)")]
        [ExifTagDescription((ushort)4, "Flash")]
        [ExifTagDescription((ushort)9, "Fine Weather")]
        [ExifTagDescription((ushort)10, "Cloudy")]
        [ExifTagDescription((ushort)11, "Shade")]
        [ExifTagDescription((ushort)12, "Daylight Fluorescent")]
        [ExifTagDescription((ushort)13, "Day White Fluorescent")]
        [ExifTagDescription((ushort)14, "Cool White Fluorescent")]
        [ExifTagDescription((ushort)15, "White Fluorescent")]
        [ExifTagDescription((ushort)16, "Warm White Fluorescent")]
        [ExifTagDescription((ushort)17, "Standard Light A")]
        [ExifTagDescription((ushort)18, "Standard Light B")]
        [ExifTagDescription((ushort)19, "Standard Light C")]
        [ExifTagDescription((ushort)20, "D55")]
        [ExifTagDescription((ushort)21, "D65")]
        [ExifTagDescription((ushort)22, "D75")]
        [ExifTagDescription((ushort)23, "D50")]
        [ExifTagDescription((ushort)24, "ISO Studio Tungsten")]
        [ExifTagDescription((ushort)255, "Other")]
        LightSource = 0x9208,

        /// <summary>
        /// Flash
        /// </summary>
        [ExifTagDescription((ushort)0, "No Flash")]
        [ExifTagDescription((ushort)1, "Fired")]
        [ExifTagDescription((ushort)5, "Fired, Return not detected")]
        [ExifTagDescription((ushort)7, "Fired, Return detected")]
        [ExifTagDescription((ushort)8, "On, Did not fire")]
        [ExifTagDescription((ushort)9, "On, Fired")]
        [ExifTagDescription((ushort)13, "On, Return not detected")]
        [ExifTagDescription((ushort)15, "On, Return detected")]
        [ExifTagDescription((ushort)16, "Off, Did not fire")]
        [ExifTagDescription((ushort)20, "Off, Did not fire, Return not detected")]
        [ExifTagDescription((ushort)24, "Auto, Did not fire")]
        [ExifTagDescription((ushort)25, "Auto, Fired")]
        [ExifTagDescription((ushort)29, "Auto, Fired, Return not detected")]
        [ExifTagDescription((ushort)31, "Auto, Fired, Return detected")]
        [ExifTagDescription((ushort)32, "No flash function")]
        [ExifTagDescription((ushort)48, "Off, No flash function")]
        [ExifTagDescription((ushort)65, "Fired, Red-eye reduction")]
        [ExifTagDescription((ushort)69, "Fired, Red-eye reduction, Return not detected")]
        [ExifTagDescription((ushort)71, "Fired, Red-eye reduction, Return detected")]
        [ExifTagDescription((ushort)73, "On, Red-eye reduction")]
        [ExifTagDescription((ushort)77, "On, Red-eye reduction, Return not detected")]
        [ExifTagDescription((ushort)69, "On, Red-eye reduction, Return detected")]
        [ExifTagDescription((ushort)80, "Off, Red-eye reduction")]
        [ExifTagDescription((ushort)88, "Auto, Did not fire, Red-eye reduction")]
        [ExifTagDescription((ushort)89, "Auto, Fired, Red-eye reduction")]
        [ExifTagDescription((ushort)93, "Auto, Fired, Red-eye reduction, Return not detected")]
        [ExifTagDescription((ushort)95, "Auto, Fired, Red-eye reduction, Return detected")]
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
        [ExifTagDescription((ushort)1, "sRGB")]
        [ExifTagDescription((ushort)2, "Adobe RGB")]
        [ExifTagDescription((ushort)4093, "Wide Gamut RGB")]
        [ExifTagDescription((ushort)65534, "ICC Profile")]
        [ExifTagDescription((ushort)65535, "Uncalibrated")]
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
        [ExifTagDescription((ushort)1, "None")]
        [ExifTagDescription((ushort)2, "Inches")]
        [ExifTagDescription((ushort)3, "Centimeter")]
        [ExifTagDescription((ushort)4, "Millimeter")]
        [ExifTagDescription((ushort)5, "Micrometer")]
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
        [ExifTagDescription((ushort)1, "Not defined")]
        [ExifTagDescription((ushort)2, "One-chip color area")]
        [ExifTagDescription((ushort)3, "Two-chip color area")]
        [ExifTagDescription((ushort)4, "Three-chip color area")]
        [ExifTagDescription((ushort)5, "Color sequential area")]
        [ExifTagDescription((ushort)7, "Trilinear")]
        [ExifTagDescription((ushort)8, "Color sequential linear")]
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
        [ExifTagDescription((ushort)1, "Normal")]
        [ExifTagDescription((ushort)2, "Custom")]
        CustomRendered = 0xA401,

        /// <summary>
        /// ExposureMode
        /// </summary>
        [ExifTagDescription((ushort)0, "Auto")]
        [ExifTagDescription((ushort)1, "Manual")]
        [ExifTagDescription((ushort)2, "Auto bracket")]
        ExposureMode = 0xA402,

        /// <summary>
        /// WhiteBalance
        /// </summary>
        [ExifTagDescription((ushort)0, "Auto")]
        [ExifTagDescription((ushort)1, "Manual")]
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
        [ExifTagDescription((ushort)0, "Standard")]
        [ExifTagDescription((ushort)1, "Landscape")]
        [ExifTagDescription((ushort)2, "Portrait")]
        [ExifTagDescription((ushort)3, "Night")]
        SceneCaptureType = 0xA406,

        /// <summary>
        /// GainControl
        /// </summary>
        [ExifTagDescription((ushort)0, "None")]
        [ExifTagDescription((ushort)1, "Low gain up")]
        [ExifTagDescription((ushort)2, "High gain up")]
        [ExifTagDescription((ushort)3, "Low gain down")]
        [ExifTagDescription((ushort)4, "High gain down")]
        GainControl = 0xA407,

        /// <summary>
        /// Contrast
        /// </summary>
        [ExifTagDescription((ushort)0, "Normal")]
        [ExifTagDescription((ushort)1, "Low")]
        [ExifTagDescription((ushort)2, "High")]
        Contrast = 0xA408,

        /// <summary>
        /// Saturation
        /// </summary>
        [ExifTagDescription((ushort)0, "Normal")]
        [ExifTagDescription((ushort)1, "Low")]
        [ExifTagDescription((ushort)2, "High")]
        Saturation = 0xA409,

        /// <summary>
        /// Sharpness
        /// </summary>
        [ExifTagDescription((ushort)0, "Normal")]
        [ExifTagDescription((ushort)1, "Soft")]
        [ExifTagDescription((ushort)2, "Hard")]
        Sharpness = 0xA40A,

        /// <summary>
        /// DeviceSettingDescription
        /// </summary>
        DeviceSettingDescription = 0xA40B,

        /// <summary>
        /// SubjectDistanceRange
        /// </summary>
        [ExifTagDescription((ushort)0, "Unknown")]
        [ExifTagDescription((ushort)1, "Macro")]
        [ExifTagDescription((ushort)2, "Close")]
        [ExifTagDescription((ushort)3, "Distant")]
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
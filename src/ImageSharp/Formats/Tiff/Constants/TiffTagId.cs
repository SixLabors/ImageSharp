// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Constants representing tag IDs in the Tiff file-format.
    /// todo: join with <see cref="SixLabors.ImageSharp.MetaData.Profiles.Exif.ExifTag"/>
    /// </summary>
    internal enum TiffTagId : int
    {
        /// <summary>
        /// Artist (see Section 8: Baseline Fields).
        /// </summary>
        Artist = 315,

        /// <summary>
        /// BitsPerSample (see Section 8: Baseline Fields).
        /// </summary>
        BitsPerSample = 258,

        /// <summary>
        /// CellLength (see Section 8: Baseline Fields).
        /// </summary>
        CellLength = 265,

        /// <summary>
        /// CellWidth (see Section 8: Baseline Fields).
        /// </summary>
        CellWidth = 264,

        /// <summary>
        /// ColorMap (see Section 8: Baseline Fields).
        /// </summary>
        ColorMap = 320,

        /// <summary>
        /// Compression (see Section 8: Baseline Fields).
        /// </summary>
        Compression = 259,

        /// <summary>
        /// Copyright (see Section 8: Baseline Fields).
        /// </summary>
        Copyright = 33432,

        /// <summary>
        /// DateTime (see Section 8: Baseline Fields).
        /// </summary>
        DateTime = 306,

        /// <summary>
        /// ExtraSamples (see Section 8: Baseline Fields).
        /// </summary>
        ExtraSamples = 338,

        /// <summary>
        /// FillOrder (see Section 8: Baseline Fields).
        /// </summary>
        FillOrder = 266,

        /// <summary>
        /// FreeByteCounts (see Section 8: Baseline Fields).
        /// </summary>
        FreeByteCounts = 289,

        /// <summary>
        /// FreeOffsets (see Section 8: Baseline Fields).
        /// </summary>
        FreeOffsets = 288,

        /// <summary>
        /// GrayResponseCurve (see Section 8: Baseline Fields).
        /// </summary>
        GrayResponseCurve = 291,

        /// <summary>
        /// GrayResponseUnit (see Section 8: Baseline Fields).
        /// </summary>
        GrayResponseUnit = 290,

        /// <summary>
        /// HostComputer (see Section 8: Baseline Fields).
        /// </summary>
        HostComputer = 316,

        /// <summary>
        /// ImageDescription (see Section 8: Baseline Fields).
        /// </summary>
        ImageDescription = 270,

        /// <summary>
        /// ImageLength (see Section 8: Baseline Fields).
        /// </summary>
        ImageLength = 257,

        /// <summary>
        /// ImageWidth (see Section 8: Baseline Fields).
        /// </summary>
        ImageWidth = 256,

        /// <summary>
        /// Make (see Section 8: Baseline Fields).
        /// </summary>
        Make = 271,

        /// <summary>
        /// MaxSampleValue (see Section 8: Baseline Fields).
        /// </summary>
        MaxSampleValue = 281,

        /// <summary>
        /// MinSampleValue (see Section 8: Baseline Fields).
        /// </summary>
        MinSampleValue = 280,

        /// <summary>
        /// Model (see Section 8: Baseline Fields).
        /// </summary>
        Model = 272,

        /// <summary>
        /// NewSubfileType (see Section 8: Baseline Fields).
        /// </summary>
        NewSubfileType = 254,

        /// <summary>
        /// Orientation (see Section 8: Baseline Fields).
        /// </summary>
        Orientation = 274,

        /// <summary>
        /// PhotometricInterpretation (see Section 8: Baseline Fields).
        /// </summary>
        PhotometricInterpretation = 262,

        /// <summary>
        /// PlanarConfiguration (see Section 8: Baseline Fields).
        /// </summary>
        PlanarConfiguration = 284,

        /// <summary>
        /// ResolutionUnit (see Section 8: Baseline Fields).
        /// </summary>
        ResolutionUnit = 296,

        /// <summary>
        /// RowsPerStrip (see Section 8: Baseline Fields).
        /// </summary>
        RowsPerStrip = 278,

        /// <summary>
        /// SamplesPerPixel (see Section 8: Baseline Fields).
        /// </summary>
        SamplesPerPixel = 277,

        /// <summary>
        /// Software (see Section 8: Baseline Fields).
        /// </summary>
        Software = 305,

        /// <summary>
        /// StripByteCounts (see Section 8: Baseline Fields).
        /// </summary>
        StripByteCounts = 279,

        /// <summary>
        /// StripOffsets (see Section 8: Baseline Fields).
        /// </summary>
        StripOffsets = 273,

        /// <summary>
        /// SubfileType (see Section 8: Baseline Fields).
        /// </summary>
        SubfileType = 255,

        /// <summary>
        /// Threshholding (see Section 8: Baseline Fields).
        /// </summary>
        Threshholding = 263,

        /// <summary>
        /// XResolution (see Section 8: Baseline Fields).
        /// </summary>
        XResolution = 282,

        /// <summary>
        /// YResolution (see Section 8: Baseline Fields).
        /// </summary>
        YResolution = 283,

        /// <summary>
        /// T4Options (see Section 11: CCITT Bilevel Encodings).
        /// </summary>
        T4Options = 292,

        /// <summary>
        /// T6Options (see Section 11: CCITT Bilevel Encodings).
        /// </summary>
        T6Options = 293,

        /// <summary>
        /// DocumentName (see Section 12: Document Storage and Retrieval).
        /// </summary>
        DocumentName = 269,

        /// <summary>
        /// PageName (see Section 12: Document Storage and Retrieval).
        /// </summary>
        PageName = 285,

        /// <summary>
        /// PageNumber (see Section 12: Document Storage and Retrieval).
        /// </summary>
        PageNumber = 297,

        /// <summary>
        /// XPosition (see Section 12: Document Storage and Retrieval).
        /// </summary>
        XPosition = 286,

        /// <summary>
        /// YPosition (see Section 12: Document Storage and Retrieval).
        /// </summary>
        YPosition = 287,

        /// <summary>
        /// Predictor (see Section 14: Differencing Predictor).
        /// </summary>
        Predictor = 317,

        /// <summary>
        /// TileWidth (see Section 15: Tiled Images).
        /// </summary>
        TileWidth = 322,

        /// <summary>
        /// TileLength (see Section 15: Tiled Images).
        /// </summary>
        TileLength = 323,

        /// <summary>
        /// TileOffsets (see Section 15: Tiled Images).
        /// </summary>
        TileOffsets = 324,

        /// <summary>
        /// TileByteCounts (see Section 15: Tiled Images).
        /// </summary>
        TileByteCounts = 325,

        /// <summary>
        /// InkSet (see Section 16: CMYK Images).
        /// </summary>
        InkSet = 332,

        /// <summary>
        /// NumberOfInks (see Section 16: CMYK Images).
        /// </summary>
        NumberOfInks = 334,

        /// <summary>
        /// InkNames (see Section 16: CMYK Images).
        /// </summary>
        InkNames = 333,

        /// <summary>
        /// DotRange (see Section 16: CMYK Images).
        /// </summary>
        DotRange = 336,

        /// <summary>
        /// TargetPrinter (see Section 16: CMYK Images).
        /// </summary>
        TargetPrinter = 337,

        /// <summary>
        /// HalftoneHints (see Section 17: Halftone Hints).
        /// </summary>
        HalftoneHints = 321,

        /// <summary>
        /// SampleFormat (see Section 19: Data Sample Format).
        /// </summary>
        SampleFormat = 339,

        /// <summary>
        /// SMinSampleValue (see Section 19: Data Sample Format).
        /// </summary>
        SMinSampleValue = 340,

        /// <summary>
        /// SMaxSampleValue (see Section 19: Data Sample Format).
        /// </summary>
        SMaxSampleValue = 341,

        /// <summary>
        /// WhitePoint (see Section 20: RGB Image Colorimetry).
        /// </summary>
        WhitePoint = 318,

        /// <summary>
        /// PrimaryChromaticities (see Section 20: RGB Image Colorimetry).
        /// </summary>
        PrimaryChromaticities = 319,

        /// <summary>
        /// TransferFunction (see Section 20: RGB Image Colorimetry).
        /// </summary>
        TransferFunction = 301,

        /// <summary>
        /// TransferRange (see Section 20: RGB Image Colorimetry).
        /// </summary>
        TransferRange = 342,

        /// <summary>
        /// ReferenceBlackWhite (see Section 20: RGB Image Colorimetry).
        /// </summary>
        ReferenceBlackWhite = 532,

        /// <summary>
        /// YCbCrCoefficients (see Section 21: YCbCr Images).
        /// </summary>
        YCbCrCoefficients = 529,

        /// <summary>
        /// YCbCrSubSampling (see Section 21: YCbCr Images).
        /// </summary>
        YCbCrSubSampling = 530,

        /// <summary>
        /// YCbCrPositioning (see Section 21: YCbCr Images).
        /// </summary>
        YCbCrPositioning = 531,

        /// <summary>
        /// JpegProc (see Section 22: JPEG Compression).
        /// </summary>
        JpegProc = 512,

        /// <summary>
        /// JpegInterchangeFormat (see Section 22: JPEG Compression).
        /// </summary>
        JpegInterchangeFormat = 513,

        /// <summary>
        /// JpegInterchangeFormatLength (see Section 22: JPEG Compression).
        /// </summary>
        JpegInterchangeFormatLength = 514,

        /// <summary>
        /// JpegRestartInterval (see Section 22: JPEG Compression).
        /// </summary>
        JpegRestartInterval = 515,

        /// <summary>
        /// JpegLosslessPredictors (see Section 22: JPEG Compression).
        /// </summary>
        JpegLosslessPredictors = 517,

        /// <summary>
        /// JpegPointTransforms (see Section 22: JPEG Compression).
        /// </summary>
        JpegPointTransforms = 518,

        /// <summary>
        /// JpegQTables (see Section 22: JPEG Compression).
        /// </summary>
        JpegQTables = 519,

        /// <summary>
        /// JpegDCTables (see Section 22: JPEG Compression).
        /// </summary>
        JpegDCTables = 520,

        /// <summary>
        /// JpegACTables (see Section 22: JPEG Compression).
        /// </summary>
        JpegACTables = 521,

        /// <summary>
        /// SubIFDs (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        SubIFDs = 330,

        /// <summary>
        /// ClipPath (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        ClipPath = 343,

        /// <summary>
        /// XClipPathUnits (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        XClipPathUnits = 344,

        /// <summary>
        /// YClipPathUnits (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        YClipPathUnits = 345,

        /// <summary>
        /// Indexed (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        Indexed = 346,

        /// <summary>
        /// ImageID (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        ImageID = 32781,

        /// <summary>
        /// OpiProxy (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        OpiProxy = 351,

        /// <summary>
        /// ImageSourceData (see TIFF Supplement 2: Adobe Photoshop).
        /// </summary>
        ImageSourceData = 37724,

        /// <summary>
        /// JPEGTables (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        JPEGTables = 0x015B,

        /// <summary>
        /// CFARepeatPatternDim (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        CFARepeatPatternDim = 0x828D,

        /// <summary>
        /// BatteryLevel (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        BatteryLevel = 0x828F,

        /// <summary>
        /// Interlace (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        Interlace = 0x8829,

        /// <summary>
        /// TimeZoneOffset (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        TimeZoneOffset = 0x882A,

        /// <summary>
        /// SelfTimerMode (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        SelfTimerMode = 0x882B,

        /// <summary>
        /// Noise (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        Noise = 0x920D,

        /// <summary>
        /// ImageNumber (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        ImageNumber = 0x9211,

        /// <summary>
        /// SecurityClassification (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        SecurityClassification = 0x9212,

        /// <summary>
        /// ImageHistory (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        ImageHistory = 0x9213,

        /// <summary>
        /// TiffEPStandardID (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        TiffEPStandardID = 0x9216,

        /// <summary>
        /// BadFaxLines (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        BadFaxLines = 326,

        /// <summary>
        /// CleanFaxData (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        CleanFaxData = 327,

        /// <summary>
        /// ConsecutiveBadFaxLines (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        ConsecutiveBadFaxLines = 328,

        /// <summary>
        /// GlobalParametersIFD (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        GlobalParametersIFD = 400,

        /// <summary>
        /// ProfileType (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        ProfileType = 401,

        /// <summary>
        /// FaxProfile (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        FaxProfile = 402,

        /// <summary>
        /// CodingMethod (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        CodingMethod = 403,

        /// <summary>
        /// VersionYear (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        VersionYear = 404,

        /// <summary>
        /// ModeNumber (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        ModeNumber = 405,

        /// <summary>
        /// Decode (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        Decode = 433,

        /// <summary>
        /// DefaultImageColor (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        DefaultImageColor = 434,

        /// <summary>
        /// StripRowCounts (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        StripRowCounts = 559,

        /// <summary>
        /// ImageLayer (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        ImageLayer = 34732,

        /// <summary>
        /// Xmp (Embedded Metadata).
        /// </summary>
        Xmp = 700,

        /// <summary>
        /// Iptc (Embedded Metadata).
        /// </summary>
        Iptc = 33723,

        /// <summary>
        /// Photoshop (Embedded Metadata).
        /// </summary>
        Photoshop = 34377,

        /// <summary>
        /// ExifIFD (Embedded Metadata).
        /// </summary>
        ExifIFD = 34665,

        /// <summary>
        /// GpsIFD (Embedded Metadata).
        /// </summary>
        GpsIFD = 34853,

        /// <summary>
        /// InteroperabilityIFD (Embedded Metadata).
        /// </summary>
        InteroperabilityIFD = 40965,

        /// <summary>
        /// WangAnnotation (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        WangAnnotation = 32932,

        /// <summary>
        /// MDFileTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDFileTag = 33445,

        /// <summary>
        /// MDScalePixel (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDScalePixel = 33446,

        /// <summary>
        /// MDColorTable (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDColorTable = 33447,

        /// <summary>
        /// MDLabName (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDLabName = 33448,

        /// <summary>
        /// MDSampleInfo (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDSampleInfo = 33449,

        /// <summary>
        /// MDPrepDate (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDPrepDate = 33450,

        /// <summary>
        /// MDPrepTime (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDPrepTime = 33451,

        /// <summary>
        /// MDFileUnits (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        MDFileUnits = 33452,

        /// <summary>
        /// ModelPixelScaleTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        ModelPixelScaleTag = 33550,

        /// <summary>
        /// IngrPacketDataTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        IngrPacketDataTag = 33918,

        /// <summary>
        /// IngrFlagRegisters (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        IngrFlagRegisters = 33919,

        /// <summary>
        /// IrasBTransformationMatrix (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        IrasBTransformationMatrix = 33920,

        /// <summary>
        /// ModelTiePointTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        ModelTiePointTag = 33922,

        /// <summary>
        /// ModelTransformationTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        ModelTransformationTag = 34264,

        /// <summary>
        /// IccProfile (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        IccProfile = 34675,

        /// <summary>
        /// GeoKeyDirectoryTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        GeoKeyDirectoryTag = 34735,

        /// <summary>
        /// GeoDoubleParamsTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        GeoDoubleParamsTag = 34736,

        /// <summary>
        /// GeoAsciiParamsTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        GeoAsciiParamsTag = 34737,

        /// <summary>
        /// HylaFAXFaxRecvParams (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        HylaFAXFaxRecvParams = 34908,

        /// <summary>
        /// HylaFAXFaxSubAddress (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        HylaFAXFaxSubAddress = 34909,

        /// <summary>
        /// HylaFAXFaxRecvTime (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        HylaFAXFaxRecvTime = 34910,

        /// <summary>
        /// GdalMetadata (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        GdalMetadata = 42112,

        /// <summary>
        /// GdalNodata (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        GdalNodata = 42113,

        /// <summary>
        /// OceScanjobDescription (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        OceScanjobDescription = 50215,

        /// <summary>
        /// OceApplicationSelector (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        OceApplicationSelector = 50216,

        /// <summary>
        /// OceIdentificationNumber (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        OceIdentificationNumber = 50217,

        /// <summary>
        /// OceImageLogicCharacteristics (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        OceImageLogicCharacteristics = 50218,

        /// <summary>
        /// AliasLayerMetadata (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        AliasLayerMetadata = 50784,
    }
}
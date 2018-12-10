// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Constants representing tag IDs in the Tiff file-format.
    /// </summary>
    internal class TiffTags
    {
        /// <summary>
        /// Artist (see Section 8: Baseline Fields).
        /// </summary>
        public const int Artist = 315;

        /// <summary>
        /// BitsPerSample (see Section 8: Baseline Fields).
        /// </summary>
        public const int BitsPerSample = 258;

        /// <summary>
        /// CellLength (see Section 8: Baseline Fields).
        /// </summary>
        public const int CellLength = 265;

        /// <summary>
        /// CellWidth (see Section 8: Baseline Fields).
        /// </summary>
        public const int CellWidth = 264;

        /// <summary>
        /// ColorMap (see Section 8: Baseline Fields).
        /// </summary>
        public const int ColorMap = 320;

        /// <summary>
        /// Compression (see Section 8: Baseline Fields).
        /// </summary>
        public const int Compression = 259;

        /// <summary>
        /// Copyright (see Section 8: Baseline Fields).
        /// </summary>
        public const int Copyright = 33432;

        /// <summary>
        /// DateTime (see Section 8: Baseline Fields).
        /// </summary>
        public const int DateTime = 306;

        /// <summary>
        /// ExtraSamples (see Section 8: Baseline Fields).
        /// </summary>
        public const int ExtraSamples = 338;

        /// <summary>
        /// FillOrder (see Section 8: Baseline Fields).
        /// </summary>
        public const int FillOrder = 266;

        /// <summary>
        /// FreeByteCounts (see Section 8: Baseline Fields).
        /// </summary>
        public const int FreeByteCounts = 289;

        /// <summary>
        /// FreeOffsets (see Section 8: Baseline Fields).
        /// </summary>
        public const int FreeOffsets = 288;

        /// <summary>
        /// GrayResponseCurve (see Section 8: Baseline Fields).
        /// </summary>
        public const int GrayResponseCurve = 291;

        /// <summary>
        /// GrayResponseUnit (see Section 8: Baseline Fields).
        /// </summary>
        public const int GrayResponseUnit = 290;

        /// <summary>
        /// HostComputer (see Section 8: Baseline Fields).
        /// </summary>
        public const int HostComputer = 316;

        /// <summary>
        /// ImageDescription (see Section 8: Baseline Fields).
        /// </summary>
        public const int ImageDescription = 270;

        /// <summary>
        /// ImageLength (see Section 8: Baseline Fields).
        /// </summary>
        public const int ImageLength = 257;

        /// <summary>
        /// ImageWidth (see Section 8: Baseline Fields).
        /// </summary>
        public const int ImageWidth = 256;

        /// <summary>
        /// Make (see Section 8: Baseline Fields).
        /// </summary>
        public const int Make = 271;

        /// <summary>
        /// MaxSampleValue (see Section 8: Baseline Fields).
        /// </summary>
        public const int MaxSampleValue = 281;

        /// <summary>
        /// MinSampleValue (see Section 8: Baseline Fields).
        /// </summary>
        public const int MinSampleValue = 280;

        /// <summary>
        /// Model (see Section 8: Baseline Fields).
        /// </summary>
        public const int Model = 272;

        /// <summary>
        /// NewSubfileType (see Section 8: Baseline Fields).
        /// </summary>
        public const int NewSubfileType = 254;

        /// <summary>
        /// Orientation (see Section 8: Baseline Fields).
        /// </summary>
        public const int Orientation = 274;

        /// <summary>
        /// PhotometricInterpretation (see Section 8: Baseline Fields).
        /// </summary>
        public const int PhotometricInterpretation = 262;

        /// <summary>
        /// PlanarConfiguration (see Section 8: Baseline Fields).
        /// </summary>
        public const int PlanarConfiguration = 284;

        /// <summary>
        /// ResolutionUnit (see Section 8: Baseline Fields).
        /// </summary>
        public const int ResolutionUnit = 296;

        /// <summary>
        /// RowsPerStrip (see Section 8: Baseline Fields).
        /// </summary>
        public const int RowsPerStrip = 278;

        /// <summary>
        /// SamplesPerPixel (see Section 8: Baseline Fields).
        /// </summary>
        public const int SamplesPerPixel = 277;

        /// <summary>
        /// Software (see Section 8: Baseline Fields).
        /// </summary>
        public const int Software = 305;

        /// <summary>
        /// StripByteCounts (see Section 8: Baseline Fields).
        /// </summary>
        public const int StripByteCounts = 279;

        /// <summary>
        /// StripOffsets (see Section 8: Baseline Fields).
        /// </summary>
        public const int StripOffsets = 273;

        /// <summary>
        /// SubfileType (see Section 8: Baseline Fields).
        /// </summary>
        public const int SubfileType = 255;

        /// <summary>
        /// Threshholding (see Section 8: Baseline Fields).
        /// </summary>
        public const int Threshholding = 263;

        /// <summary>
        /// XResolution (see Section 8: Baseline Fields).
        /// </summary>
        public const int XResolution = 282;

        /// <summary>
        /// YResolution (see Section 8: Baseline Fields).
        /// </summary>
        public const int YResolution = 283;

        /// <summary>
        /// T4Options (see Section 11: CCITT Bilevel Encodings).
        /// </summary>
        public const int T4Options = 292;

        /// <summary>
        /// T6Options (see Section 11: CCITT Bilevel Encodings).
        /// </summary>
        public const int T6Options = 293;

        /// <summary>
        /// DocumentName (see Section 12: Document Storage and Retrieval).
        /// </summary>
        public const int DocumentName = 269;

        /// <summary>
        /// PageName (see Section 12: Document Storage and Retrieval).
        /// </summary>
        public const int PageName = 285;

        /// <summary>
        /// PageNumber (see Section 12: Document Storage and Retrieval).
        /// </summary>
        public const int PageNumber = 297;

        /// <summary>
        /// XPosition (see Section 12: Document Storage and Retrieval).
        /// </summary>
        public const int XPosition = 286;

        /// <summary>
        /// YPosition (see Section 12: Document Storage and Retrieval).
        /// </summary>
        public const int YPosition = 287;

        /// <summary>
        /// Predictor (see Section 14: Differencing Predictor).
        /// </summary>
        public const int Predictor = 317;

        /// <summary>
        /// TileWidth (see Section 15: Tiled Images).
        /// </summary>
        public const int TileWidth = 322;

        /// <summary>
        /// TileLength (see Section 15: Tiled Images).
        /// </summary>
        public const int TileLength = 323;

        /// <summary>
        /// TileOffsets (see Section 15: Tiled Images).
        /// </summary>
        public const int TileOffsets = 324;

        /// <summary>
        /// TileByteCounts (see Section 15: Tiled Images).
        /// </summary>
        public const int TileByteCounts = 325;

        /// <summary>
        /// InkSet (see Section 16: CMYK Images).
        /// </summary>
        public const int InkSet = 332;

        /// <summary>
        /// NumberOfInks (see Section 16: CMYK Images).
        /// </summary>
        public const int NumberOfInks = 334;

        /// <summary>
        /// InkNames (see Section 16: CMYK Images).
        /// </summary>
        public const int InkNames = 333;

        /// <summary>
        /// DotRange (see Section 16: CMYK Images).
        /// </summary>
        public const int DotRange = 336;

        /// <summary>
        /// TargetPrinter (see Section 16: CMYK Images).
        /// </summary>
        public const int TargetPrinter = 337;

        /// <summary>
        /// HalftoneHints (see Section 17: Halftone Hints).
        /// </summary>
        public const int HalftoneHints = 321;

        /// <summary>
        /// SampleFormat (see Section 19: Data Sample Format).
        /// </summary>
        public const int SampleFormat = 339;

        /// <summary>
        /// SMinSampleValue (see Section 19: Data Sample Format).
        /// </summary>
        public const int SMinSampleValue = 340;

        /// <summary>
        /// SMaxSampleValue (see Section 19: Data Sample Format).
        /// </summary>
        public const int SMaxSampleValue = 341;

        /// <summary>
        /// WhitePoint (see Section 20: RGB Image Colorimetry).
        /// </summary>
        public const int WhitePoint = 318;

        /// <summary>
        /// PrimaryChromaticities (see Section 20: RGB Image Colorimetry).
        /// </summary>
        public const int PrimaryChromaticities = 319;

        /// <summary>
        /// TransferFunction (see Section 20: RGB Image Colorimetry).
        /// </summary>
        public const int TransferFunction = 301;

        /// <summary>
        /// TransferRange (see Section 20: RGB Image Colorimetry).
        /// </summary>
        public const int TransferRange = 342;

        /// <summary>
        /// ReferenceBlackWhite (see Section 20: RGB Image Colorimetry).
        /// </summary>
        public const int ReferenceBlackWhite = 532;

        /// <summary>
        /// YCbCrCoefficients (see Section 21: YCbCr Images).
        /// </summary>
        public const int YCbCrCoefficients = 529;

        /// <summary>
        /// YCbCrSubSampling (see Section 21: YCbCr Images).
        /// </summary>
        public const int YCbCrSubSampling = 530;

        /// <summary>
        /// YCbCrPositioning (see Section 21: YCbCr Images).
        /// </summary>
        public const int YCbCrPositioning = 531;

        /// <summary>
        /// JpegProc (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegProc = 512;

        /// <summary>
        /// JpegInterchangeFormat (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegInterchangeFormat = 513;

        /// <summary>
        /// JpegInterchangeFormatLength (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegInterchangeFormatLength = 514;

        /// <summary>
        /// JpegRestartInterval (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegRestartInterval = 515;

        /// <summary>
        /// JpegLosslessPredictors (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegLosslessPredictors = 517;

        /// <summary>
        /// JpegPointTransforms (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegPointTransforms = 518;

        /// <summary>
        /// JpegQTables (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegQTables = 519;

        /// <summary>
        /// JpegDCTables (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegDCTables = 520;

        /// <summary>
        /// JpegACTables (see Section 22: JPEG Compression).
        /// </summary>
        public const int JpegACTables = 521;

        /// <summary>
        /// SubIFDs (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        public const int SubIFDs = 330;

        /// <summary>
        /// ClipPath (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        public const int ClipPath = 343;

        /// <summary>
        /// XClipPathUnits (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        public const int XClipPathUnits = 344;

        /// <summary>
        /// YClipPathUnits (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        public const int YClipPathUnits = 345;

        /// <summary>
        /// Indexed (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        public const int Indexed = 346;

        /// <summary>
        /// ImageID (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        public const int ImageID = 32781;

        /// <summary>
        /// OpiProxy (see TIFF Supplement 1: Adobe Pagemaker 6.0).
        /// </summary>
        public const int OpiProxy = 351;

        /// <summary>
        /// ImageSourceData (see TIFF Supplement 2: Adobe Photoshop).
        /// </summary>
        public const int ImageSourceData = 37724;

        /// <summary>
        /// JPEGTables (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int JPEGTables = 0x015B;

        /// <summary>
        /// CFARepeatPatternDim (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int CFARepeatPatternDim = 0x828D;

        /// <summary>
        /// BatteryLevel (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int BatteryLevel = 0x828F;

        /// <summary>
        /// Interlace (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int Interlace = 0x8829;

        /// <summary>
        /// TimeZoneOffset (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int TimeZoneOffset = 0x882A;

        /// <summary>
        /// SelfTimerMode (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int SelfTimerMode = 0x882B;

        /// <summary>
        /// Noise (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int Noise = 0x920D;

        /// <summary>
        /// ImageNumber (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int ImageNumber = 0x9211;

        /// <summary>
        /// SecurityClassification (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int SecurityClassification = 0x9212;

        /// <summary>
        /// ImageHistory (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int ImageHistory = 0x9213;

        /// <summary>
        /// TiffEPStandardID (see TIFF/EP Specification: Additional Tags).
        /// </summary>
        public const int TiffEPStandardID = 0x9216;

        /// <summary>
        /// BadFaxLines (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int BadFaxLines = 326;

        /// <summary>
        /// CleanFaxData (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int CleanFaxData = 327;

        /// <summary>
        /// ConsecutiveBadFaxLines (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int ConsecutiveBadFaxLines = 328;

        /// <summary>
        /// GlobalParametersIFD (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int GlobalParametersIFD = 400;

        /// <summary>
        /// ProfileType (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int ProfileType = 401;

        /// <summary>
        /// FaxProfile (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int FaxProfile = 402;

        /// <summary>
        /// CodingMethod (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int CodingMethod = 403;

        /// <summary>
        /// VersionYear (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int VersionYear = 404;

        /// <summary>
        /// ModeNumber (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int ModeNumber = 405;

        /// <summary>
        /// Decode (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int Decode = 433;

        /// <summary>
        /// DefaultImageColor (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int DefaultImageColor = 434;

        /// <summary>
        /// StripRowCounts (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int StripRowCounts = 559;

        /// <summary>
        /// ImageLayer (see RFC2301: TIFF-F/FX Specification).
        /// </summary>
        public const int ImageLayer = 34732;

        /// <summary>
        /// Xmp (Embedded Metadata).
        /// </summary>
        public const int Xmp = 700;

        /// <summary>
        /// Iptc (Embedded Metadata).
        /// </summary>
        public const int Iptc = 33723;

        /// <summary>
        /// Photoshop (Embedded Metadata).
        /// </summary>
        public const int Photoshop = 34377;

        /// <summary>
        /// ExifIFD (Embedded Metadata).
        /// </summary>
        public const int ExifIFD = 34665;

        /// <summary>
        /// GpsIFD (Embedded Metadata).
        /// </summary>
        public const int GpsIFD = 34853;

        /// <summary>
        /// InteroperabilityIFD (Embedded Metadata).
        /// </summary>
        public const int InteroperabilityIFD = 40965;

        /// <summary>
        /// WangAnnotation (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int WangAnnotation = 32932;

        /// <summary>
        /// MDFileTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDFileTag = 33445;

        /// <summary>
        /// MDScalePixel (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDScalePixel = 33446;

        /// <summary>
        /// MDColorTable (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDColorTable = 33447;

        /// <summary>
        /// MDLabName (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDLabName = 33448;

        /// <summary>
        /// MDSampleInfo (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDSampleInfo = 33449;

        /// <summary>
        /// MDPrepDate (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDPrepDate = 33450;

        /// <summary>
        /// MDPrepTime (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDPrepTime = 33451;

        /// <summary>
        /// MDFileUnits (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int MDFileUnits = 33452;

        /// <summary>
        /// ModelPixelScaleTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int ModelPixelScaleTag = 33550;

        /// <summary>
        /// IngrPacketDataTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int IngrPacketDataTag = 33918;

        /// <summary>
        /// IngrFlagRegisters (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int IngrFlagRegisters = 33919;

        /// <summary>
        /// IrasBTransformationMatrix (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int IrasBTransformationMatrix = 33920;

        /// <summary>
        /// ModelTiePointTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int ModelTiePointTag = 33922;

        /// <summary>
        /// ModelTransformationTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int ModelTransformationTag = 34264;

        /// <summary>
        /// IccProfile (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int IccProfile = 34675;

        /// <summary>
        /// GeoKeyDirectoryTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int GeoKeyDirectoryTag = 34735;

        /// <summary>
        /// GeoDoubleParamsTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int GeoDoubleParamsTag = 34736;

        /// <summary>
        /// GeoAsciiParamsTag (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int GeoAsciiParamsTag = 34737;

        /// <summary>
        /// HylaFAXFaxRecvParams (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int HylaFAXFaxRecvParams = 34908;

        /// <summary>
        /// HylaFAXFaxSubAddress (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int HylaFAXFaxSubAddress = 34909;

        /// <summary>
        /// HylaFAXFaxRecvTime (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int HylaFAXFaxRecvTime = 34910;

        /// <summary>
        /// GdalMetadata (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int GdalMetadata = 42112;

        /// <summary>
        /// GdalNodata (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int GdalNodata = 42113;

        /// <summary>
        /// OceScanjobDescription (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int OceScanjobDescription = 50215;

        /// <summary>
        /// OceApplicationSelector (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int OceApplicationSelector = 50216;

        /// <summary>
        /// OceIdentificationNumber (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int OceIdentificationNumber = 50217;

        /// <summary>
        /// OceImageLogicCharacteristics (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int OceImageLogicCharacteristics = 50218;

        /// <summary>
        /// AliasLayerMetadata (Other Private TIFF tags : see http://www.awaresystems.be/imaging/tiff/tifftags/private.html).
        /// </summary>
        public const int AliasLayerMetadata = 50784;
    }
}
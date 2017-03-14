// <copyright file="TiffTags.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Constants representing tag IDs in the Tiff file-format.
    /// </summary>
    internal class TiffTags
    {
        // Section 8: Baseline Fields

        public const int Artist = 315;
        public const int BitsPerSample = 258;
        public const int CellLength = 265;
        public const int CellWidth = 264;
        public const int ColorMap = 320;
        public const int Compression = 259;
        public const int Copyright = 33432;
        public const int DateTime = 306;
        public const int ExtraSamples = 338;
        public const int FillOrder = 266;
        public const int FreeByteCounts = 289;
        public const int FreeOffsets = 288;
        public const int GrayResponseCurve = 291;
        public const int GrayResponseUnit = 290;
        public const int HostComputer = 316;
        public const int ImageDescription = 270;
        public const int ImageLength = 257;
        public const int ImageWidth = 256;
        public const int Make = 271;
        public const int MaxSampleValue = 281;
        public const int MinSampleValue = 280;
        public const int Model = 272;
        public const int NewSubfileType = 254;
        public const int Orientation = 274;
        public const int PhotometricInterpretation = 262;
        public const int PlanarConfiguration = 284;
        public const int ResolutionUnit = 296;
        public const int RowsPerStrip = 278;
        public const int SamplesPerPixel = 277;
        public const int Software = 305;
        public const int StripByteCounts = 279;
        public const int StripOffsets = 273;
        public const int SubfileType = 255;
        public const int Threshholding = 263;
        public const int XResolution = 282;
        public const int YResolution = 283;

        // Section 11: CCITT Bilevel Encodings

        public const int T4Options = 292;
        public const int T6Options = 293;

        // Section 12: Document Storage and Retrieval

        public const int DocumentName = 269;
        public const int PageName = 285;
        public const int PageNumber = 297;
        public const int XPosition = 286;
        public const int YPosition = 287;

        // Section 14: Differencing Predictor

        public const int Predictor = 317;

        // Section 15: Tiled Images

        public const int TileWidth = 322;
        public const int TileLength = 323;
        public const int TileOffsets = 324;
        public const int TileByteCounts = 325;

        // Section 16: CMYK Images

        public const int InkSet = 332;
        public const int NumberOfInks = 334;
        public const int InkNames = 333;
        public const int DotRange = 336;
        public const int TargetPrinter = 337;

        // Section 17: Halftone Hints

        public const int HalftoneHints = 321;

        // Section 19: Data Sample Format

        public const int SampleFormat = 339;
        public const int SMinSampleValue = 340;
        public const int SMaxSampleValue = 341;

        // Section 20: RGB Image Colorimetry

        public const int WhitePoint = 318;
        public const int PrimaryChromaticities = 319;
        public const int TransferFunction = 301;
        public const int TransferRange = 342;
        public const int ReferenceBlackWhite = 532;

        // Section 21: YCbCr Images

        public const int YCbCrCoefficients = 529;
        public const int YCbCrSubSampling = 530;
        public const int YCbCrPositioning = 531;

        // Section 22: JPEG Compression

        public const int JpegProc = 512;
        public const int JpegInterchangeFormat = 513;
        public const int JpegInterchangeFormatLength = 514;
        public const int JpegRestartInterval = 515;
        public const int JpegLosslessPredictors = 517;
        public const int JpegPointTransforms = 518;
        public const int JpegQTables = 519;
        public const int JpegDCTables = 520;
        public const int JpegACTables = 521;

        // TIFF Supplement 1: Adobe Pagemaker 6.0

        public const int SubIFDs = 330;
        public const int ClipPath = 343;
        public const int XClipPathUnits = 344;
        public const int YClipPathUnits = 345;
        public const int Indexed = 346;
        public const int ImageID = 32781;
        public const int OpiProxy = 351;

        // TIFF Supplement 2: Adobe Photoshop

        public const int ImageSourceData = 37724;

        // TIFF/EP Specification: Additional Tags

        public const int JPEGTables = 0x015B;
        public const int CFARepeatPatternDim = 0x828D;
        public const int BatteryLevel = 0x828F;
        public const int Interlace = 0x8829;
        public const int TimeZoneOffset = 0x882A;
        public const int SelfTimerMode = 0x882B;
        public const int Noise = 0x920D;
        public const int ImageNumber = 0x9211;
        public const int SecurityClassification = 0x9212;
        public const int ImageHistory = 0x9213;
        public const int TiffEPStandardID = 0x9216;

        // TIFF-F/FX Specification (http://www.ietf.org/rfc/rfc2301.txt)

        public const int BadFaxLines = 326;
        public const int CleanFaxData = 327;
        public const int ConsecutiveBadFaxLines = 328;
        public const int GlobalParametersIFD = 400;
        public const int ProfileType = 401;
        public const int FaxProfile = 402;
        public const int CodingMethod = 403;
        public const int VersionYear = 404;
        public const int ModeNumber = 405;
        public const int Decode = 433;
        public const int DefaultImageColor = 434;
        public const int StripRowCounts = 559;
        public const int ImageLayer = 34732;

        // Embedded Metadata

        public const int Xmp = 700;
        public const int Iptc = 33723;
        public const int Photoshop = 34377;
        public const int ExifIFD = 34665;
        public const int GpsIFD = 34853;
        public const int InteroperabilityIFD = 40965;

        // Other Private TIFF tags (http://www.awaresystems.be/imaging/tiff/tifftags/private.html)

        public const int WangAnnotation = 32932;
        public const int MDFileTag = 33445;
        public const int MDScalePixel = 33446;
        public const int MDColorTable = 33447;
        public const int MDLabName = 33448;
        public const int MDSampleInfo = 33449;
        public const int MDPrepDate = 33450;
        public const int MDPrepTime = 33451;
        public const int MDFileUnits = 33452;
        public const int ModelPixelScaleTag = 33550;
        public const int IngrPacketDataTag = 33918;
        public const int IngrFlagRegisters = 33919;
        public const int IrasBTransformationMatrix = 33920;
        public const int ModelTiePointTag = 33922;
        public const int ModelTransformationTag = 34264;
        public const int IccProfile = 34675;
        public const int GeoKeyDirectoryTag = 34735;
        public const int GeoDoubleParamsTag = 34736;
        public const int GeoAsciiParamsTag = 34737;
        public const int HylaFAXFaxRecvParams = 34908;
        public const int HylaFAXFaxSubAddress = 34909;
        public const int HylaFAXFaxRecvTime = 34910;
        public const int GdalMetadata = 42112;
        public const int GdalNodata = 42113;
        public const int OceScanjobDescription = 50215;
        public const int OceApplicationSelector = 50216;
        public const int OceIdentificationNumber = 50217;
        public const int OceImageLogicCharacteristics = 50218;
        public const int AliasLayerMetadata = 50784;
    }
}
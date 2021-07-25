// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// All exif tags from the Exif standard 2.31.
    /// </summary>
    internal enum ExifTagValue
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
        /// Indicates the identification of the Interoperability rule.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/privateifd/interoperability/interoperabilityindex.html
        /// </summary>
        [ExifTagDescription("R98", "Indicates a file conforming to R98 file specification of Recommended Exif Interoperability Rules (ExifR98) or to DCF basic file stipulated by Design Rule for Camera File System.")]
        [ExifTagDescription("THM", "Indicates a file conforming to DCF thumbnail file stipulated by Design rule for Camera File System.")]
        InteroperabilityIndex = 0x0001,

        /// <summary>
        /// A general indication of the kind of data contained in this subfile.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription(0U, "Full-resolution Image")]
        [ExifTagDescription(1U, "Reduced-resolution image")]
        [ExifTagDescription(2U, "Single page of multi-page image")]
        [ExifTagDescription(3U, "Single page of multi-page reduced-resolution image")]
        [ExifTagDescription(4U, "Transparency mask")]
        [ExifTagDescription(5U, "Transparency mask of reduced-resolution image")]
        [ExifTagDescription(6U, "Transparency mask of multi-page image")]
        [ExifTagDescription(7U, "Transparency mask of reduced-resolution multi-page image")]
        [ExifTagDescription(0x10001U, "Alternate reduced-resolution image ")]
        SubfileType = 0x00FE,

        /// <summary>
        /// A general indication of the kind of data contained in this subfile.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription((ushort)1, "Full-resolution Image")]
        [ExifTagDescription((ushort)2, "Reduced-resolution image")]
        [ExifTagDescription((ushort)3, "Single page of multi-page image")]
        OldSubfileType = 0x00FF,

        /// <summary>
        /// The number of columns in the image, i.e., the number of pixels per row.
        /// See Section 8: Baseline Fields.
        /// </summary>
        ImageWidth = 0x0100,

        /// <summary>
        /// The number of rows of pixels in the image.
        /// See Section 8: Baseline Fields.
        /// </summary>
        ImageLength = 0x0101,

        /// <summary>
        /// Number of bits per component.
        /// See Section 8: Baseline Fields.
        /// </summary>
        BitsPerSample = 0x0102,

        /// <summary>
        /// Compression scheme used on the image data.
        /// See Section 8: Baseline Fields.
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
        /// The color space of the image data.
        /// See Section 8: Baseline Fields.
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
        /// For black and white TIFF files that represent shades of gray, the technique used to convert from gray to black and white pixels.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription((ushort)1, "No dithering or halftoning")]
        [ExifTagDescription((ushort)2, "Ordered dither or halftone")]
        [ExifTagDescription((ushort)3, "Randomized dither")]
        Thresholding = 0x0107,

        /// <summary>
        /// The width of the dithering or halftoning matrix used to create a dithered or halftoned bilevel file.
        /// See Section 8: Baseline Fields.
        /// </summary>
        CellWidth = 0x0108,

        /// <summary>
        /// The length of the dithering or halftoning matrix used to create a dithered or halftoned bilevel file.
        /// See Section 8: Baseline Fields.
        /// </summary>
        CellLength = 0x0109,

        /// <summary>
        /// The logical order of bits within a byte.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription((ushort)1, "Normal")]
        [ExifTagDescription((ushort)2, "Reversed")]
        FillOrder = 0x010A,

        /// <summary>
        /// The name of the document from which this image was scanned.
        /// See Section 12: Document Storage and Retrieval.
        /// </summary>
        DocumentName = 0x010D,

        /// <summary>
        /// A string that describes the subject of the image.
        /// See Section 8: Baseline Fields.
        /// </summary>
        ImageDescription = 0x010E,

        /// <summary>
        /// The scanner manufacturer.
        /// See Section 8: Baseline Fields.
        /// </summary>
        Make = 0x010F,

        /// <summary>
        /// The scanner model name or number.
        /// See Section 8: Baseline Fields.
        /// </summary>
        Model = 0x0110,

        /// <summary>
        /// For each strip, the byte offset of that strip.
        /// See Section 8: Baseline Fields.
        /// </summary>
        StripOffsets = 0x0111,

        /// <summary>
        /// The orientation of the image with respect to the rows and columns.
        /// See Section 8: Baseline Fields.
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
        /// The number of components per pixel.
        /// See Section 8: Baseline Fields.
        /// </summary>
        SamplesPerPixel = 0x0115,

        /// <summary>
        /// The number of rows per strip.
        /// See Section 8: Baseline Fields.
        /// </summary>
        RowsPerStrip = 0x0116,

        /// <summary>
        /// For each strip, the number of bytes in the strip after compression.
        /// See Section 8: Baseline Fields.
        /// </summary>
        StripByteCounts = 0x0117,

        /// <summary>
        /// The minimum component value used.
        /// See Section 8: Baseline Fields.
        /// </summary>
        MinSampleValue = 0x0118,

        /// <summary>
        /// The maximum component value used.
        /// See Section 8: Baseline Fields.
        /// </summary>
        MaxSampleValue = 0x0119,

        /// <summary>
        /// The number of pixels per ResolutionUnit in the ImageWidth direction.
        /// See Section 8: Baseline Fields.
        /// </summary>
        XResolution = 0x011A,

        /// <summary>
        /// The number of pixels per ResolutionUnit in the <see cref="ImageLength"/> direction.
        /// See Section 8: Baseline Fields.
        /// </summary>
        YResolution = 0x011B,

        /// <summary>
        /// How the components of each pixel are stored.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription((ushort)1, "Chunky")]
        [ExifTagDescription((ushort)2, "Planar")]
        PlanarConfiguration = 0x011C,

        /// <summary>
        /// The name of the page from which this image was scanned.
        /// See Section 12: Document Storage and Retrieval.
        /// </summary>
        PageName = 0x011D,

        /// <summary>
        /// X position of the image.
        /// See Section 12: Document Storage and Retrieval.
        /// </summary>
        XPosition = 0x011E,

        /// <summary>
        /// Y position of the image.
        /// See Section 12: Document Storage and Retrieval.
        /// </summary>
        YPosition = 0x011F,

        /// <summary>
        /// For each string of contiguous unused bytes in a TIFF file, the byte offset of the string.
        /// See Section 8: Baseline Fields.
        /// </summary>
        FreeOffsets = 0x0120,

        /// <summary>
        /// For each string of contiguous unused bytes in a TIFF file, the number of bytes in the string.
        /// See Section 8: Baseline Fields.
        /// </summary>
        FreeByteCounts = 0x0121,

        /// <summary>
        /// The precision of the information contained in the GrayResponseCurve.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription((ushort)1, "0.1")]
        [ExifTagDescription((ushort)2, "0.001")]
        [ExifTagDescription((ushort)3, "0.0001")]
        [ExifTagDescription((ushort)4, "1e-05")]
        [ExifTagDescription((ushort)5, "1e-06")]
        GrayResponseUnit = 0x0122,

        /// <summary>
        /// For grayscale data, the optical density of each possible pixel value.
        /// See Section 8: Baseline Fields.
        /// </summary>
        GrayResponseCurve = 0x0123,

        /// <summary>
        /// Options for Group 3 Fax compression.
        /// </summary>
        [ExifTagDescription(0U, "2-Dimensional encoding")]
        [ExifTagDescription(1U, "Uncompressed")]
        [ExifTagDescription(2U, "Fill bits added")]
        T4Options = 0x0124,

        /// <summary>
        /// Options for Group 4 Fax compression.
        /// </summary>
        [ExifTagDescription(1U, "Uncompressed")]
        T6Options = 0x0125,

        /// <summary>
        /// The unit of measurement for XResolution and YResolution.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription((ushort)1, "None")]
        [ExifTagDescription((ushort)2, "Inches")]
        [ExifTagDescription((ushort)3, "Centimeter")]
        ResolutionUnit = 0x0128,

        /// <summary>
        /// The page number of the page from which this image was scanned.
        /// See Section 12: Document Storage and Retrieval.
        /// </summary>
        PageNumber = 0x0129,

        /// <summary>
        /// ColorResponseUnit
        /// </summary>
        ColorResponseUnit = 0x012C,

        /// <summary>
        /// TransferFunction
        /// </summary>
        TransferFunction = 0x012D,

        /// <summary>
        /// Name and version number of the software package(s) used to create the image.
        /// See Section 8: Baseline Fields.
        /// </summary>
        Software = 0x0131,

        /// <summary>
        /// Date and time of image creation.
        /// See Section 8: Baseline Fields.
        /// </summary>
        DateTime = 0x0132,

        /// <summary>
        /// Person who created the image.
        /// See Section 8: Baseline Fields.
        /// </summary>
        Artist = 0x013B,

        /// <summary>
        /// The computer and/or operating system in use at the time of image creation.
        /// See Section 8: Baseline Fields.
        /// </summary>
        HostComputer = 0x013C,

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
        /// A color map for palette color images.
        /// See Section 8: Baseline Fields.
        /// </summary>
        ColorMap = 0x0140,

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
        [ExifTagDescription(0U, "Clean")]
        [ExifTagDescription(1U, "Regenerated")]
        [ExifTagDescription(2U, "Unclean")]
        CleanFaxData = 0x0147,

        /// <summary>
        /// ConsecutiveBadFaxLines
        /// </summary>
        ConsecutiveBadFaxLines = 0x0148,

        /// <summary>
        /// Offset to child IFDs.
        /// See TIFF Supplement 1: Adobe Pagemaker 6.0.
        /// Each value is an offset (from the beginning of the TIFF file, as always) to a child IFD. Child images provide extra information for the parent image - such as a subsampled version of the parent image.
        /// TIFF data type is Long or 13, IFD. The IFD type is identical to LONG, except that it is only used to point to other valid IFDs.
        /// </summary>
        SubIFDs = 0x014A,

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
        /// Description of extra components.
        /// See Section 8: Baseline Fields.
        /// </summary>
        [ExifTagDescription((ushort)0, "Unspecified")]
        [ExifTagDescription((ushort)1, "Associated Alpha")]
        [ExifTagDescription((ushort)2, "Unassociated Alpha")]
        ExtraSamples = 0x0152,

        /// <summary>
        /// SampleFormat
        /// </summary>
        [ExifTagDescription((ushort)1, "Unsigned")]
        [ExifTagDescription((ushort)2, "Signed")]
        [ExifTagDescription((ushort)3, "Float")]
        [ExifTagDescription((ushort)4, "Undefined")]
        [ExifTagDescription((ushort)5, "Complex int")]
        [ExifTagDescription((ushort)6, "Complex float")]
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
        /// Used in the TIFF-FX standard to point to an IFD containing tags that are globally applicable to the complete TIFF file.
        /// See RFC2301: TIFF-F/FX Specification.
        /// It is recommended that a TIFF writer place this field in the first IFD, where a TIFF reader would find it quickly.
        /// Each field in the GlobalParametersIFD is a TIFF field that is legal in any IFD. Required baseline fields should not be located in the GlobalParametersIFD, but should be in each image IFD. If a conflict exists between fields in the GlobalParametersIFD and in the image IFDs, then the data in the image IFD shall prevail.
        /// </summary>
        GlobalParametersIFD = 0x0190,

        /// <summary>
        /// ProfileType
        /// </summary>
        [ExifTagDescription(0U, "Unspecified")]
        [ExifTagDescription(1U, "Group 3 FAX")]
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
        [ExifTagDescription(0UL, "Unspecified compression")]
        [ExifTagDescription(1UL, "Modified Huffman")]
        [ExifTagDescription(2UL, "Modified Read")]
        [ExifTagDescription(4UL, "Modified MR")]
        [ExifTagDescription(8UL, "JBIG")]
        [ExifTagDescription(16UL, "Baseline JPEG")]
        [ExifTagDescription(32UL, "JBIG color")]
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
        /// T82ptions
        /// </summary>
        T82ptions = 0x01B3,

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
        /// Rating
        /// </summary>
        Rating = 0x4746,

        /// <summary>
        /// RatingPercent
        /// </summary>
        RatingPercent = 0x4749,

        /// <summary>
        /// ImageID
        /// </summary>
        ImageID = 0x800D,

        /// <summary>
        /// Annotation data, as used in 'Imaging for Windows'.
        /// See Other Private TIFF tags: http://www.awaresystems.be/imaging/tiff/tifftags/private.html
        /// </summary>
        WangAnnotation = 0x80A4,

        /// <summary>
        /// CFARepeatPatternDim
        /// </summary>
        CFARepeatPatternDim = 0x828D,

        /// <summary>
        /// CFAPattern2
        /// </summary>
        CFAPattern2 = 0x828E,

        /// <summary>
        /// BatteryLevel
        /// </summary>
        BatteryLevel = 0x828F,

        /// <summary>
        /// Copyright notice.
        /// See Section 8: Baseline Fields.
        /// </summary>
        Copyright = 0x8298,

        /// <summary>
        /// ExposureTime
        /// </summary>
        ExposureTime = 0x829A,

        /// <summary>
        /// FNumber
        /// </summary>
        FNumber = 0x829D,

        /// <summary>
        /// Specifies the pixel data format encoding in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// </summary>
        [ExifTagDescription((ushort)2, "Squary root data format")]
        [ExifTagDescription((ushort)128, "Linear data format")]
        MDFileTag = 0x82A5,

        /// <summary>
        /// Specifies a scale factor in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// The scale factor is to be applies to each pixel before presenting it to the user.
        /// </summary>
        MDScalePixel = 0x82A6,

        /// <summary>
        /// Used to specify the conversion from 16bit to 8bit in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// Since the display is only 9bit, the 16bit data must be converted before display.
        /// 8bit value = (16bit value - low range ) * 255 / (high range - low range)
        /// Count: n.
        /// </summary>
        [ExifTagDescription((ushort)0, "lowest possible")]
        [ExifTagDescription((ushort)1, "low range")]
        [ExifTagDescription("n-2", "high range")]
        [ExifTagDescription("n-1", "highest possible")]
        MDColorTable = 0x82A7,

        /// <summary>
        /// Name of the lab that scanned this file, as used in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// </summary>
        MDLabName = 0x82A8,

        /// <summary>
        /// Information about the sample, as used in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// This information is entered by the person that scanned the file.
        /// Note that the word 'sample' as used here, refers to the scanned sample, not an image channel.
        /// </summary>
        MDSampleInfo = 0x82A9,

        /// <summary>
        /// Date the sample was prepared, as used in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// The format of this data is YY/MM/DD.
        /// Note that the word 'sample' as used here, refers to the scanned sample, not an image channel.
        /// </summary>
        MDPrepDate = 0x82AA,

        /// <summary>
        /// Time the sample was prepared, as used in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// Format of this data is HH:MM using the 24-hour clock.
        /// Note that the word 'sample' as used here, refers to the scanned sample, not an image channel.
        /// </summary>
        MDPrepTime = 0x82AB,

        /// <summary>
        /// Units for data in this file, as used in the Molecular Dynamics GEL file format.
        /// See Molecular Dynamics GEL File Format and Private Tags: https://www.awaresystems.be/imaging/tiff/tifftags/docs/gel.html
        /// </summary>
        [ExifTagDescription("O.D.", "Densitometer")]
        [ExifTagDescription("Counts", "PhosphorImager")]
        [ExifTagDescription("RFU", "FluorImager")]
        MDFileUnits = 0x82AC,

        /// <summary>
        /// PixelScale
        /// </summary>
        PixelScale = 0x830E,

        /// <summary>
        /// IPTC (International Press Telecommunications Council) metadata.
        /// See IPTC 4.1 specification.
        /// </summary>
        IPTC = 0x83BB,

        /// <summary>
        /// IntergraphPacketData
        /// </summary>
        IntergraphPacketData = 0x847E,

        /// <summary>
        /// IntergraphRegisters
        /// </summary>
        IntergraphRegisters = 0x847F,

        /// <summary>
        /// IntergraphMatrix
        /// </summary>
        IntergraphMatrix = 0x8480,

        /// <summary>
        /// ModelTiePoint
        /// </summary>
        ModelTiePoint = 0x8482,

        /// <summary>
        /// SEMInfo
        /// </summary>
        SEMInfo = 0x8546,

        /// <summary>
        /// ModelTransform
        /// </summary>
        ModelTransform = 0x85D8,

        /// <summary>
        /// Collection of Photoshop 'Image Resource Blocks' (Embedded Metadata).
        /// See Extracting the Thumbnail from the PhotoShop private TIFF Tag: https://www.awaresystems.be/imaging/tiff/tifftags/docs/photoshopthumbnail.html
        /// </summary>
        Photoshop = 0x8649,

        /// <summary>
        /// ICC profile data.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/iccprofile.html
        /// </summary>
        IccProfile = 0x8773,

        /// <summary>
        /// Used in interchangeable GeoTIFF files.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/geokeydirectorytag.html
        /// This tag is also know as 'ProjectionInfoTag' and 'CoordSystemInfoTag'
        /// This tag may be used to store the GeoKey Directory, which defines and references the "GeoKeys".
        /// </summary>
        GeoKeyDirectoryTag = 0x87AF,

        /// <summary>
        /// Used in interchangeable GeoTIFF files.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/geodoubleparamstag.html
        /// This tag is used to store all of the DOUBLE valued GeoKeys, referenced by the GeoKeyDirectoryTag. The meaning of any value of this double array is determined from the GeoKeyDirectoryTag reference pointing to it. FLOAT values should first be converted to DOUBLE and stored here.
        /// </summary>
        GeoDoubleParamsTag = 0x87B0,

        /// <summary>
        /// Used in interchangeable GeoTIFF files.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/geoasciiparamstag.html
        /// This tag is used to store all of the ASCII valued GeoKeys, referenced by the GeoKeyDirectoryTag. Since keys use offsets into tags, any special comments may be placed at the beginning of this tag. For the most part, the only keys that are ASCII valued are "Citation" keys, giving documentation and references for obscure projections, datums, etc.
        /// </summary>
        GeoAsciiParamsTag = 0x87B1,

        /// <summary>
        /// ImageLayer
        /// </summary>
        ImageLayer = 0x87AC,

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
        /// Interlace
        /// </summary>
        Interlace = 0x8829,

        /// <summary>
        /// TimeZoneOffset
        /// </summary>
        TimeZoneOffset = 0x882A,

        /// <summary>
        /// SelfTimerMode
        /// </summary>
        SelfTimerMode = 0x882B,

        /// <summary>
        /// SensitivityType
        /// </summary>
        [ExifTagDescription((ushort)0, "Unknown")]
        [ExifTagDescription((ushort)1, "Standard Output Sensitivity")]
        [ExifTagDescription((ushort)2, "Recommended Exposure Index")]
        [ExifTagDescription((ushort)3, "ISO Speed")]
        [ExifTagDescription((ushort)4, "Standard Output Sensitivity and Recommended Exposure Index")]
        [ExifTagDescription((ushort)5, "Standard Output Sensitivity and ISO Speed")]
        [ExifTagDescription((ushort)6, "Recommended Exposure Index and ISO Speed")]
        [ExifTagDescription((ushort)7, "Standard Output Sensitivity, Recommended Exposure Index and ISO Speed")]
        SensitivityType = 0x8830,

        /// <summary>
        /// StandardOutputSensitivity
        /// </summary>
        StandardOutputSensitivity = 0x8831,

        /// <summary>
        /// RecommendedExposureIndex
        /// </summary>
        RecommendedExposureIndex = 0x8832,

        /// <summary>
        /// ISOSpeed
        /// </summary>
        ISOSpeed = 0x8833,

        /// <summary>
        /// ISOSpeedLatitudeyyy
        /// </summary>
        ISOSpeedLatitudeyyy = 0x8834,

        /// <summary>
        /// ISOSpeedLatitudezzz
        /// </summary>
        ISOSpeedLatitudezzz = 0x8835,

        /// <summary>
        /// FaxRecvParams
        /// </summary>
        FaxRecvParams = 0x885C,

        /// <summary>
        /// FaxSubaddress
        /// </summary>
        FaxSubaddress = 0x885D,

        /// <summary>
        /// FaxRecvTime
        /// </summary>
        FaxRecvTime = 0x885E,

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
        /// OffsetTime
        /// </summary>
        OffsetTime = 0x9010,

        /// <summary>
        /// OffsetTimeOriginal
        /// </summary>
        OffsetTimeOriginal = 0x9011,

        /// <summary>
        /// OffsetTimeDigitized
        /// </summary>
        OffsetTimeDigitized = 0x9012,

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
        [ExifTagDescription((ushort)79, "On, Red-eye reduction, Return detected")]
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
        /// FlashEnergy2
        /// </summary>
        FlashEnergy2 = 0x920B,

        /// <summary>
        /// SpatialFrequencyResponse2
        /// </summary>
        SpatialFrequencyResponse2 = 0x920C,

        /// <summary>
        /// Noise
        /// </summary>
        Noise = 0x920D,

        /// <summary>
        /// FocalPlaneXResolution2
        /// </summary>
        FocalPlaneXResolution2 = 0x920E,

        /// <summary>
        /// FocalPlaneYResolution2
        /// </summary>
        FocalPlaneYResolution2 = 0x920F,

        /// <summary>
        /// FocalPlaneResolutionUnit2
        /// </summary>
        [ExifTagDescription((ushort)1, "None")]
        [ExifTagDescription((ushort)2, "Inches")]
        [ExifTagDescription((ushort)3, "Centimeter")]
        [ExifTagDescription((ushort)4, "Millimeter")]
        [ExifTagDescription((ushort)5, "Micrometer")]
        FocalPlaneResolutionUnit2 = 0x9210,

        /// <summary>
        /// ImageNumber
        /// </summary>
        ImageNumber = 0x9211,

        /// <summary>
        /// SecurityClassification
        /// </summary>
        [ExifTagDescription("C", "Confidential")]
        [ExifTagDescription("R", "Restricted")]
        [ExifTagDescription("S", "Secret")]
        [ExifTagDescription("T", "Top Secret")]
        [ExifTagDescription("U", "Unclassified")]
        SecurityClassification = 0x9212,

        /// <summary>
        /// ImageHistory
        /// </summary>
        ImageHistory = 0x9213,

        /// <summary>
        /// SubjectArea
        /// </summary>
        SubjectArea = 0x9214,

        /// <summary>
        /// ExposureIndex2
        /// </summary>
        ExposureIndex2 = 0x9215,

        /// <summary>
        /// TIFFEPStandardID
        /// </summary>
        TIFFEPStandardID = 0x9216,

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
        SensingMethod2 = 0x9217,

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
        /// ImageSourceData
        /// </summary>
        ImageSourceData = 0x935C,

        /// <summary>
        /// AmbientTemperature
        /// </summary>
        AmbientTemperature = 0x9400,

        /// <summary>
        /// Humidity
        /// </summary>
        Humidity = 0x9401,

        /// <summary>
        /// Pressure
        /// </summary>
        Pressure = 0x9402,

        /// <summary>
        /// WaterDepth
        /// </summary>
        WaterDepth = 0x9403,

        /// <summary>
        /// Acceleration
        /// </summary>
        Acceleration = 0x9404,

        /// <summary>
        /// CameraElevationAngle
        /// </summary>
        CameraElevationAngle = 0x9405,

        /// <summary>
        /// XPTitle
        /// </summary>
        XPTitle = 0x9C9B,

        /// <summary>
        /// XPComment
        /// </summary>
        XPComment = 0x9C9C,

        /// <summary>
        /// XPAuthor
        /// </summary>
        XPAuthor = 0x9C9D,

        /// <summary>
        /// XPKeywords
        /// </summary>
        XPKeywords = 0x9C9E,

        /// <summary>
        /// XPSubject
        /// </summary>
        XPSubject = 0x9C9F,

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
        /// A pointer to the Exif-related Interoperability IFD.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/privateifd/interoperability.html
        /// Interoperability IFD is composed of tags which stores the information to ensure the Interoperability.
        /// The Interoperability structure of Interoperability IFD is same as TIFF defined IFD structure but does not contain the image data characteristically compared with normal TIFF IFD.
        /// </summary>
        InteroperabilityIFD = 0xA005,

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
        /// OwnerName
        /// </summary>
        OwnerName = 0xA430,

        /// <summary>
        /// SerialNumber
        /// </summary>
        SerialNumber = 0xA431,

        /// <summary>
        /// LensSpecification
        /// </summary>
        LensSpecification = 0xA432,

        /// <summary>
        /// LensMake
        /// </summary>
        LensMake = 0xA433,

        /// <summary>
        /// LensModel
        /// </summary>
        LensModel = 0xA434,

        /// <summary>
        /// LensSerialNumber
        /// </summary>
        LensSerialNumber = 0xA435,

        /// <summary>
        /// GDALMetadata
        /// </summary>
        GDALMetadata = 0xA480,

        /// <summary>
        /// GDALNoData
        /// </summary>
        GDALNoData = 0xA481,

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
        GPSDifferential = 0x001E,

        /// <summary>
        /// Used in the Oce scanning process.
        /// Identifies the scanticket used in the scanning process.
        /// Includes a trailing zero.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/docs/oce.html
        /// </summary>
        OceScanjobDescription = 0xC427,

        /// <summary>
        /// Used in the Oce scanning process.
        /// Identifies the application to process the TIFF file that results from scanning.
        /// Includes a trailing zero.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/docs/oce.html
        /// </summary>
        OceApplicationSelector = 0xC428,

        /// <summary>
        /// Used in the Oce scanning process.
        /// This is the user's answer to an optional question embedded in the Oce scanticket, and presented to that user before scanning. It can serve in further determination of the workflow.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/docs/oce.html
        /// </summary>
        OceIdentificationNumber = 0xC429,

        /// <summary>
        /// Used in the Oce scanning process.
        /// This tag encodes the imageprocessing done by the Oce ImageLogic module in the scanner to ensure optimal quality for certain workflows.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/docs/oce.html
        /// </summary>
        OceImageLogicCharacteristics = 0xC42A,

        /// <summary>
        /// Alias Sketchbook Pro layer usage description.
        /// See https://www.awaresystems.be/imaging/tiff/tifftags/docs/alias.html
        /// </summary>
        AliasLayerMetadata = 0xC660,
    }
}

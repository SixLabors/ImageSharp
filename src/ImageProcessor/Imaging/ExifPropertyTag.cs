// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExifPropertyTag.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The following enum gives descriptions of the property items supported by Windows GDI+.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    /// <summary>
    /// The following enum gives descriptions of the property items supported by Windows GDI+.
    /// <see cref="http://msdn.microsoft.com/en-us/library/ms534417%28VS.85%29.aspx"/>
    /// TODO: Add more XML descriptions.
    /// </summary>
    public enum ExifPropertyTag
    {
        /// <summary>
        /// Null-terminated character string that specifies the name of the person who created the image.
        /// </summary>
        Artist = 0x013B,

        /// <summary>
        /// Number of bits per color component. See also <see cref="SamplesPerPixel"/>
        /// </summary>
        BitsPerSample = 0x0102,

        /// <summary>
        /// Height of the dithering or halftoning matrix.
        /// </summary>
        CellHeight = 0x0109,

        /// <summary>
        /// Width of the dithering or halftoning matrix.
        /// </summary>
        CellWidth = 0x0108,

        /// <summary>
        /// Chrominance table. The luminance table and the chrominance table are used to control JPEG quality. 
        /// A valid luminance or chrominance table has 64 entries. 
        /// If an image has either a luminance table or a chrominance table, then it must have both tables.
        /// </summary>
        ChrominanceTable = 0x5091,

        /// <summary>
        /// Color palette (lookup table) for a palette-indexed image.
        /// </summary>
        ColorMap = 0x0140,

        /// <summary>
        /// Table of values that specify color transfer functions.
        /// </summary>
        ColorTransferFunction = 0x501A,

        /// <summary>
        /// Compression scheme used for the image data.
        /// </summary>
        Compression = 0x0103,

        /// <summary>
        /// Null-terminated character string that contains copyright information.
        /// </summary>
        Copyright = 0x8298,

        /// <summary>
        /// Date and time the image was created.
        /// </summary>
        DateTime = 0x0132,

        /// <summary>
        /// Null-terminated character string that specifies the name of the document from 
        /// which the image was scanned.
        /// </summary>
        DocumentName = 0x010D,

        /// <summary>
        /// Color component values that correspond to a 0 percent dot and a 100 percent dot.
        /// </summary>
        DotRange = 0x0150,

        /// <summary>
        /// Null-terminated character string that specifies the manufacturer of the 
        /// equipment used to record the image.
        /// </summary>
        EquipMake = 0x010F,

        /// <summary>
        /// Null-terminated character string that specifies the model name or model number
        /// of the equipment used to record the image.
        /// </summary>
        EquipModel = 0x0110,

        /// <summary>
        /// Lens aperture. The unit is the APEX value.
        /// </summary>
        ExifAperture = 0x9202,

        /// <summary>
        /// Brightness value. The unit is the APEX value. Ordinarily it is given 
        /// in the range of -99.99 to 99.99.
        /// </summary>
        ExifBrightness = 0x9203,

        /// <summary>
        /// The color filter array (CFA) geometric pattern of the image sensor when a one-chip color 
        /// area sensor is used. It does not apply to all sensing methods.
        /// </summary>
        ExifCfaPattern = 0xA302,

        /// <summary>
        /// Color space specifier. Normally sRGB (=1) is used to define the color space 
        /// based on the PC monitor conditions and environment. If a color space other 
        /// than sRGB is used, Uncalibrated (=0xFFFF) is set. Image data recorded as 
        /// Uncalibrated can be treated as sRGB when it is converted to FlashPix.
        /// </summary>
        ExifColorSpace = 0xA001,

        /// <summary>
        /// Information specific to compressed data. The compression mode used for a 
        /// compressed image is indicated in unit BPP.
        /// </summary>
        ExifCompBPP = 0x9102,

        /// <summary>
        /// Information specific to compressed data. The channels of each component are 
        /// arranged in order from the first component to the fourth. For 
        /// uncompressed data, the data arrangement is given in the 
        /// PhotometricInterp tag.
        /// <para>
        /// However, because PhotometricInterp can only express the order of Y, Cb, and Cr, 
        /// this tag is provided for cases when compressed data uses components other than 
        /// Y, Cb, and Cr and to support other sequences.
        /// </para>
        /// </summary>
        ExifCompConfig = 0x9101,

        /// <summary>
        /// Date and time when the image was stored as digital data. If, for example, an image 
        /// was captured by DSC and at the same time the file was recorded, then DateTimeOriginal 
        /// and DateTimeDigitized will have the same contents.
        /// <para>
        /// The format is YYYY:MM:DD HH:MM:SS with time shown in 24-hour format and the date and 
        /// time separated by one blank character (0x2000). The character string length is 20 bytes 
        /// including the NULL terminator. When the field is empty, it is treated as unknown.
        /// </para>
        /// </summary>
        ExifDTDigitized = 0x9004,

        /// <summary>
        /// Null-terminated character string that specifies a fraction of a second for the ExifDTDigitized tag.
        /// </summary>
        ExifDTDigSS = 0x9292,

        /// <summary>
        /// Date and time when the original image data was generated. For a DSC, the date and time when the picture was taken. The format is YYYY:MM:DD HH:MM:SS with time shown in 24-hour format and the date and time separated by one blank character (0x2000). The character string length is 20 bytes including the NULL terminator. When the field is empty, it is treated as unknown.
        /// </summary>
        ExifDTOrig = 0x9003,

        /// <summary>
        /// Null-terminated character string that specifies a fraction of a second for the PropertyTagExifDTOrig tag.
        /// </summary>
        ExifDTOrigSS = 0x9291,

        /// <summary>
        /// Null-terminated character string that specifies a fraction of a second for the PropertyTagDateTime tag.
        /// </summary>
        ExifDTSubsec = 0x9290,

        /// <summary>
        /// Exposure bias. The unit is the APEX value. Ordinarily it is given in the range -99.99 to 99.99.
        /// </summary>
        ExifExposureBias = 0x9204,

        /// <summary>
        /// Exposure index selected on the camera or input device at the time the image was captured.
        /// </summary>
        ExifExposureIndex = 0xA215,

        /// <summary>
        /// Class of the program used by the camera to set exposure when the picture is taken.
        /// </summary>
        ExifExposureProg = 0x8822,

        /// <summary>
        /// Exposure time, measured in seconds.
        /// </summary>
        ExifExposureTime = 0x829A,

        /// <summary>
        /// The image source. If a DSC recorded the image, the value of this tag is 3.
        /// </summary>
        ExifFileSource = 0xA300,

        /// <summary>
        /// Flash status. This tag is recorded when an image is taken using a strobe light (flash). Bit 0 indicates the flash firing status, and bits 1 and 2 indicate the flash return status.
        /// </summary>
        ExifFlash = 0x9209,

        /// <summary>
        /// Strobe energy, in Beam Candle Power Seconds (BCPS), at the time the image was captured.
        /// </summary>
        ExifFlashEnergy = 0xA20B,

        /// <summary>
        /// F number.
        /// </summary>
        ExifFNumber = 0x829D,

        /// <summary>
        /// Actual focal length, in millimeters, of the lens. Conversion is not made to the focal length of a 35 millimeter film camera.
        /// </summary>
        ExifFocalLength = 0x920A,

        /// <summary>
        /// Unit of measure for PropertyTagExifFocalXRes and PropertyTagExifFocalYRes.
        /// </summary>
        ExifFocalResUnit = 0xA210,

        /// <summary>
        /// Number of pixels in the image width (x) direction per unit on the camera focal plane. The unit is specified in PropertyTagExifFocalResUnit.
        /// </summary>
        ExifFocalXRes = 0xA20E,

        /// <summary>
        /// Number of pixels in the image height (y) direction per unit on the camera focal plane. The unit is specified in PropertyTagExifFocalResUnit.
        /// </summary>
        ExifFocalYRes = 0xA20F,

        /// <summary>
        /// FlashPix format version supported by an FPXR file. If the FPXR function supports FlashPix format version 1.0, this is indicated similarly to PropertyTagExifVer by recording 0100 as a 4-byte ASCII string. Because the type is PropertyTagTypeUndefined, there is no NULL terminator.
        /// </summary>
        ExifFPXVer = 0xA000,

        /// <summary>
        /// Private tag used by GDI+. Not for public use. GDI+ uses this tag to locate Exif-specific information.
        /// </summary>
        ExifIFD = 0x8769,

        /// <summary>
        /// Offset to a block of property items that contain interoperability information.
        /// </summary>
        ExifInterop = 0xA005,

        /// <summary>
        /// ISO speed and ISO latitude of the camera or input device as specified in ISO 12232.
        /// </summary>
        ExifISOSpeed = 0x8827,

        /// <summary>
        /// Type of light source.
        /// </summary>
        ExifLightSource = 0x9208,

        /// <summary>
        /// Note tag. A tag used by manufacturers of EXIF writers to record information. The contents are up to the manufacturer.
        /// </summary>
        ExifMakerNote = 0x927C,

        /// <summary>
        /// Smallest F number of the lens. The unit is the APEX value. Ordinarily it is given in the range of 00.00 to 99.99, but it is not limited to this range.
        /// </summary>
        ExifMaxAperture = 0x9205,

        /// <summary>
        /// Metering mode.
        /// </summary>
        ExifMeteringMode = 0x9207,

        /// <summary>
        /// Optoelectronic conversion function (OECF) specified in ISO 14524. The OECF is the relationship between the camera optical input and the image values.
        /// </summary>
        ExifOECF = 0x8828,

        /// <summary>
        /// Information specific to compressed data. When a compressed file is recorded, the valid width of the meaningful image must be recorded in this tag, whether or not there is padding data or a restart marker. This tag should not exist in an uncompressed file.
        /// </summary>
        ExifPixXDim = 0xA002,

        /// <summary>
        /// Information specific to compressed data. When a compressed file is recorded, the valid height of the meaningful image must be recorded in this tag whether or not there is padding data or a restart marker. This tag should not exist in an uncompressed file. Because data padding is unnecessary in the vertical direction, the number of lines recorded in this valid image height tag will be the same as that recorded in the SOF.
        /// </summary>
        ExifPixYDim = 0xA003,

        /// <summary>
        /// The name of an audio file related to the image data. The only relational information recorded is the EXIF audio file name and extension (an ASCII string that consists of 8 characters plus a period (.), plus 3 characters). The path is not recorded. When you use this tag, audio files must be recorded in conformance with the EXIF audio format. Writers can also store audio data within APP2 as FlashPix extension stream data.
        /// </summary>
        ExifRelatedWav = 0xA004,

        /// <summary>
        /// The type of scene. If a DSC recorded the image, the value of this tag must be set to 1, indicating that the image was directly photographed.
        /// </summary>
        ExifSceneType = 0xA301,

        /// <summary>
        /// Image sensor type on the camera or input device.
        /// </summary>
        ExifSensingMethod = 0xA217,

        /// <summary>
        /// Shutter speed. The unit is the Additive System of Photographic Exposure (APEX) value.
        /// </summary>
        ExifShutterSpeed = 0x9201,

        /// <summary>
        /// Camera or input device spatial frequency table and SFR values in the image width, image height, and diagonal direction, as specified in ISO 12233.
        /// </summary>
        ExifSpatialFR = 0xA20C,

        /// <summary>
        /// Null-terminated character string that specifies the spectral sensitivity of each channel of the camera used. The string is compatible with the standard developed by the ASTM Technical Committee.
        /// </summary>
        ExifSpectralSense = 0x8824,

        /// <summary>
        /// Distance to the subject, measured in meters.
        /// </summary>
        ExifSubjectDist = 0x9206,

        /// <summary>
        /// Location of the main subject in the scene. The value of this tag represents the pixel at the center of the main subject relative to the left edge. The first value indicates the column number, and the second value indicates the row number.
        /// </summary>
        ExifSubjectLoc = 0xA214,

        /// <summary>
        /// Comment tag. A tag used by EXIF users to write keywords or comments about the image besides those in PropertyTagImageDescription and without the character-code limitations of the PropertyTagImageDescription tag.
        /// </summary>
        ExifUserComment = 0x9286,

        /// <summary>
        /// Version of the EXIF standard supported. Nonexistence of this field is taken to mean nonconformance to the standard. Conformance to the standard is indicated by recording 0210 as a 4-byte ASCII string. Because the type is PropertyTagTypeUndefined, there is no NULL terminator.
        /// </summary>
        ExifVer = 0x9000,

        /// <summary>
        /// Number of extra color components. For example, one extra component might hold an alpha value.
        /// </summary>
        ExtraSamples = 0x0152,

        /// <summary>
        /// Logical order of bits in a byte.
        /// </summary>
        FillOrder = 0x010A,

        /// <summary>
        /// Time delay, in hundredths of a second, between two frames in an animated GIF image.
        /// </summary>
        FrameDelay = 0x5100,

        /// <summary>
        /// For each string of contiguous unused bytes, the number of bytes in that string.
        /// </summary>
        FreeByteCounts = 0x0121,

        /// <summary>
        /// For each string of contiguous unused bytes, the byte offset of that string.
        /// </summary>
        FreeOffset = 0x0120,

        /// <summary>
        /// Gamma value attached to the image. The gamma value is stored as a rational number (pair of long) with a numerator of 100000. For example, a gamma value of 2.2 is stored as the pair (100000, 45455).
        /// </summary>
        Gamma = 0x0301,

        /// <summary>
        /// Color palette for an indexed bitmap in a GIF image.
        /// </summary>
        GlobalPalette = 0x5102,

        /// <summary>
        /// Altitude, in meters, based on the reference altitude specified by PropertyTagGpsAltitudeRef.
        /// </summary>
        GpsAltitude = 0x0006,

        /// <summary>
        /// Reference altitude, in meters.
        /// </summary>
        GpsAltitudeRef = 0x0005,

        /// <summary>
        /// Bearing to the destination point. The range of values is from 0.00 to 359.99.
        /// </summary>
        GpsDestBear = 0x0018,

        /// <summary>
        /// Null-terminated character string that specifies the reference used for giving the bearing to the destination point. T specifies true direction, and M specifies magnetic direction.
        /// </summary>
        GpsDestBearRef = 0x0017,

        /// <summary>
        /// Distance to the destination point.
        /// </summary>
        GpsDestDist = 0x001A,

        /// <summary>
        /// Null-terminated character string that specifies the unit used to express the distance to the destination point. K, M, and N represent kilometers, miles, and knots respectively.
        /// </summary>
        GpsDestDistRef = 0x0019,

        /// <summary>
        /// Latitude of the destination point. The latitude is expressed as three rational values giving the degrees, minutes, and seconds respectively. When degrees, minutes, and seconds are expressed, the format is dd/1, mm/1, ss/1. When degrees and minutes are used and, for example, fractions of minutes are given up to two decimal places, the format is dd/1, mmmm/100, 0/1.
        /// </summary>
        GpsDestLat = 0x0014,

        /// <summary>
        /// The gps dest lat ref.
        /// </summary>
        GpsDestLatRef = 0x0013,

        /// <summary>
        /// The gps dest long.
        /// </summary>
        GpsDestLong = 0x0016,

        /// <summary>
        /// The gps dest long ref.
        /// </summary>
        GpsDestLongRef = 0x0015,

        /// <summary>
        /// The gps gps dop.
        /// </summary>
        GpsGpsDop = 0x000B,

        /// <summary>
        /// The gps gps measure mode.
        /// </summary>
        GpsGpsMeasureMode = 0x000A,

        /// <summary>
        /// The gps gps satellites.
        /// </summary>
        GpsGpsSatellites = 0x0008,

        /// <summary>
        /// The gps gps status.
        /// </summary>
        GpsGpsStatus = 0x0009,

        /// <summary>
        /// The gps gps time.
        /// </summary>
        GpsGpsTime = 0x0007,

        /// <summary>
        /// The gps ifd.
        /// </summary>
        GpsIFD = 0x8825,

        /// <summary>
        /// The gps img dir.
        /// </summary>
        GpsImgDir = 0x0011,

        /// <summary>
        /// The gps img dir ref.
        /// </summary>
        GpsImgDirRef = 0x0010,

        /// <summary>
        /// The gps latitude.
        /// </summary>
        GpsLatitude = 0x0002,

        /// <summary>
        /// The gps latitude ref.
        /// </summary>
        GpsLatitudeRef = 0x0001,

        /// <summary>
        /// The gps longitude.
        /// </summary>
        GpsLongitude = 0x0004,

        /// <summary>
        /// The gps longitude ref.
        /// </summary>
        GpsLongitudeRef = 0x0003,

        /// <summary>
        /// The gps map datum.
        /// </summary>
        GpsMapDatum = 0x0012,

        /// <summary>
        /// The gps speed.
        /// </summary>
        GpsSpeed = 0x000D,

        /// <summary>
        /// The gps speed ref.
        /// </summary>
        GpsSpeedRef = 0x000C,

        /// <summary>
        /// The gps track.
        /// </summary>
        GpsTrack = 0x000F,

        /// <summary>
        /// The gps track ref.
        /// </summary>
        GpsTrackRef = 0x000E,

        /// <summary>
        /// The gps ver.
        /// </summary>
        GpsVer = 0x0000,

        /// <summary>
        /// The gray response curve.
        /// </summary>
        GrayResponseCurve = 0x0123,

        /// <summary>
        /// The gray response unit.
        /// </summary>
        GrayResponseUnit = 0x0122,

        /// <summary>
        /// The grid size.
        /// </summary>
        GridSize = 0x5011,

        /// <summary>
        /// The halftone degree.
        /// </summary>
        HalftoneDegree = 0x500C,

        /// <summary>
        /// The halftone hints.
        /// </summary>
        HalftoneHints = 0x0141,

        /// <summary>
        /// The halftone lpi.
        /// </summary>
        HalftoneLPI = 0x500A,

        /// <summary>
        /// The halftone lpi unit.
        /// </summary>
        HalftoneLPIUnit = 0x500B,

        /// <summary>
        /// The halftone misc.
        /// </summary>
        HalftoneMisc = 0x500E,

        /// <summary>
        /// The halftone screen.
        /// </summary>
        HalftoneScreen = 0x500F,

        /// <summary>
        /// The halftone shape.
        /// </summary>
        HalftoneShape = 0x500D,

        /// <summary>
        /// The host computer.
        /// </summary>
        HostComputer = 0x013C,

        /// <summary>
        /// The icc profile.
        /// </summary>
        ICCProfile = 0x8773,

        /// <summary>
        /// The icc profile descriptor.
        /// </summary>
        ICCProfileDescriptor = 0x0302,

        /// <summary>
        /// The image description.
        /// </summary>
        ImageDescription = 0x010E,

        /// <summary>
        /// The image height.
        /// </summary>
        ImageHeight = 0x0101,

        /// <summary>
        /// The image title.
        /// </summary>
        ImageTitle = 0x0320,

        /// <summary>
        /// The image width.
        /// </summary>
        ImageWidth = 0x0100,

        /// <summary>
        /// The index background.
        /// </summary>
        IndexBackground = 0x5103,

        /// <summary>
        /// The index transparent.
        /// </summary>
        IndexTransparent = 0x5104,

        /// <summary>
        /// The ink names.
        /// </summary>
        InkNames = 0x014D,

        /// <summary>
        /// The ink set.
        /// </summary>
        InkSet = 0x014C,

        /// <summary>
        /// The jpegac tables.
        /// </summary>
        JPEGACTables = 0x0209,

        /// <summary>
        /// The jpegdc tables.
        /// </summary>
        JPEGDCTables = 0x0208,

        /// <summary>
        /// The jpeg inter format.
        /// </summary>
        JPEGInterFormat = 0x0201,

        /// <summary>
        /// The jpeg inter length.
        /// </summary>
        JPEGInterLength = 0x0202,

        /// <summary>
        /// The jpeg lossless predictors.
        /// </summary>
        JPEGLosslessPredictors = 0x0205,

        /// <summary>
        /// The jpeg point transforms.
        /// </summary>
        JPEGPointTransforms = 0x0206,

        /// <summary>
        /// The jpeg proc.
        /// </summary>
        JPEGProc = 0x0200,

        /// <summary>
        /// The jpegq tables.
        /// </summary>
        JPEGQTables = 0x0207,

        /// <summary>
        /// The jpeg quality.
        /// </summary>
        JPEGQuality = 0x5010,

        /// <summary>
        /// The jpeg restart interval.
        /// </summary>
        JPEGRestartInterval = 0x0203,

        /// <summary>
        /// The loop count.
        /// </summary>
        LoopCount = 0x5101,

        /// <summary>
        /// The luminance table.
        /// </summary>
        LuminanceTable = 0x5090,

        /// <summary>
        /// The max sample value.
        /// </summary>
        MaxSampleValue = 0x0119,

        /// <summary>
        /// The min sample value.
        /// </summary>
        MinSampleValue = 0x0118,

        /// <summary>
        /// The new subfile type.
        /// </summary>
        NewSubfileType = 0x00FE,

        /// <summary>
        /// The number of inks.
        /// </summary>
        NumberOfInks = 0x014E,

        /// <summary>
        /// The orientation.
        /// </summary>
        Orientation = 0x0112,

        /// <summary>
        /// The page name.
        /// </summary>
        PageName = 0x011D,

        /// <summary>
        /// The page number.
        /// </summary>
        PageNumber = 0x0129,

        /// <summary>
        /// The palette histogram.
        /// </summary>
        PaletteHistogram = 0x5113,

        /// <summary>
        /// The photometric interp.
        /// </summary>
        PhotometricInterp = 0x0106,

        /// <summary>
        /// The pixel per unit x.
        /// </summary>
        PixelPerUnitX = 0x5111,

        /// <summary>
        /// The pixel per unit y.
        /// </summary>
        PixelPerUnitY = 0x5112,

        /// <summary>
        /// The pixel unit.
        /// </summary>
        PixelUnit = 0x5110,

        /// <summary>
        /// The planar config.
        /// </summary>
        PlanarConfig = 0x011C,

        /// <summary>
        /// The predictor.
        /// </summary>
        Predictor = 0x013D,

        /// <summary>
        /// The primary chromaticities.
        /// </summary>
        PrimaryChromaticities = 0x013F,

        /// <summary>
        /// The print flags.
        /// </summary>
        PrintFlags = 0x5005,

        /// <summary>
        /// The print flags bleed width.
        /// </summary>
        PrintFlagsBleedWidth = 0x5008,

        /// <summary>
        /// The print flags bleed width scale.
        /// </summary>
        PrintFlagsBleedWidthScale = 0x5009,

        /// <summary>
        /// The print flags crop.
        /// </summary>
        PrintFlagsCrop = 0x5007,

        /// <summary>
        /// The print flags version.
        /// </summary>
        PrintFlagsVersion = 0x5006,

        /// <summary>
        /// The ref black white.
        /// </summary>
        REFBlackWhite = 0x0214,

        /// <summary>
        /// The resolution unit.
        /// </summary>
        ResolutionUnit = 0x0128,

        /// <summary>
        /// The resolution x length unit.
        /// </summary>
        ResolutionXLengthUnit = 0x5003,

        /// <summary>
        /// The resolution x unit.
        /// </summary>
        ResolutionXUnit = 0x5001,

        /// <summary>
        /// The resolution y length unit.
        /// </summary>
        ResolutionYLengthUnit = 0x5004,

        /// <summary>
        /// The resolution y unit.
        /// </summary>
        ResolutionYUnit = 0x5002,

        /// <summary>
        /// The rows per strip.
        /// </summary>
        RowsPerStrip = 0x0116,

        /// <summary>
        /// The sample format.
        /// </summary>
        SampleFormat = 0x0153,

        /// <summary>
        /// The samples per pixel.
        /// </summary>
        SamplesPerPixel = 0x0115,

        /// <summary>
        /// The s max sample value.
        /// </summary>
        SMaxSampleValue = 0x0155,

        /// <summary>
        /// The s min sample value.
        /// </summary>
        SMinSampleValue = 0x0154,

        /// <summary>
        /// The software used.
        /// </summary>
        SoftwareUsed = 0x0131,

        /// <summary>
        /// The srgb rendering intent.
        /// </summary>
        SRGBRenderingIntent = 0x0303,

        /// <summary>
        /// The strip bytes count.
        /// </summary>
        StripBytesCount = 0x0117,

        /// <summary>
        /// The strip offsets.
        /// </summary>
        StripOffsets = 0x0111,

        /// <summary>
        /// The subfile type.
        /// </summary>
        SubfileType = 0x00FF,

        /// <summary>
        /// The t 4 option.
        /// </summary>
        T4Option = 0x0124,

        /// <summary>
        /// The t 6 option.
        /// </summary>
        T6Option = 0x0125,

        /// <summary>
        /// The target printer.
        /// </summary>
        TargetPrinter = 0x0151,

        /// <summary>
        /// The thresh holding.
        /// </summary>
        ThreshHolding = 0x0107,

        /// <summary>
        /// The thumbnail artist.
        /// </summary>
        ThumbnailArtist = 0x5034,

        /// <summary>
        /// The thumbnail bits per sample.
        /// </summary>
        ThumbnailBitsPerSample = 0x5022,

        /// <summary>
        /// The thumbnail color depth.
        /// </summary>
        ThumbnailColorDepth = 0x5015,

        /// <summary>
        /// The thumbnail compressed size.
        /// </summary>
        ThumbnailCompressedSize = 0x5019,

        /// <summary>
        /// The thumbnail compression.
        /// </summary>
        ThumbnailCompression = 0x5023,

        /// <summary>
        /// The thumbnail copy right.
        /// </summary>
        ThumbnailCopyRight = 0x503B,

        /// <summary>
        /// The thumbnail data.
        /// </summary>
        ThumbnailData = 0x501B,

        /// <summary>
        /// The thumbnail date time.
        /// </summary>
        ThumbnailDateTime = 0x5033,

        /// <summary>
        /// The thumbnail equip make.
        /// </summary>
        ThumbnailEquipMake = 0x5026,

        /// <summary>
        /// The thumbnail equip model.
        /// </summary>
        ThumbnailEquipModel = 0x5027,

        /// <summary>
        /// The thumbnail format.
        /// </summary>
        ThumbnailFormat = 0x5012,

        /// <summary>
        /// The thumbnail height.
        /// </summary>
        ThumbnailHeight = 0x5014,

        /// <summary>
        /// The thumbnail image description.
        /// </summary>
        ThumbnailImageDescription = 0x5025,

        /// <summary>
        /// The thumbnail image height.
        /// </summary>
        ThumbnailImageHeight = 0x5021,

        /// <summary>
        /// The thumbnail image width.
        /// </summary>
        ThumbnailImageWidth = 0x5020,

        /// <summary>
        /// The thumbnail orientation.
        /// </summary>
        ThumbnailOrientation = 0x5029,

        /// <summary>
        /// The thumbnail photometric interp.
        /// </summary>
        ThumbnailPhotometricInterp = 0x5024,

        /// <summary>
        /// The thumbnail planar config.
        /// </summary>
        ThumbnailPlanarConfig = 0x502F,

        /// <summary>
        /// The thumbnail planes.
        /// </summary>
        ThumbnailPlanes = 0x5016,

        /// <summary>
        /// The thumbnail primary chromaticities.
        /// </summary>
        ThumbnailPrimaryChromaticities = 0x5036,

        /// <summary>
        /// The thumbnail raw bytes.
        /// </summary>
        ThumbnailRawBytes = 0x5017,

        /// <summary>
        /// The thumbnail ref black white.
        /// </summary>
        ThumbnailRefBlackWhite = 0x503A,

        /// <summary>
        /// The thumbnail resolution unit.
        /// </summary>
        ThumbnailResolutionUnit = 0x5030,

        /// <summary>
        /// The thumbnail resolution x.
        /// </summary>
        ThumbnailResolutionX = 0x502D,

        /// <summary>
        /// The thumbnail resolution y.
        /// </summary>
        ThumbnailResolutionY = 0x502E,

        /// <summary>
        /// The thumbnail rows per strip.
        /// </summary>
        ThumbnailRowsPerStrip = 0x502B,

        /// <summary>
        /// The thumbnail samples per pixel.
        /// </summary>
        ThumbnailSamplesPerPixel = 0x502A,

        /// <summary>
        /// The thumbnail size.
        /// </summary>
        ThumbnailSize = 0x5018,

        /// <summary>
        /// The thumbnail software used.
        /// </summary>
        ThumbnailSoftwareUsed = 0x5032,

        /// <summary>
        /// The thumbnail strip bytes count.
        /// </summary>
        ThumbnailStripBytesCount = 0x502C,

        /// <summary>
        /// The thumbnail strip offsets.
        /// </summary>
        ThumbnailStripOffsets = 0x5028,

        /// <summary>
        /// The thumbnail transfer function.
        /// </summary>
        ThumbnailTransferFunction = 0x5031,

        /// <summary>
        /// The thumbnail white point.
        /// </summary>
        ThumbnailWhitePoint = 0x5035,

        /// <summary>
        /// The thumbnail width.
        /// </summary>
        ThumbnailWidth = 0x5013,

        /// <summary>
        /// The thumbnail y cb cr coefficients.
        /// </summary>
        ThumbnailYCbCrCoefficients = 0x5037,

        /// <summary>
        /// The thumbnail y cb cr positioning.
        /// </summary>
        ThumbnailYCbCrPositioning = 0x5039,

        /// <summary>
        /// The thumbnail y cb cr subsampling.
        /// </summary>
        ThumbnailYCbCrSubsampling = 0x5038,

        /// <summary>
        /// The tile byte counts.
        /// </summary>
        TileByteCounts = 0x0145,

        /// <summary>
        /// The tile length.
        /// </summary>
        TileLength = 0x0143,

        /// <summary>
        /// The tile offset.
        /// </summary>
        TileOffset = 0x0144,

        /// <summary>
        /// The tile width.
        /// </summary>
        TileWidth = 0x0142,

        /// <summary>
        /// The transfer function.
        /// </summary>
        TransferFunction = 0x012D,

        /// <summary>
        /// The transfer range.
        /// </summary>
        TransferRange = 0x0156,

        /// <summary>
        /// The white point.
        /// </summary>
        WhitePoint = 0x013E,

        /// <summary>
        /// The x position.
        /// </summary>
        XPosition = 0x011E,

        /// <summary>
        /// The x resolution.
        /// </summary>
        XResolution = 0x011A,

        /// <summary>
        /// The y cb cr coefficients.
        /// </summary>
        YCbCrCoefficients = 0x0211,

        /// <summary>
        /// The y cb cr positioning.
        /// </summary>
        YCbCrPositioning = 0x0213,

        /// <summary>
        /// The y cb cr subsampling.
        /// </summary>
        YCbCrSubsampling = 0x0212,

        /// <summary>
        /// The y position.
        /// </summary>
        YPosition = 0x011F,

        /// <summary>
        /// The y resolution.
        /// </summary>
        YResolution = 0x011B
    }
}
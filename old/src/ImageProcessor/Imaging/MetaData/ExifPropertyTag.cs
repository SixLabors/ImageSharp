// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExifPropertyTag.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The following enum gives descriptions of the property items supported by Windows GDI+.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.MetaData
{
    /// <summary>
    /// The following enum gives descriptions of the property items supported by Windows GDI+.
    /// <see href="http://msdn.microsoft.com/en-us/library/ms534417%28VS.85%29.aspx"/>
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
        /// Null-terminated character string that specifies a fraction of a second for the <see cref="ExifPropertyTag.ExifDTOrig"/> tag.
        /// </summary>
        ExifDTOrigSS = 0x9291,

        /// <summary>
        /// Null-terminated character string that specifies a fraction of a second for the <see cref="ExifPropertyTag.DateTime"/> tag.
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
        /// Unit of measure for <see cref="ExifPropertyTag.ExifFocalXRes"/> and <see cref="ExifPropertyTag.ExifFocalYRes"/>.
        /// </summary>
        ExifFocalResUnit = 0xA210,

        /// <summary>
        /// Number of pixels in the image width (x) direction per unit on the camera focal plane. The unit is specified 
        /// in <see cref="ExifPropertyTag.ExifFocalResUnit"/>.
        /// </summary>
        ExifFocalXRes = 0xA20E,

        /// <summary>
        /// Number of pixels in the image height (y) direction per unit on the camera focal plane. The unit is specified 
        /// in <see cref="ExifPropertyTag.ExifFocalResUnit"/>.
        /// </summary>
        ExifFocalYRes = 0xA20F,

        /// <summary>
        /// FlashPix format version supported by an FPXR file. If the FPXR function supports FlashPix format version 1.0, 
        /// this is indicated similarly to <see cref="ExifPropertyTag.ExifVer"/> by recording 0100 as a 4-byte ASCII string. 
        /// Because the type is <see cref="ExifPropertyTagType.Undefined"/>, there is no NULL terminator.
        /// </summary>
        ExifFPXVer = 0xA000,

        /// <summary>
        /// Private tag used by GDI+. Not for public use. GDI+ uses this tag to locate Exif specific information.
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
        /// Location of the main subject in the scene. The value of this tag represents the pixel at the center 
        /// of the main subject relative to the left edge. The first value indicates the column number, and 
        /// the second value indicates the row number.
        /// </summary>
        ExifSubjectLoc = 0xA214,

        /// <summary>
        /// Comment tag. A tag used by EXIF users to write keywords or comments about the image besides those 
        /// in <see cref="ExifPropertyTag.ImageDescription"/> and without the character-code limitations of 
        /// the <see cref="ExifPropertyTag.ImageDescription"/> tag.
        /// </summary>
        ExifUserComment = 0x9286,

        /// <summary>
        /// Version of the EXIF standard supported. Nonexistence of this field is taken to mean non-conformance to the 
        /// standard. Conformance to the standard is indicated by recording 0210 as a 4-byte ASCII string. 
        /// Because the type is <see cref="ExifPropertyTagType.Undefined"/>, there is no NULL terminator.
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
        /// Altitude, in meters, based on the reference altitude specified by <see cref="ExifPropertyTag.GpsAltitudeRef"/>.
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
        /// Null-terminated character string that specifies the unit used to express the distance to the destination point. 
        /// K, M, and N represent kilometers, miles, and knots respectively.
        /// </summary>
        GpsDestDistRef = 0x0019,

        /// <summary>
        /// Latitude of the destination point. The latitude is expressed as three rational values giving the degrees, 
        /// minutes, and seconds respectively. When degrees, minutes, and seconds are expressed, the format is 
        /// dd/1, mm/1, ss/1. When degrees and minutes are used and, for example, fractions of minutes are given up to 
        /// two decimal places, the format is dd/1, mmmm/100, 0/1.
        /// </summary>
        GpsDestLat = 0x0014,

        /// <summary>
        /// Null-terminated character string that specifies whether the latitude of the destination point is north or south 
        /// latitude. N specifies north latitude, and S specifies south latitude.
        /// </summary>
        GpsDestLatRef = 0x0013,

        /// <summary>
        /// Longitude of the destination point. The longitude is expressed as three rational values giving the degrees, minutes, and seconds respectively. When degrees, minutes, and seconds are expressed, the format is ddd/1, mm/1, ss/1. When degrees and minutes are used and, for example, fractions of minutes are given up to two decimal places, the format is ddd/1, mmmm/100, 0/1.
        /// </summary>
        GpsDestLong = 0x0016,

        /// <summary>
        /// Null-terminated character string that specifies whether the longitude of the destination point is east or west longitude. E specifies east longitude, and W specifies west longitude.
        /// </summary>
        GpsDestLongRef = 0x0015,

        /// <summary>
        /// GPS DOP (data degree of precision). An HDOP value is written during 2-D measurement, and a PDOP value is written during 3-D measurement.
        /// </summary>
        GpsGpsDop = 0x000B,

        /// <summary>
        /// Null-terminated character string that specifies the GPS measurement mode. 2 specifies 2-D measurement, and 3 specifies 3-D measurement.
        /// </summary>
        GpsGpsMeasureMode = 0x000A,

        /// <summary>
        /// Null-terminated character string that specifies the GPS satellites used for measurements. This tag can be used to specify the ID number, angle of elevation, azimuth, SNR, and other information about each satellite. The format is not specified. If the GPS receiver is incapable of taking measurements, the value of the tag must be set to NULL.
        /// </summary>
        GpsGpsSatellites = 0x0008,

        /// <summary>
        /// Null-terminated character string that specifies the status of the GPS receiver when the image is recorded. A means measurement is in progress, and V means the measurement is Interoperability.
        /// </summary>
        GpsGpsStatus = 0x0009,

        /// <summary>
        /// Time as coordinated universal time (UTC). The value is expressed as three rational numbers that give the hour, minute, and second.
        /// </summary>
        GpsGpsTime = 0x0007,

        /// <summary>
        /// Offset to a block of GPS property items. Property items whose tags have the prefix Gps are stored in the GPS block. 
        /// The GPS property items are defined in the EXIF specification. GDI+ uses this tag to locate GPS information, 
        /// but GDI+ does not expose this tag for public use.
        /// </summary>
        GpsIFD = 0x8825,

        /// <summary>
        /// Direction of the image when it was captured. The range of values is from 0.00 to 359.99.
        /// </summary>
        GpsImgDir = 0x0011,

        /// <summary>
        /// Null-terminated character string that specifies the reference for the direction of the image when it is captured. T specifies true direction, and M specifies magnetic direction.
        /// </summary>
        GpsImgDirRef = 0x0010,

        /// <summary>
        /// Latitude. Latitude is expressed as three rational values giving the degrees, minutes, and seconds respectively. When degrees, minutes, and seconds are expressed, the format is dd/1, mm/1, ss/1. When degrees and minutes are used and, for example, fractions of minutes are given up to two decimal places, the format is dd/1, mmmm/100, 0/1.
        /// </summary>
        GpsLatitude = 0x0002,

        /// <summary>
        /// Null-terminated character string that specifies whether the longitude is east or west longitude. 
        /// E specifies east longitude, and W specifies west longitude.
        /// </summary>
        GpsLatitudeRef = 0x0001,

        /// <summary>
        /// Longitude. Longitude is expressed as three rational values giving the degrees, minutes, and seconds 
        /// respectively. When degrees, minutes and seconds are expressed, the format is ddd/1, mm/1, ss/1. 
        /// When degrees and minutes are used and, for example, fractions of minutes are given up to two 
        /// decimal places, the format is ddd/1, mmmm/100, 0/1.
        /// </summary>
        GpsLongitude = 0x0004,

        /// <summary>
        /// Null-terminated character string that specifies whether the longitude is east or west longitude. 
        /// E specifies east longitude, and W specifies west longitude.
        /// </summary>
        GpsLongitudeRef = 0x0003,

        /// <summary>
        /// Null-terminated character string that specifies geodetic survey data used by the GPS receiver. 
        /// If the survey data is restricted to Japan, the value of this tag is TOKYO or WGS-84.
        /// </summary>
        GpsMapDatum = 0x0012,

        /// <summary>
        /// Speed of the GPS receiver movement.
        /// </summary>
        GpsSpeed = 0x000D,

        /// <summary>
        /// Null-terminated character string that specifies the unit used to express the GPS receiver speed of movement. 
        /// K, M, and N represent kilometers per hour, miles per hour, and knots respectively.
        /// </summary>
        GpsSpeedRef = 0x000C,

        /// <summary>
        /// Direction of GPS receiver movement. The range of values is from 0.00 to 359.99.
        /// </summary>
        GpsTrack = 0x000F,

        /// <summary>
        /// Null-terminated character string that specifies the reference for giving the direction of 
        /// GPS receiver movement. T specifies true direction, and M specifies magnetic direction.
        /// </summary>
        GpsTrackRef = 0x000E,

        /// <summary>
        /// Version of the Global Positioning Systems (GPS) IFD, given as 2.0.0.0. This tag is mandatory when 
        /// the <see cref="ExifPropertyTag.GpsIFD"/> tag is present. When the version is 2.0.0.0, the tag value is 0x02000000.
        /// </summary>
        GpsVer = 0x0000,

        /// <summary>
        /// For each possible pixel value in a grayscale image, the optical density of that pixel value.
        /// </summary>
        GrayResponseCurve = 0x0123,

        /// <summary>
        /// Precision of the number specified by <see cref="ExifPropertyTag.GrayResponseCurve"/>. 1 specifies tenths, 
        /// 2 specifies hundredths, 3 specifies thousandths, and so on.
        /// </summary>
        GrayResponseUnit = 0x0122,

        /// <summary>
        /// Block of information about grids and guides.
        /// </summary>
        GridSize = 0x5011,

        /// <summary>
        /// Angle for screen.
        /// </summary>
        HalftoneDegree = 0x500C,

        /// <summary>
        /// Information used by the halftone function.
        /// </summary>
        HalftoneHints = 0x0141,

        /// <summary>
        /// Ink's screen frequency, in lines per inch.
        /// </summary>
        HalftoneLPI = 0x500A,

        /// <summary>
        /// Units for the screen frequency.
        /// </summary>
        HalftoneLPIUnit = 0x500B,

        /// <summary>
        /// Miscellaneous halftone information.
        /// </summary>
        HalftoneMisc = 0x500E,

        /// <summary>
        /// Boolean value that specifies whether to use the printer's default screens.
        /// </summary>
        HalftoneScreen = 0x500F,

        /// <summary>
        /// Shape of the halftone dots.
        /// </summary>
        HalftoneShape = 0x500D,

        /// <summary>
        /// Null-terminated character string that specifies the computer and/or operating system used to create the image.
        /// </summary>
        HostComputer = 0x013C,

        /// <summary>
        /// ICC profile embedded in the image.
        /// </summary>
        ICCProfile = 0x8773,

        /// <summary>
        /// Null-terminated character string that identifies an ICC profile.
        /// </summary>
        ICCProfileDescriptor = 0x0302,

        /// <summary>
        /// Null-terminated character string that specifies the title of the image.
        /// </summary>
        ImageDescription = 0x010E,

        /// <summary>
        /// Number of pixel rows.
        /// </summary>
        ImageHeight = 0x0101,

        /// <summary>
        /// Null-terminated character string that specifies the title of the image.
        /// </summary>
        ImageTitle = 0x0320,

        /// <summary>
        /// Number of pixels per row.
        /// </summary>
        ImageWidth = 0x0100,

        /// <summary>
        /// Index of the background color in the palette of a GIF image.
        /// </summary>
        IndexBackground = 0x5103,

        /// <summary>
        /// Index of the transparent color in the palette of a GIF image.
        /// </summary>
        IndexTransparent = 0x5104,

        /// <summary>
        /// Sequence of concatenated, null-terminated, character strings that specify the names of the 
        /// inks used in a separated image.
        /// </summary>
        InkNames = 0x014D,

        /// <summary>
        /// Set of inks used in a separated image.
        /// </summary>
        InkSet = 0x014C,

        /// <summary>
        /// For each color component, the offset to the AC Huffman table for that component. 
        /// See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        JPEGACTables = 0x0209,

        /// <summary>
        /// For each color component, the offset to the DC Huffman table (or lossless Huffman table) for that 
        /// component. See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        JPEGDCTables = 0x0208,

        /// <summary>
        /// Offset to the start of a JPEG bitstream.
        /// </summary>
        JPEGInterFormat = 0x0201,

        /// <summary>
        /// Length, in bytes, of the JPEG bitstream.
        /// </summary>
        JPEGInterLength = 0x0202,

        /// <summary>
        /// For each color component, a lossless predictor-selection value for that component. 
        /// See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        JPEGLosslessPredictors = 0x0205,

        /// <summary>
        /// For each color component, a point transformation value for that component. 
        /// See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        JPEGPointTransforms = 0x0206,

        /// <summary>
        /// JPEG compression process.
        /// </summary>
        JPEGProc = 0x0200,

        /// <summary>
        /// For each color component, the offset to the quantization table for that 
        /// component. See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        JPEGQTables = 0x0207,

        /// <summary>
        /// Private tag used by the Adobe Photoshop format. Not for public use.
        /// </summary>
        JPEGQuality = 0x5010,

        /// <summary>
        /// Length of the restart interval.
        /// </summary>
        JPEGRestartInterval = 0x0203,

        /// <summary>
        /// For an animated GIF image, the number of times to display the animation. 
        /// A value of 0 specifies that the animation should be displayed infinitely.
        /// </summary>
        LoopCount = 0x5101,

        /// <summary>
        /// Luminance table. The luminance table and the chrominance table are used to control JPEG quality. 
        /// A valid luminance or chrominance table has 64 entries of type <see cref="ExifPropertyTagType.Int16"/>. 
        /// If an image has either a luminance table or a chrominance table, then it must have both tables.
        /// </summary>
        LuminanceTable = 0x5090,

        /// <summary>
        /// For each color component, the maximum value assigned to that component. 
        /// See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        MaxSampleValue = 0x0119,

        /// <summary>
        /// For each color component, the minimum value assigned to that component. 
        /// See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        MinSampleValue = 0x0118,

        /// <summary>
        /// Type of data in a subfile.
        /// </summary>
        NewSubfileType = 0x00FE,

        /// <summary>
        /// The number of inks.
        /// </summary>
        NumberOfInks = 0x014E,

        /// <summary>
        /// Image orientation viewed in terms of rows and columns.
        /// </summary>
        Orientation = 0x0112,

        /// <summary>
        /// Null-terminated character string that specifies the name of the page from which the image was scanned.
        /// </summary>
        PageName = 0x011D,

        /// <summary>
        /// Page number of the page from which the image was scanned.
        /// </summary>
        PageNumber = 0x0129,

        /// <summary>
        /// The palette histogram.
        /// </summary>
        PaletteHistogram = 0x5113,

        /// <summary>
        /// How pixel data will be interpreted.
        /// </summary>
        PhotometricInterp = 0x0106,

        /// <summary>
        /// Pixels per unit in the x direction.
        /// </summary>
        PixelPerUnitX = 0x5111,

        /// <summary>
        /// Pixels per unit in the y direction.
        /// </summary>
        PixelPerUnitY = 0x5112,

        /// <summary>
        /// Unit for <see cref="ExifPropertyTag.PixelPerUnitX"/> and <see cref="ExifPropertyTag.PixelPerUnitY"/>.
        /// </summary>
        PixelUnit = 0x5110,

        /// <summary>
        /// Whether pixel components are recorded in chunky or planar format.
        /// </summary>
        PlanarConfig = 0x011C,

        /// <summary>
        /// TType of prediction scheme that was applied to the image data before the encoding scheme was applied.
        /// </summary>
        Predictor = 0x013D,

        /// <summary>
        /// For each of the three primary colors in the image, the chromaticity of that color.
        /// </summary>
        PrimaryChromaticities = 0x013F,

        /// <summary>
        /// Sequence of one-byte Boolean values that specify printing options.
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
        /// The print flags crop marks.
        /// </summary>
        PrintFlagsCrop = 0x5007,

        /// <summary>
        /// The print flags version.
        /// </summary>
        PrintFlagsVersion = 0x5006,

        /// <summary>
        /// Reference black point value and reference white point value.
        /// </summary>
        REFBlackWhite = 0x0214,

        /// <summary>
        /// Unit of measure for the horizontal resolution and the vertical resolution.
        /// </summary>
        ResolutionUnit = 0x0128,

        /// <summary>
        /// Units in which to display the image width.
        /// </summary>
        ResolutionXLengthUnit = 0x5003,

        /// <summary>
        /// Units in which to display horizontal resolution.
        /// </summary>
        ResolutionXUnit = 0x5001,

        /// <summary>
        /// Units in which to display the image height.
        /// </summary>
        ResolutionYLengthUnit = 0x5004,

        /// <summary>
        /// Units in which to display vertical resolution.
        /// </summary>
        ResolutionYUnit = 0x5002,

        /// <summary>
        /// Number of rows per strip. See also <see cref="ExifPropertyTag.StripBytesCount"/> and <see cref="ExifPropertyTag.StripOffsets"/>.
        /// </summary>
        RowsPerStrip = 0x0116,

        /// <summary>
        /// For each color component, the numerical format (unsigned, signed, floating point) of that component. 
        /// See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        SampleFormat = 0x0153,

        /// <summary>
        /// Number of color components per pixel.
        /// </summary>
        SamplesPerPixel = 0x0115,

        /// <summary>
        /// For each color component, the maximum value of that component. See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        SMaxSampleValue = 0x0155,

        /// <summary>
        /// For each color component, the minimum value of that component. See also <see cref="ExifPropertyTag.SamplesPerPixel"/>.
        /// </summary>
        SMinSampleValue = 0x0154,

        /// <summary>
        /// Null-terminated character string that specifies the name and version of the software or 
        /// firmware of the device used to generate the image.
        /// </summary>
        SoftwareUsed = 0x0131,

        /// <summary>
        /// How the image should be displayed as defined by the International Color Consortium (ICC). If a GDI+ Image object
        /// is constructed with the useEmbeddedColorManagement parameter set to TRUE, then GDI+ renders the image 
        /// according to the specified rendering intent. The intent can be set to perceptual, relative colorimetric, 
        /// saturation, or absolute colorimetric.
        /// </summary>
        SRGBRenderingIntent = 0x0303,

        /// <summary>
        /// For each strip, the total number of bytes in that strip.
        /// </summary>
        StripBytesCount = 0x0117,

        /// <summary>
        /// For each strip, the byte offset of that strip. See also <see cref="ExifPropertyTag.RowsPerStrip"/> 
        /// and <see cref="ExifPropertyTag.StripBytesCount"/>.
        /// </summary>
        StripOffsets = 0x0111,

        /// <summary>
        /// The type of data in a subfile.
        /// </summary>
        SubfileType = 0x00FF,

        /// <summary>
        /// Set of flags that relate to T4 encoding.
        /// </summary>
        T4Option = 0x0124,

        /// <summary>
        /// Set of flags that relate to T6 encoding.
        /// </summary>
        T6Option = 0x0125,

        /// <summary>
        /// Null-terminated character string that describes the intended printing environment.
        /// </summary>
        TargetPrinter = 0x0151,

        /// <summary>
        /// Technique used to convert from gray pixels to black and white pixels.
        /// </summary>
        ThreshHolding = 0x0107,

        /// <summary>
        /// Null-terminated character string that specifies the name of the person who created the thumbnail image.
        /// </summary>
        ThumbnailArtist = 0x5034,

        /// <summary>
        /// Number of bits per color component in the thumbnail image. See also <see cref="ExifPropertyTag.ThumbnailSamplesPerPixel"/>.
        /// </summary>
        ThumbnailBitsPerSample = 0x5022,

        /// <summary>
        /// Bits per pixel (BPP) for the thumbnail image.
        /// </summary>
        ThumbnailColorDepth = 0x5015,

        /// <summary>
        /// Compressed size, in bytes, of the thumbnail image.
        /// </summary>
        ThumbnailCompressedSize = 0x5019,

        /// <summary>
        /// Compression scheme used for thumbnail image data.
        /// </summary>
        ThumbnailCompression = 0x5023,

        /// <summary>
        /// Null-terminated character string that contains copyright information for the thumbnail image.
        /// </summary>
        ThumbnailCopyRight = 0x503B,

        /// <summary>
        /// Raw thumbnail bits in JPEG or RGB format. Depends on <see cref="ExifPropertyTag.ThumbnailFormat"/>.
        /// </summary>
        ThumbnailData = 0x501B,

        /// <summary>
        /// Date and time the thumbnail image was created. See also <see cref="ExifPropertyTag.DateTime"/>.
        /// </summary>
        ThumbnailDateTime = 0x5033,

        /// <summary>
        /// Null-terminated character string that specifies the manufacturer of the equipment used to 
        /// record the thumbnail image.
        /// </summary>
        ThumbnailEquipMake = 0x5026,

        /// <summary>
        /// Null-terminated character string that specifies the model name or model number of the 
        /// equipment used to record the thumbnail image.
        /// </summary>
        ThumbnailEquipModel = 0x5027,

        /// <summary>
        /// Format of the thumbnail image.
        /// </summary>
        ThumbnailFormat = 0x5012,

        /// <summary>
        /// Height, in pixels, of the thumbnail image.
        /// </summary>
        ThumbnailHeight = 0x5014,

        /// <summary>
        /// Null-terminated character string that specifies the title of the image.
        /// </summary>
        ThumbnailImageDescription = 0x5025,

        /// <summary>
        /// Number of pixel rows in the thumbnail image.
        /// </summary>
        ThumbnailImageHeight = 0x5021,

        /// <summary>
        /// Number of pixels per row in the thumbnail image.
        /// </summary>
        ThumbnailImageWidth = 0x5020,

        /// <summary>
        /// Thumbnail image orientation in terms of rows and columns. See also <see cref="ExifPropertyTag.Orientation"/>.
        /// </summary>
        ThumbnailOrientation = 0x5029,

        /// <summary>
        /// How thumbnail pixel data will be interpreted.
        /// </summary>
        ThumbnailPhotometricInterp = 0x5024,

        /// <summary>
        /// Whether pixel components in the thumbnail image are recorded in chunky or planar format. 
        /// See also <see cref="ExifPropertyTag.PlanarConfig"/>.
        /// </summary>
        ThumbnailPlanarConfig = 0x502F,

        /// <summary>
        /// Number of color planes for the thumbnail image.
        /// </summary>
        ThumbnailPlanes = 0x5016,

        /// <summary>
        /// For each of the three primary colors in the thumbnail image, the chromaticity 
        /// of that color. See also <see cref="ExifPropertyTag.PrimaryChromaticities"/>.
        /// </summary>
        ThumbnailPrimaryChromaticities = 0x5036,

        /// <summary>
        /// Byte offset between rows of pixel data.
        /// </summary>
        ThumbnailRawBytes = 0x5017,

        /// <summary>
        /// Reference black point value and reference white point value 
        /// for the thumbnail image. See also <see cref="ExifPropertyTag.REFBlackWhite"/>.
        /// </summary>
        ThumbnailRefBlackWhite = 0x503A,

        /// <summary>
        /// Unit of measure for the horizontal resolution and the vertical resolution of 
        /// the thumbnail image. See also <see cref="ExifPropertyTag.ResolutionUnit"/>.
        /// </summary>
        ThumbnailResolutionUnit = 0x5030,

        /// <summary>
        /// Thumbnail resolution in the width direction. 
        /// The resolution unit is given in <see cref="ExifPropertyTag.ThumbnailResolutionUnit"/>.
        /// </summary>
        ThumbnailResolutionX = 0x502D,

        /// <summary>
        /// Thumbnail resolution in the height direction. The resolution unit is given 
        /// in <see cref="ExifPropertyTag.ThumbnailResolutionUnit"/>.
        /// </summary>
        ThumbnailResolutionY = 0x502E,

        /// <summary>
        /// Number of rows per strip in the thumbnail image. See also <see cref="ExifPropertyTag.ThumbnailStripBytesCount"/> 
        /// and <see cref="ExifPropertyTag.ThumbnailStripOffsets"/>.
        /// </summary>
        ThumbnailRowsPerStrip = 0x502B,

        /// <summary>
        /// Number of color components per pixel in the thumbnail image.
        /// </summary>
        ThumbnailSamplesPerPixel = 0x502A,

        /// <summary>
        /// Total size, in bytes, of the thumbnail image.
        /// </summary>
        ThumbnailSize = 0x5018,

        /// <summary>
        /// Null-terminated character string that specifies the name and version of the software 
        /// or firmware of the device used to generate the thumbnail image.
        /// </summary>
        ThumbnailSoftwareUsed = 0x5032,

        /// <summary>
        /// For each thumbnail image strip, the total number of bytes in that strip.
        /// </summary>
        ThumbnailStripBytesCount = 0x502C,

        /// <summary>
        /// For each strip in the thumbnail image, the byte offset of that strip. See also <see cref="ExifPropertyTag.ThumbnailRowsPerStrip"/> 
        /// and <see cref="ExifPropertyTag.ThumbnailStripBytesCount"/>.
        /// </summary>
        ThumbnailStripOffsets = 0x5028,

        /// <summary>
        /// Tables that specify transfer functions for the thumbnail image. See also <see cref="ExifPropertyTag.TransferFunction"/>.
        /// </summary>
        ThumbnailTransferFunction = 0x5031,

        /// <summary>
        /// Chromaticity of the white point of the thumbnail image. See also <see cref="ExifPropertyTag.WhitePoint"/>.
        /// </summary>
        ThumbnailWhitePoint = 0x5035,

        /// <summary>
        /// Width, in pixels, of the thumbnail image.
        /// </summary>
        ThumbnailWidth = 0x5013,

        /// <summary>
        /// Coefficients for transformation from RGB to YCbCr data for the thumbnail image. 
        /// See also <see cref="ExifPropertyTag.YCbCrCoefficients"/>.
        /// </summary>
        ThumbnailYCbCrCoefficients = 0x5037,

        /// <summary>
        /// Position of chrominance components in relation to the luminance component for 
        /// the thumbnail image. See also <see cref="ExifPropertyTag.YCbCrPositioning"/>.
        /// </summary>
        ThumbnailYCbCrPositioning = 0x5039,

        /// <summary>
        /// Sampling ratio of chrominance components in relation to the luminance component for 
        /// the thumbnail image. See also <see cref="ExifPropertyTag.YCbCrSubsampling"/>.
        /// </summary>
        ThumbnailYCbCrSubsampling = 0x5038,

        /// <summary>
        /// For each tile, the number of bytes in that tile.
        /// </summary>
        TileByteCounts = 0x0145,

        /// <summary>
        /// Number of pixel rows in each tile.
        /// </summary>
        TileLength = 0x0143,

        /// <summary>
        /// For each tile, the byte offset of that tile.
        /// </summary>
        TileOffset = 0x0144,

        /// <summary>
        /// Number of pixel columns in each tile.
        /// </summary>
        TileWidth = 0x0142,

        /// <summary>
        /// Tables that specify transfer functions for the image.
        /// </summary>
        TransferFunction = 0x012D,

        /// <summary>
        /// Table of values that extends the range of the transfer function.
        /// </summary>
        TransferRange = 0x0156,

        /// <summary>
        /// Chromaticity of the white point of the image.
        /// </summary>
        WhitePoint = 0x013E,

        /// <summary>
        /// Offset from the left side of the page to the left side of the image. 
        /// The unit of measure is specified by <see cref="ExifPropertyTag.ResolutionUnit"/>.
        /// </summary>
        XPosition = 0x011E,

        /// <summary>
        /// Number of pixels per unit in the image width (x) direction. 
        /// The unit is specified by <see cref="ExifPropertyTag.ResolutionUnit"/>.
        /// </summary>
        XResolution = 0x011A,

        /// <summary>
        /// Coefficients for transformation from RGB to YCbCr image data.
        /// </summary>
        YCbCrCoefficients = 0x0211,

        /// <summary>
        /// Position of chrominance components in relation to the luminance component.
        /// </summary>
        YCbCrPositioning = 0x0213,

        /// <summary>
        /// Sampling ratio of chrominance components in relation to the luminance component.
        /// </summary>
        YCbCrSubsampling = 0x0212,

        /// <summary>
        /// Offset from the top of the page to the top of the image. The unit of measure 
        /// is specified by <see cref="ExifPropertyTag.ResolutionUnit"/>.
        /// </summary>
        YPosition = 0x011F,

        /// <summary>
        /// Number of pixels per unit in the image height (y) direction. The unit is specified 
        /// by <see cref="ExifPropertyTag.ResolutionUnit"/>.
        /// </summary>
        YResolution = 0x011B
    }
}
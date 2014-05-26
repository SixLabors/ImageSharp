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
        /// The color transfer function.
        /// </summary>
        ColorTransferFunction = 0x501A,

        /// <summary>
        /// The compression.
        /// </summary>
        Compression = 0x0103,

        /// <summary>
        /// The copyright.
        /// </summary>
        Copyright = 0x8298,

        /// <summary>
        /// The date time.
        /// </summary>
        DateTime = 0x0132,

        /// <summary>
        /// The document name.
        /// </summary>
        DocumentName = 0x010D,

        /// <summary>
        /// The dot range.
        /// </summary>
        DotRange = 0x0150,

        /// <summary>
        /// The equip make.
        /// </summary>
        EquipMake = 0x010F,

        /// <summary>
        /// The equip model.
        /// </summary>
        EquipModel = 0x0110,

        /// <summary>
        /// The exif aperture.
        /// </summary>
        ExifAperture = 0x9202,

        /// <summary>
        /// The exif brightness.
        /// </summary>
        ExifBrightness = 0x9203,

        /// <summary>
        /// The exif cfa pattern.
        /// </summary>
        ExifCfaPattern = 0xA302,

        /// <summary>
        /// The exif color space.
        /// </summary>
        ExifColorSpace = 0xA001,

        /// <summary>
        /// The exif comp bpp.
        /// </summary>
        ExifCompBPP = 0x9102,

        /// <summary>
        /// The exif comp config.
        /// </summary>
        ExifCompConfig = 0x9101,

        /// <summary>
        /// The exif dt digitized.
        /// </summary>
        ExifDTDigitized = 0x9004,

        /// <summary>
        /// The exif dt dig ss.
        /// </summary>
        ExifDTDigSS = 0x9292,

        /// <summary>
        /// The exif dt orig.
        /// </summary>
        ExifDTOrig = 0x9003,

        /// <summary>
        /// The exif dt orig ss.
        /// </summary>
        ExifDTOrigSS = 0x9291,

        /// <summary>
        /// The exif dt subsec.
        /// </summary>
        ExifDTSubsec = 0x9290,

        /// <summary>
        /// The exif exposure bias.
        /// </summary>
        ExifExposureBias = 0x9204,

        /// <summary>
        /// The exif exposure index.
        /// </summary>
        ExifExposureIndex = 0xA215,

        /// <summary>
        /// The exif exposure prog.
        /// </summary>
        ExifExposureProg = 0x8822,

        /// <summary>
        /// The exif exposure time.
        /// </summary>
        ExifExposureTime = 0x829A,

        /// <summary>
        /// The exif file source.
        /// </summary>
        ExifFileSource = 0xA300,

        /// <summary>
        /// The exif flash.
        /// </summary>
        ExifFlash = 0x9209,

        /// <summary>
        /// The exif flash energy.
        /// </summary>
        ExifFlashEnergy = 0xA20B,

        /// <summary>
        /// The exif f number.
        /// </summary>
        ExifFNumber = 0x829D,

        /// <summary>
        /// The exif focal length.
        /// </summary>
        ExifFocalLength = 0x920A,

        /// <summary>
        /// The exif focal res unit.
        /// </summary>
        ExifFocalResUnit = 0xA210,

        /// <summary>
        /// The exif focal x res.
        /// </summary>
        ExifFocalXRes = 0xA20E,

        /// <summary>
        /// The exif focal y res.
        /// </summary>
        ExifFocalYRes = 0xA20F,

        /// <summary>
        /// The exif fpx ver.
        /// </summary>
        ExifFPXVer = 0xA000,

        /// <summary>
        /// The exif ifd.
        /// </summary>
        ExifIFD = 0x8769,

        /// <summary>
        /// The exif interop.
        /// </summary>
        ExifInterop = 0xA005,

        /// <summary>
        /// The exif iso speed.
        /// </summary>
        ExifISOSpeed = 0x8827,

        /// <summary>
        /// The exif light source.
        /// </summary>
        ExifLightSource = 0x9208,

        /// <summary>
        /// The exif maker note.
        /// </summary>
        ExifMakerNote = 0x927C,

        /// <summary>
        /// The exif max aperture.
        /// </summary>
        ExifMaxAperture = 0x9205,

        /// <summary>
        /// The exif metering mode.
        /// </summary>
        ExifMeteringMode = 0x9207,

        /// <summary>
        /// The exif oecf.
        /// </summary>
        ExifOECF = 0x8828,

        /// <summary>
        /// The exif pix x dim.
        /// </summary>
        ExifPixXDim = 0xA002,

        /// <summary>
        /// The exif pix y dim.
        /// </summary>
        ExifPixYDim = 0xA003,

        /// <summary>
        /// The exif related wav.
        /// </summary>
        ExifRelatedWav = 0xA004,

        /// <summary>
        /// The exif scene type.
        /// </summary>
        ExifSceneType = 0xA301,

        /// <summary>
        /// The exif sensing method.
        /// </summary>
        ExifSensingMethod = 0xA217,

        /// <summary>
        /// The exif shutter speed.
        /// </summary>
        ExifShutterSpeed = 0x9201,

        /// <summary>
        /// The exif spatial fr.
        /// </summary>
        ExifSpatialFR = 0xA20C,

        /// <summary>
        /// The exif spectral sense.
        /// </summary>
        ExifSpectralSense = 0x8824,

        /// <summary>
        /// The exif subject dist.
        /// </summary>
        ExifSubjectDist = 0x9206,

        /// <summary>
        /// The exif subject loc.
        /// </summary>
        ExifSubjectLoc = 0xA214,

        /// <summary>
        /// The exif user comment.
        /// </summary>
        ExifUserComment = 0x9286,

        /// <summary>
        /// The exif ver.
        /// </summary>
        ExifVer = 0x9000,

        /// <summary>
        /// The extra samples.
        /// </summary>
        ExtraSamples = 0x0152,

        /// <summary>
        /// The fill order.
        /// </summary>
        FillOrder = 0x010A,

        /// <summary>
        /// The frame delay.
        /// </summary>
        FrameDelay = 0x5100,

        /// <summary>
        /// The free byte counts.
        /// </summary>
        FreeByteCounts = 0x0121,

        /// <summary>
        /// The free offset.
        /// </summary>
        FreeOffset = 0x0120,

        /// <summary>
        /// The gamma.
        /// </summary>
        Gamma = 0x0301,

        /// <summary>
        /// The global palette.
        /// </summary>
        GlobalPalette = 0x5102,

        /// <summary>
        /// The gps altitude.
        /// </summary>
        GpsAltitude = 0x0006,

        /// <summary>
        /// The gps altitude ref.
        /// </summary>
        GpsAltitudeRef = 0x0005,

        /// <summary>
        /// The gps dest bear.
        /// </summary>
        GpsDestBear = 0x0018,

        /// <summary>
        /// The gps dest bear ref.
        /// </summary>
        GpsDestBearRef = 0x0017,

        /// <summary>
        /// The gps dest dist.
        /// </summary>
        GpsDestDist = 0x001A,

        /// <summary>
        /// The gps dest dist ref.
        /// </summary>
        GpsDestDistRef = 0x0019,

        /// <summary>
        /// The gps dest lat.
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
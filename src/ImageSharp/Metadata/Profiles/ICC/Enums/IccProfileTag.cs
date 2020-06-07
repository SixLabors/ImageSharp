// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Enumerates the ICC Profile Tags as defined in ICC.1:2010 version 4.3.0.0
    /// <see href="http://www.color.org/specification/ICC1v43_2010-12.pdf"/> Section 9
    /// <remarks>
    /// Each tag value represent the size of the tag in the profile.
    /// </remarks>
    /// </summary>
    public enum IccProfileTag : uint
    {
        /// <summary>
        /// Unknown tag
        /// </summary>
        Unknown,

        /// <summary>
        /// A2B0 - This tag defines a color transform from Device, Color Encoding or PCS, to PCS, or a color transform
        /// from Device 1 to Device 2, using lookup table tag element structures
        /// </summary>
        AToB0 = 0x41324230,

        /// <summary>
        /// A2B2 - This tag describes the color transform from Device or Color Encoding to PCS using lookup table tag element structures
        /// </summary>
        AToB1 = 0x41324231,

        /// <summary>
        /// A2B2 - This tag describes the color transform from Device or Color Encoding to PCS using lookup table tag element structures
        /// </summary>
        AToB2 = 0x41324232,

        /// <summary>
        /// bXYZ - This tag contains the third column in the matrix used in matrix/TRC transforms.
        /// </summary>
        BlueMatrixColumn = 0x6258595A,

        /// <summary>
        /// bTRC - This tag contains the blue channel tone reproduction curve. The first element represents no colorant (white) or
        /// phosphor (black) and the last element represents 100 % colorant (blue) or 100 % phosphor (blue).
        /// </summary>
        BlueTrc = 0x62545243,

        /// <summary>
        /// B2A0 - This tag defines a color transform from PCS to Device or Color Encoding using the lookup table tag element structures
        /// </summary>
        BToA0 = 0x42324130,

        /// <summary>
        /// B2A1 - This tag defines a color transform from PCS to Device or Color Encoding using the lookup table tag element structures.
        /// </summary>
        BToA1 = 0x42324131,

        /// <summary>
        /// B2A2 - This tag defines a color transform from PCS to Device or Color Encoding using the lookup table tag element structures.
        /// </summary>
        BToA2 = 0x42324132,

        /// <summary>
        /// B2D0 - This tag defines a color transform from PCS to Device. It supports float32Number-encoded input range, output range and transform, and
        /// provides a means to override the BToA0 tag.
        /// </summary>
        BToD0 = 0x42324430,

        /// <summary>
        /// B2D1 - This tag defines a color transform from PCS to Device. It supports float32Number-encoded input range, output range and transform, and
        /// provides a means to override the BToA1 tag.
        /// </summary>
        BToD1 = 0x42324431,

        /// <summary>
        /// B2D2 - This tag defines a color transform from PCS to Device. It supports float32Number-encoded input range, output range and transform, and
        /// provides a means to override the BToA2 tag.
        /// </summary>
        BToD2 = 0x42324432,

        /// <summary>
        /// B2D3 - This tag defines a color transform from PCS to Device. It supports float32Number-encoded input range, output range and transform, and
        /// provides a means to override the BToA1 tag.
        /// </summary>
        BToD3 = 0x42324433,

        /// <summary>
        /// calt - This tag contains the profile calibration date and time. This allows applications and utilities to verify if this profile matches a
        /// vendor's profile and how recently calibration has been performed.
        /// </summary>
        CalibrationDateTime = 0x63616C74,

        /// <summary>
        /// targ - This tag contains the name of the registered characterization data set, or it contains the measurement
        /// data for a characterization target.
        /// </summary>
        CharTarget = 0x74617267,

        /// <summary>
        /// chad - This tag contains a matrix, which shall be invertible, and which converts an nCIEXYZ color, measured using the actual illumination
        /// conditions and relative to the actual adopted white, to an nCIEXYZ color relative to the PCS adopted white
        /// </summary>
        ChromaticAdaptation = 0x63686164,

        /// <summary>
        /// chrm - This tag contains the type and the data of the phosphor/colorant chromaticity set used.
        /// </summary>
        Chromaticity = 0x6368726D,

        /// <summary>
        /// clro - This tag specifies the laydown order of colorants.
        /// </summary>
        ColorantOrder = 0x636C726F,

        /// <summary>
        /// clrt
        /// </summary>
        ColorantTable = 0x636C7274,

        /// <summary>
        /// clot - This tag identifies the colorants used in the profile by a unique name and set of PCSXYZ or PCSLAB values.
        /// When used in DeviceLink profiles only the PCSLAB values shall be permitted.
        /// </summary>
        ColorantTableOut = 0x636C6F74,

        /// <summary>
        /// ciis - This tag indicates the image state of PCS colorimetry produced using the colorimetric intent transforms.
        /// </summary>
        ColorimetricIntentImageStat = 0x63696973,

        /// <summary>
        /// cprt - This tag contains the text copyright information for the profile.
        /// </summary>
        Copyright = 0x63707274,

        /// <summary>
        /// crdi  - Removed in V4
        /// </summary>
        CrdInfo = 0x63726469,

        /// <summary>
        /// data - Removed in V4
        /// </summary>
        Data = 0x64617461,

        /// <summary>
        /// dtim - Removed in V4
        /// </summary>
        DateTime = 0x6474696D,

        /// <summary>
        /// dmnd - This tag describes the structure containing invariant and localizable
        /// versions of the device manufacturer for display
        /// </summary>
        DeviceManufacturerDescription = 0x646D6E64,

        /// <summary>
        /// dmdd - This tag describes the structure containing invariant and localizable
        /// versions of the device model for display.
        /// </summary>
        DeviceModelDescription = 0x646D6464,

        /// <summary>
        /// devs - Removed in V4
        /// </summary>
        DeviceSettings = 0x64657673,

        /// <summary>
        /// D2B0 - This tag defines a color transform from Device to PCS. It supports float32Number-encoded
        /// input range, output range and transform, and provides a means to override the AToB0 tag
        /// </summary>
        DToB0 = 0x44324230,

        /// <summary>
        /// D2B1 - This tag defines a color transform from Device to PCS. It supports float32Number-encoded
        /// input range, output range and transform, and provides a means to override the AToB1 tag
        /// </summary>
        DToB1 = 0x44324230,

        /// <summary>
        /// D2B2 - This tag defines a color transform from Device to PCS. It supports float32Number-encoded
        /// input range, output range and transform, and provides a means to override the AToB1 tag
        /// </summary>
        DToB2 = 0x44324230,

        /// <summary>
        /// D2B3 - This tag defines a color transform from Device to PCS. It supports float32Number-encoded
        /// input range, output range and transform, and provides a means to override the AToB1 tag
        /// </summary>
        DToB3 = 0x44324230,

        /// <summary>
        /// gamt - This tag provides a table in which PCS values are the input and a single
        /// output value for each input value is the output. If the output value is 0, the PCS color is in-gamut.
        /// If the output is non-zero, the PCS color is out-of-gamut
        /// </summary>
        Gamut = 0x67616D74,

        /// <summary>
        /// kTRC - This tag contains the grey tone reproduction curve. The tone reproduction curve provides the necessary
        /// information to convert between a single device channel and the PCSXYZ or PCSLAB encoding.
        /// </summary>
        GrayTrc = 0x6b545243,

        /// <summary>
        /// gXYZ - This tag contains the second column in the matrix, which is used in matrix/TRC transforms.
        /// </summary>
        GreenMatrixColumn = 0x6758595A,

        /// <summary>
        /// gTRC - This tag contains the green channel tone reproduction curve. The first element represents no
        /// colorant (white) or phosphor (black) and the last element represents 100 % colorant (green) or 100 % phosphor (green).
        /// </summary>
        GreenTrc = 0x67545243,

        /// <summary>
        /// lumi - This tag contains the absolute luminance of emissive devices in candelas per square meter as described by the Y channel.
        /// </summary>
        Luminance = 0x6C756d69,

        /// <summary>
        /// meas - This tag describes the alternative measurement specification, such as a D65 illuminant instead of the default D50.
        /// </summary>
        Measurement = 0x6D656173,

        /// <summary>
        /// bkpt - Removed in V4
        /// </summary>
        MediaBlackPoint = 0x626B7074,

        /// <summary>
        /// wtpt - This tag, which is used for generating the ICC-absolute colorimetric intent, specifies the chromatically
        /// adapted nCIEXYZ tristimulus values of the media white point.
        /// </summary>
        MediaWhitePoint = 0x77747074,

        /// <summary>
        /// ncol - OBSOLETE, use <see cref="NamedColor2"/>
        /// </summary>
        NamedColor = 0x6E636f6C,

        /// <summary>
        /// ncl2 - This tag contains the named color information providing a PCS and optional device representation
        /// for a list of named colors.
        /// </summary>
        NamedColor2 = 0x6E636C32,

        /// <summary>
        /// resp - This tag describes the structure containing a description of the device response for which the profile is intended.
        /// </summary>
        OutputResponse = 0x72657370,

        /// <summary>
        /// rig0 - There is only one standard reference medium gamut, as defined in ISO 12640-3
        /// </summary>
        PerceptualRenderingIntentGamut = 0x72696730,

        /// <summary>
        /// pre0 - This tag contains the preview transformation from PCS to device space and back to the PCS.
        /// </summary>
        Preview0 = 0x70726530,

        /// <summary>
        /// pre1 - This tag defines the preview transformation from PCS to device space and back to the PCS.
        /// </summary>
        Preview1 = 0x70726531,

        /// <summary>
        /// pre2 - This tag contains the preview transformation from PCS to device space and back to the PCS.
        /// </summary>
        Preview2 = 0x70726532,

        /// <summary>
        /// desc - This tag describes the structure containing invariant and localizable versions of the profile
        /// description for display.
        /// </summary>
        ProfileDescription = 0x64657363,

        /// <summary>
        /// pseq - This tag describes the structure containing a description of the profile sequence from source to
        /// destination, typically used with the DeviceLink profile.
        /// </summary>
        ProfileSequenceDescription = 0x70736571,

        /// <summary>
        /// psd0 - Removed in V4
        /// </summary>
        PostScript2Crd0 = 0x70736430,

        /// <summary>
        /// psd1 - Removed in V4
        /// </summary>
        PostScript2Crd1 = 0x70736431,

        /// <summary>
        /// psd2 - Removed in V4
        /// </summary>
        PostScript2Crd2 = 0x70736432,

        /// <summary>
        /// psd3 - Removed in V4
        /// </summary>
        PostScript2Crd3 = 0x70736433,

        /// <summary>
        /// ps2s - Removed in V4
        /// </summary>
        PostScript2Csa = 0x70733273,

        /// <summary>
        /// psd2i- Removed in V4
        /// </summary>
        PostScript2RenderingIntent = 0x70733269,

        /// <summary>
        /// rXYZ - This tag contains the first column in the matrix, which is used in matrix/TRC transforms.
        /// </summary>
        RedMatrixColumn = 0x7258595A,

        /// <summary>
        /// This tag contains the red channel tone reproduction curve. The first element represents no colorant
        /// (white) or phosphor (black) and the last element represents 100 % colorant (red) or 100 % phosphor (red).
        /// </summary>
        RedTrc = 0x72545243,

        /// <summary>
        /// rig2 - There is only one standard reference medium gamut, as defined in ISO 12640-3.
        /// </summary>
        SaturationRenderingIntentGamut = 0x72696732,

        /// <summary>
        /// scrd - Removed in V4
        /// </summary>
        ScreeningDescription = 0x73637264,

        /// <summary>
        /// scrn - Removed in V4
        /// </summary>
        Screening = 0x7363726E,

        /// <summary>
        /// tech - The device technology signature
        /// </summary>
        Technology = 0x74656368,

        /// <summary>
        /// bfd - Removed in V4
        /// </summary>
        UcrBgSpecification = 0x62666420,

        /// <summary>
        /// vued - This tag describes the structure containing invariant and localizable
        /// versions of the viewing conditions.
        /// </summary>
        ViewingCondDescription = 0x76756564,

        /// <summary>
        /// view - This tag defines the viewing conditions parameters
        /// </summary>
        ViewingConditions = 0x76696577,
    }
}

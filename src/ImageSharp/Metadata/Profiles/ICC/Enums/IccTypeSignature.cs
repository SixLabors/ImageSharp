// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Type Signature
    /// </summary>
    public enum IccTypeSignature : uint
    {
        /// <summary>
        /// Unknown type signature
        /// </summary>
        Unknown,

        /// <summary>
        /// The chromaticity tag type provides basic chromaticity data and type of
        /// phosphors or colorants of a monitor to applications and utilities
        /// </summary>
        Chromaticity = 0x6368726D,

        /// <summary>
        /// This is an optional tag which specifies the laydown order in which colorants
        /// will be printed on an n-colorant device. The laydown order may be the same
        /// as the channel generation order listed in the colorantTableTag or the channel
        /// order of a color encoding type such as CMYK, in which case this tag is not
        /// needed. When this is not the case (for example, ink-towers sometimes use
        /// the order KCMY), this tag may be used to specify the laydown order of the
        /// colorants
        /// </summary>
        ColorantOrder = 0x636c726f,

        /// <summary>
        /// The purpose of this tag is to identify the colorants used in the profile
        /// by a unique name and set of PCSXYZ or PCSLAB values to give the colorant
        /// an unambiguous value. The first colorant listed is the colorant of the
        /// first device channel of a LUT tag. The second colorant listed is the
        /// colorant of the second device channel of a LUT tag, and so on
        /// </summary>
        ColorantTable = 0x636c7274,

        /// <summary>
        /// The curveType embodies a one-dimensional function which maps an input
        /// value in the domain of the function to an output value in the range
        /// of the function
        /// </summary>
        Curve = 0x63757276,

        /// <summary>
        /// The dataType is a simple data structure that contains either 7-bit ASCII
        /// or binary data
        /// </summary>
        Data = 0x64617461,

        /// <summary>
        /// Date and time defined by 6 unsigned 16bit integers
        /// (year, month, day, hour, minute, second)
        /// </summary>
        DateTime = 0x6474696D,

        /// <summary>
        /// This structure represents a color transform using tables with 16-bit
        /// precision. This type contains four processing elements: a 3 × 3 matrix
        /// (which shall be the identity matrix unless the input color space is
        /// PCSXYZ), a set of one-dimensional input tables, a multi-dimensional
        /// lookup table, and a set of one-dimensional output tables
        /// </summary>
        Lut16 = 0x6D667432,

        /// <summary>
        /// This structure represents a color transform using tables of 8-bit
        /// precision. This type contains four processing elements: a 3 × 3 matrix
        /// (which shall be the identity matrix unless the input color space is
        /// PCSXYZ), a set of one-dimensional input tables, a multi-dimensional
        /// lookup table, and a set of one-dimensional output tables.
        /// </summary>
        Lut8 = 0x6D667431,

        /// <summary>
        /// This structure represents a color transform. The type contains up
        /// to five processing elements which are stored in the AToBTag tag
        /// in the following order: a set of one-dimensional curves, a 3 × 3
        /// matrix with offset terms, a set of one-dimensional curves, a
        /// multi-dimensional lookup table, and a set of one-dimensional
        /// output curves
        /// </summary>
        LutAToB = 0x6D414220,

        /// <summary>
        /// This structure represents a color transform. The type contains
        /// up to five processing elements which are stored in the BToATag
        /// in the following order: a set of one-dimensional curves, a 3 × 3
        /// matrix with offset terms, a set of one-dimensional curves, a
        /// multi-dimensional lookup table, and a set of one-dimensional curves.
        /// </summary>
        LutBToA = 0x6D424120,

        /// <summary>
        /// This information refers only to the internal
        /// profile data and is meant to provide profile makers an alternative
        /// to the default measurement specifications
        /// </summary>
        Measurement = 0x6D656173,

        /// <summary>
        /// This tag structure contains a set of records each referencing a
        /// multilingual Unicode string associated with a profile. Each string
        /// is referenced in a separate record with the information about what
        /// language and region the string is for.
        /// </summary>
        MultiLocalizedUnicode = 0x6D6C7563,

        /// <summary>
        /// This structure represents a color transform, containing a sequence
        /// of processing elements. The processing elements contained in the
        /// structure are defined in the structure itself, allowing for a flexible
        /// structure. Currently supported processing elements are: a set of one
        /// dimensional curves, a matrix with offset terms, and a multidimensional
        /// lookup table (CLUT). Other processing element types may be added in
        /// the future. Each type of processing element may be contained any
        /// number of times in the structure.
        /// </summary>
        MultiProcessElements = 0x6D706574,

        /// <summary>
        /// This type is a count value and array of structures that provide color
        /// coordinates for color names. For each named color, a PCS and optional
        /// device representation of the color are given. Both representations are
        /// 16-bit values and PCS values shall be relative colorimetric. The device
        /// representation corresponds to the header’s "data color space" field.
        /// This representation should be consistent with the "number of device
        /// coordinates" field in the namedColor2Type. If this field is 0, device
        /// coordinates are not provided. The PCS representation corresponds to the
        /// header's PCS field. The PCS representation is always provided. Color
        /// names are fixed-length, 32-byte fields including null termination. In
        /// order to maintain maximum portability, it is strongly recommended that
        /// special characters of the 7-bit ASCII set not be used.
        /// </summary>
        NamedColor2 = 0x6E636C32,

        /// <summary>
        /// This type describes a one-dimensional curve by specifying one of a
        /// predefined set of functions using the parameters.
        /// </summary>
        ParametricCurve = 0x70617261,

        /// <summary>
        /// This type is an array of structures, each of which contains information
        /// from the header fields and tags from the original profiles which were
        /// combined to create the final profile. The order of the structures is
        /// the order in which the profiles were combined and includes a structure
        /// for the final profile. This provides a description of the profile
        /// sequence from source to destination, typically used with the DeviceLink
        /// profile.
        /// </summary>
        ProfileSequenceDesc = 0x70736571,

        /// <summary>
        /// This type is an array of structures, each of which contains information
        /// for identification of a profile used in a sequence.
        /// </summary>
        ProfileSequenceIdentifier = 0x70736964,

        /// <summary>
        /// The purpose of this tag type is to provide a mechanism to relate physical
        /// colorant amounts with the normalized device codes produced by lut8Type,
        /// lut16Type, lutAToBType, lutBToAType or multiProcessElementsType tags
        /// so that corrections can be made for variation in the device without
        /// having to produce a new profile. The mechanism can be used by applications
        /// to allow users with relatively inexpensive and readily available
        /// instrumentation to apply corrections to individual output color
        /// channels in order to achieve consistent results.
        /// </summary>
        ResponseCurveSet16 = 0x72637332,

        /// <summary>
        /// Array of signed floating point numbers with 1 sign bit, 15 value bits and 16 fractional bits
        /// </summary>
        S15Fixed16Array = 0x73663332,

        /// <summary>
        /// The signatureType contains a 4-byte sequence. Sequences of less than four
        /// characters are padded at the end with spaces. Typically this type is used
        /// for registered tags that can be displayed on many development systems as
        /// a sequence of four characters.
        /// </summary>
        Signature = 0x73696720,

        /// <summary>
        /// Simple ASCII text
        /// </summary>
        Text = 0x74657874,

        /// <summary>
        /// Array of unsigned floating point numbers with 16 value bits and 16 fractional bits
        /// </summary>
        U16Fixed16Array = 0x75663332,

        /// <summary>
        /// Array of unsigned 16bit integers (ushort)
        /// </summary>
        UInt16Array = 0x75693136,

        /// <summary>
        /// Array of unsigned 32bit integers (uint)
        /// </summary>
        UInt32Array = 0x75693332,

        /// <summary>
        /// Array of unsigned 64bit integers (ulong)
        /// </summary>
        UInt64Array = 0x75693634,

        /// <summary>
        /// Array of unsigned 8bit integers (byte)
        /// </summary>
        UInt8Array = 0x75693038,

        /// <summary>
        /// This type represents a set of viewing condition parameters.
        /// </summary>
        ViewingConditions = 0x76696577,

        /// <summary>
        /// 3 floating point values describing a XYZ color value
        /// </summary>
        Xyz = 0x58595A20,

        /// <summary>
        /// REMOVED IN V4 - The textDescriptionType is a complex structure that contains three
        /// types of text description structures: 7-bit ASCII, Unicode and ScriptCode. Since no
        /// single standard method for specifying localizable character sets exists across
        /// the major platform vendors, including all three provides access for the major
        /// operating systems. The 7-bit ASCII description is to be an invariant,
        /// nonlocalizable name for consistent reference. It is preferred that both the
        /// Unicode and ScriptCode structures be properly localized.
        /// </summary>
        TextDescription = 0x64657363,

        /// <summary>
        /// REMOVED IN V4 - This type contains the PostScript product name to which this
        /// profile corresponds and the names of the companion CRDs
        /// </summary>
        CrdInfo = 0x63726469,

        /// <summary>
        /// REMOVED IN V4 - The screeningType describes various screening parameters including
        /// screen frequency, screening angle, and spot shape
        /// </summary>
        Screening = 0x7363726E,

        /// <summary>
        /// REMOVED IN V4 - This type contains curves representing the under color removal and
        /// black generation and a text string which is a general description of the method
        /// used for the UCR and BG
        /// </summary>
        UcrBg = 0x62666420,

        /// <summary>
        /// REMOVED IN V4 - This type is an array of structures each of which contains
        /// platform-specific information about the settings of the device for which
        /// this profile is valid. This type is not supported.
        /// </summary>
        DeviceSettings = 0x64657673,        // not supported

        /// <summary>
        /// REMOVED IN V2 - use <see cref="NamedColor2"/> instead. This type is not supported.
        /// </summary>
        NamedColor = 0x6E636F6C,            // not supported
    }
}

// <copyright file="IccTypeSignature.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Type Signature
    /// </summary>
    internal enum IccTypeSignature : uint
    {
        /// <summary>
        /// Unknown type signature
        /// </summary>
        Unknown,

        Chromaticity = 0x6368726D,

        ColorantOrder = 0x636c726f,

        ColorantTable = 0x636c7274,

        Curve = 0x63757276,

        Data = 0x64617461,

        /// <summary>
        /// Date and time defined by 6 unsigned 16bit integers (year, month, day, hour, minute, second)
        /// </summary>
        DateTime = 0x6474696D,

        /// <summary>
        /// Lookup table with 16bit unsigned integers (ushort)
        /// </summary>
        Lut16 = 0x6D667432,

        /// <summary>
        /// Lookup table with 8bit unsigned integers (byte)
        /// </summary>
        Lut8 = 0x6D667431,

        LutAToB = 0x6D414220,

        LutBToA = 0x6D424120,

        Measurement = 0x6D656173,

        /// <summary>
        /// Unicode text in one or more languages
        /// </summary>
        MultiLocalizedUnicode = 0x6D6C7563,

        MultiProcessElements = 0x6D706574,

        NamedColor2 = 0x6E636C32,

        ParametricCurve = 0x70617261,

        ProfileSequenceDesc = 0x70736571,

        ProfileSequenceIdentifier = 0x70736964,

        ResponseCurveSet16 = 0x72637332,

        /// <summary>
        /// Array of signed floating point numbers with 1 sign bit, 15 value bits and 16 fractional bits
        /// </summary>
        S15Fixed16Array = 0x73663332,

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

        ViewingConditions = 0x76696577,

        /// <summary>
        /// 3 floating point values describing a XYZ color value
        /// </summary>
        Xyz = 0x58595A20,

        TextDescription = 0x64657363,
    }
}

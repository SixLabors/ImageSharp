// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Enumerates the basic data types as defined in ICC.1:2010 version 4.3.0.0
    /// <see href="http://www.color.org/specification/ICC1v43_2010-12.pdf"/> Section 4.2 to 4.15
    /// </summary>
    internal enum IccDataType
    {
        /// <summary>
        /// A 12-byte value representation of the time and date
        /// </summary>
        DateTime,

        /// <summary>
        /// A single-precision 32-bit floating-point  as specified in IEEE 754,
        /// excluding un-normalized s, infinities, and not a "" (NaN) values
        /// </summary>
        Float32,

        /// <summary>
        /// Positions of some data elements are indicated using a position offset with the data element's size.
        /// </summary>
        Position,

        /// <summary>
        /// An 8-byte value, used to associate a normalized device code with a measurement value
        /// </summary>
        Response16,

        /// <summary>
        /// A fixed signed 4-byte (32-bit) quantity which has 16 fractional bits
        /// </summary>
        S15Fixed16,

        /// <summary>
        /// A fixed unsigned 4-byte (32-bit) quantity having 16 fractional bits
        /// </summary>
        U16Fixed16,

        /// <summary>
        /// A fixed unsigned 2-byte (16-bit) quantity having15 fractional bits
        /// </summary>
        U1Fixed15,

        /// <summary>
        ///  A fixed unsigned 2-byte (16-bit) quantity having 8 fractional bits
        /// </summary>
        U8Fixed8,

        /// <summary>
        /// An unsigned 2-byte (16-bit) integer
        /// </summary>
        UInt16,

        /// <summary>
        /// An unsigned 4-byte (32-bit) integer
        /// </summary>
        UInt32,

        /// <summary>
        /// An unsigned 8-byte (64-bit) integer
        /// </summary>
        UInt64,

        /// <summary>
        /// An unsigned 1-byte (8-bit) integer
        /// </summary>
        UInt8,

        /// <summary>
        /// A set of three fixed signed 4-byte (32-bit) quantities used to encode CIEXYZ, nCIEXYZ, and PCSXYZ tristimulus values
        /// </summary>
        Xyz,

        /// <summary>
        /// Alpha-numeric values, and other input and output codes, shall conform to the American Standard Code for
        /// Information Interchange (ASCII) specified in ISO/IEC 646.
        /// </summary>
        Ascii
    }
}
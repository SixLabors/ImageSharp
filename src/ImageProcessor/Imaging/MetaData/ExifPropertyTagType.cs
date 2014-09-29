// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExifPropertyTagType.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Specifies the data type of the values stored in the value data member of that same PropertyItem object.
//   <see href="http://msdn.microsoft.com/en-us/library/system.drawing.imaging.propertyitem.type.aspx" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.MetaData
{
    /// <summary>
    /// Specifies the data type of the values stored in the value data member of that same PropertyItem object.
    /// <see href="http://msdn.microsoft.com/en-us/library/system.drawing.imaging.propertyitem.type.aspx"/>
    /// </summary>
    public enum ExifPropertyTagType : short
    {
        /// <summary>Specifies that the value data member is an array of bytes.</summary>
        Byte = 1,

        /// <summary>
        /// Specifies that the value data member is a null-terminated ASCII string. If you set the type data member of a
        /// PropertyItem object to ExifPropertyTagTypeASCII, you should set the length data member to the length of the string
        /// including the NULL terminator. For example, the string HELLO would have a length of 6.
        /// </summary>
        ASCII = 2,

        /// <summary>Specifies that the value data member is an array of unsigned short (16-bit) integers.</summary>
        Int16 = 3,

        /// <summary>Specifies that the value data member is an array of unsigned long (32-bit) integers.</summary>
        Int32 = 4,

        /// <summary>
        /// Specifies that the value data member is an array of pairs of unsigned long integers. Each pair represents a
        /// fraction; the first integer is the numerator and the second integer is the denominator.
        /// </summary>
        Rational = 5,

        /// <summary>
        /// Specifies that the value data member is an array of bytes that can hold values of any data type.
        /// </summary>
        Undefined = 7,

        /// <summary>
        /// Specifies that the value data member is an array of signed long (32-bit) integers.
        /// </summary>
        SLONG = 9,

        /// <summary>
        /// Specifies that the value data member is an array of pairs of signed long integers. Each pair represents a
        /// fraction; the first integer is the numerator and the second integer is the denominator.
        /// </summary>
        SRational = 10
    }
}
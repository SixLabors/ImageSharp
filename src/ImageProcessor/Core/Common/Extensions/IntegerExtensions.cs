// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IntegerExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates a series of time saving extension methods to the <see cref="T:System.Int32" /> class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Core.Common.Extensions
{
    using System.Globalization;

    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Int32"/> class.
    /// </summary>
    public static class IntegerExtensions
    {
        /// <summary>
        /// Converts an <see cref="T:System.Int32"/> value into a valid <see cref="T:System.Byte"/>.
        /// <remarks>
        /// If the value given is less than 0 or greater than 255, the value will be constrained into
        /// those restricted ranges.
        /// </remarks>
        /// </summary>
        /// <param name="integer">
        /// The <see cref="T:System.Int32"/> to convert.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Byte"/>.
        /// </returns>
        public static byte ToByte(this int integer)
        {
            return ((double)integer).ToByte();
        }

        /// <summary>
        /// Converts the string representation of a number in a specified culture-specific format to its 
        /// 32-bit signed integer equivalent using invariant culture.
        /// </summary>
        /// <param name="integer">The integer.</param>
        /// <param name="s">A string containing a number to convert.</param>
        /// <returns>A 32-bit signed integer equivalent to the number specified in s.</returns>
        public static int ParseInvariant(this int integer, string s)
        {
            return int.Parse(s, CultureInfo.InvariantCulture);
        }
    }
}

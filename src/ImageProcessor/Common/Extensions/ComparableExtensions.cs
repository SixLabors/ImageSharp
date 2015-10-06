// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ComparableExtensions.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Extension methods for classes that implement <see cref="IComparable{T}" />.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// Extension methods for classes that implement <see cref="IComparable{T}"/>.
    /// </summary>
    internal static class ComparableExtensions
    {
        /// <summary>
        /// Restricts a value to be within a specified range.
        /// </summary>
        /// <param name="value">The The value to clamp.</param>
        /// <param name="min">The minimum value. If value is less than min, min will be returned.</param>
        /// <param name="max">The maximum value. If value is greater than max, max will be returned.</param>
        /// <typeparam name="T">The <see cref="System.Type"/> to clamp.</typeparam>
        /// <returns>
        /// The <see cref="IComparable{T}"/> representing the clamped value.
        /// </returns>
        public static T Clamp<T>(this T value, T min, T max)
                    where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
            {
                return min;
            }

            if (value.CompareTo(max) > 0)
            {
                return max;
            }

            return value;
        }

        /// <summary>
        /// Converts an <see cref="int"/> to a <see cref="byte"/> first restricting the value between the
        /// minimum and maximum allowable ranges.
        /// </summary>
        /// <param name="value">The <see cref="int"/> this method extends.</param>
        /// <returns>The <see cref="byte"/></returns>
        public static byte ToByte(this int value)
        {
            return (byte)value.Clamp(0, 255);
        }

        /// <summary>
        /// Converts an <see cref="float"/> to a <see cref="byte"/> first restricting the value between the
        /// minimum and maximum allowable ranges.
        /// </summary>
        /// <param name="value">The <see cref="float"/> this method extends.</param>
        /// <returns>The <see cref="byte"/></returns>
        public static byte ToByte(this float value)
        {
            return (byte)value.Clamp(0, 255);
        }

        /// <summary>
        /// Converts an <see cref="double"/> to a <see cref="byte"/> first restricting the value between the
        /// minimum and maximum allowable ranges.
        /// </summary>
        /// <param name="value">The <see cref="double"/> this method extends.</param>
        /// <returns>The <see cref="byte"/></returns>
        public static byte ToByte(this double value)
        {
            return (byte)value.Clamp(0, 255);
        }
    }
}

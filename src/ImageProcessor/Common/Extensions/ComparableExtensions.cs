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
        /// Returns value indicating whether the given number is with in the minimum and maximum
        /// given range.
        /// </summary>
        /// <param name="value">The value to to check. </param>
        /// <param name="min">The minimum range value.</param>
        /// <param name="max">The maximum range value.</param>
        /// <typeparam name="T">The <see cref="System.Type"/> to test.</typeparam>
        /// <returns>
        /// True if the value falls within the maximum and minimum; otherwise, false.
        /// </returns>
        public static bool IsBetween<T>(this T value, T min, T max) where T : IComparable<T>
        {
            return (value.CompareTo(min) > 0) && (value.CompareTo(max) < 0);
        }

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
        public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
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
    }
}

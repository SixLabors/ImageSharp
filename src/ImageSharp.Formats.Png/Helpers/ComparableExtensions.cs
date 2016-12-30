// <copyright file="ComparableExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Extension methods for classes that implement <see cref="IComparable{T}"/>.
    /// </summary>
    internal static class ComparableExtensions
    {
        /// <summary>
        /// Restricts a <see cref="int"/> to be within a specified range.
        /// </summary>
        /// <param name="value">The The value to clamp.</param>
        /// <param name="min">The minimum value. If value is less than min, min will be returned.</param>
        /// <param name="max">The maximum value. If value is greater than max, max will be returned.</param>
        /// <returns>
        /// The <see cref="int"/> representing the clamped value.
        /// </returns>
        public static int Clamp(this int value, int min, int max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Swaps the references to two objects in memory.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference.</param>
        /// <typeparam name="T">The type of object.</typeparam>
        public static void Swap<T>(ref T first, ref T second)
        {
            T temp = second;
            second = first;
            first = temp;
        }
    }
}

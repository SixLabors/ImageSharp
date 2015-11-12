// <copyright file="EnumerableExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Collections.IEnumerable"/> interface.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Generates a sequence of integral numbers within a specified range.
        /// </summary>
        /// <param name="fromInclusive">
        /// The start index, inclusive.
        /// </param>
        /// <param name="toExclusive">
        /// The end index, exclusive.
        /// </param>
        /// <param name="step">
        /// The incremental step.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable{Int32}"/> that contains a range of sequential integral numbers.
        /// </returns>
        public static IEnumerable<int> SteppedRange(int fromInclusive, int toExclusive, int step)
        {
            // Borrowed from Enumerable.Range
            long num = (fromInclusive + toExclusive) - 1L;
            if ((toExclusive < 0) || (num > 0x7fffffffL))
            {
                throw new ArgumentOutOfRangeException(nameof(toExclusive));
            }

            return RangeIterator(fromInclusive, i => i < toExclusive, step);
        }

        /// <summary>
        /// Generates a sequence of integral numbers within a specified range.
        /// </summary>
        /// <param name="fromInclusive">
        /// The start index, inclusive.
        /// </param>
        /// <param name="toDelegate">
        /// A method that has one parameter and returns a <see cref="bool"/> calculating the end index
        /// </param>
        /// <param name="step">
        /// The incremental step.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable{Int32}"/> that contains a range of sequential integral numbers.
        /// </returns>
        public static IEnumerable<int> SteppedRange(int fromInclusive, Func<int, bool> toDelegate, int step)
        {
            return RangeIterator(fromInclusive, toDelegate, step);
        }

        /// <summary>
        /// Generates a sequence of integral numbers within a specified range.
        /// </summary>
        /// <param name="fromInclusive">
        /// The start index, inclusive.
        /// </param>
        /// <param name="toDelegate">
        /// A method that has one parameter and returns a <see cref="bool"/> calculating the end index
        /// </param>
        /// <param name="step">
        /// The incremental step.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable{Int32}"/> that contains a range of sequential integral numbers.
        /// </returns>
        private static IEnumerable<int> RangeIterator(int fromInclusive, Func<int, bool> toDelegate, int step)
        {
            int i = fromInclusive;
            while (toDelegate(i))
            {
                yield return i;
                i += step;
            }
        }
    }
}
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Collections.IEnumerable"/> interface.
    /// </summary>
    internal static class EnumerableExtensions
    {
        /// <summary>
        /// Generates a sequence of integral numbers within a specified range.
        /// </summary>
        /// <param name="fromInclusive">
        /// The start index, inclusive.
        /// </param>
        /// <param name="toDelegate">
        /// A method that has one parameter and returns a <see cref="bool"/> calculating the end index.
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
        /// <param name="fromInclusive">The start index, inclusive.</param>
        /// <param name="toDelegate">
        /// A method that has one parameter and returns a <see cref="bool"/> calculating the end index.
        /// </param>
        /// <param name="step">The incremental step.</param>
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

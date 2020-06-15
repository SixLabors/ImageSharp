// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;

// TODO: These should just call the guard equivalents
namespace SixLabors
{
    /// <summary>
    /// Provides methods to protect against invalid parameters for a DEBUG build.
    /// </summary>
    internal static partial class DebugGuard
    {
        /// <summary>
        /// Verifies whether a specific condition is met, throwing an exception if it's false.
        /// </summary>
        /// <param name="target">The condition</param>
        /// <param name="message">The error message</param>
        [Conditional("DEBUG")]
        public static void IsTrue(bool target, string message)
        {
            if (!target)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Verifies, that the target span is of same size than the 'other' span.
        /// </summary>
        /// <typeparam name="T">The element type of the spans</typeparam>
        /// <param name="target">The target span.</param>
        /// <param name="other">The 'other' span to compare 'target' to.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/> has a different size than <paramref name="other"/>
        /// </exception>
        [Conditional("DEBUG")]
        public static void MustBeSameSized<T>(Span<T> target, Span<T> other, string parameterName)
            where T : struct
        {
            if (target.Length != other.Length)
            {
                throw new ArgumentException("Span-s must be the same size!", parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the `target` span has the length of 'minSpan', or longer.
        /// </summary>
        /// <typeparam name="T">The element type of the spans</typeparam>
        /// <param name="target">The target span.</param>
        /// <param name="minSpan">The 'minSpan' span to compare 'target' to.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/> has less items than <paramref name="minSpan"/>
        /// </exception>
        [Conditional("DEBUG")]
        public static void MustBeSizedAtLeast<T>(Span<T> target, Span<T> minSpan, string parameterName)
            where T : struct
        {
            if (target.Length < minSpan.Length)
            {
                throw new ArgumentException($"Span-s must be at least of length {minSpan.Length}!", parameterName);
            }
        }
    }
}

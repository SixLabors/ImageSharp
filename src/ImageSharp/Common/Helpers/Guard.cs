// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides methods to protect against invalid parameters.
    /// </summary>
    [DebuggerStepThrough]
    internal static class Guard
    {
        /// <summary>
        /// Ensures that the value is not null.
        /// </summary>
        /// <param name="value">The target object, which cannot be null.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null</exception>
        public static void NotNull<T>(T value, string parameterName)
            where T : class
        {
            if (value is null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        /// <summary>
        /// Ensures that the target value is not null, empty, or whitespace.
        /// </summary>
        /// <param name="value">The target string, which should be checked against being null or empty.</param>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> is empty or contains only blanks.</exception>
        public static void NotNullOrWhiteSpace(string value, string parameterName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Must not be empty or whitespace.", parameterName);
            }
        }

        /// <summary>
        /// Ensures that the enumeration is not null or empty.
        /// </summary>
        /// <typeparam name="T">The type of objects in the <paramref name="value"/></typeparam>
        /// <param name="value">The target enumeration, which should be checked against being null or empty.</param>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
        public static void NotNullOrEmpty<T>(ICollection<T> value, string parameterName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Count == 0)
            {
                throw new ArgumentException("Must not be empty.", parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        public static void MustBeLessThan<TValue>(TValue value, TValue max, string parameterName)
                    where TValue : IComparable<TValue>
        {
            if (value.CompareTo(max) >= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value {value} must be less than {max}.");
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        public static void MustBeLessThanOrEqualTo<TValue>(TValue value, TValue max, string parameterName)
                    where TValue : IComparable<TValue>
        {
            if (value.CompareTo(max) > 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value {value} must be less than or equal to {max}.");
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        public static void MustBeGreaterThan<TValue>(TValue value, TValue min, string parameterName)
            where TValue : IComparable<TValue>
        {
            if (value.CompareTo(min) <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Value {value} must be greater than {min}.");
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        public static void MustBeGreaterThanOrEqualTo<TValue>(TValue value, TValue min, string parameterName)
            where TValue : IComparable<TValue>
        {
            if (value.CompareTo(min) < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value {value} must be greater than or equal to {min}.");
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        public static void MustBeBetweenOrEqualTo<TValue>(TValue value, TValue min, TValue max, string parameterName)
            where TValue : IComparable<TValue>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value {value} must be greater than or equal to {min} and less than or equal to {max}.");
            }
        }

        /// <summary>
        /// Verifies, that the method parameter with specified target value is true
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="target">The target value, which cannot be false.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <param name="message">The error message, if any to add to the exception.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/> is false
        /// </exception>
        public static void IsTrue(bool target, string parameterName, string message)
        {
            if (!target)
            {
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the method parameter with specified target value is false
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="target">The target value, which cannot be true.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <param name="message">The error message, if any to add to the exception.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/> is true
        /// </exception>
        public static void IsFalse(bool target, string parameterName, string message)
        {
            if (target)
            {
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the `source` span has the length of 'minLength', or longer.
        /// </summary>
        /// <typeparam name="T">The element type of the spans</typeparam>
        /// <param name="source">The source span.</param>
        /// <param name="minLength">The minimum length.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="source"/> has less than <paramref name="minLength"/> items
        /// </exception>
        public static void MustBeSizedAtLeast<T>(ReadOnlySpan<T> source, int minLength, string parameterName)
        {
            if (source.Length < minLength)
            {
                throw new ArgumentException($"Span-s must be at least of length {minLength}!", parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the `source` span has the length of 'minLength', or longer.
        /// </summary>
        /// <typeparam name="T">The element type of the spans</typeparam>
        /// <param name="source">The target span.</param>
        /// <param name="minLength">The minimum length.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="source"/> has less than <paramref name="minLength"/> items
        /// </exception>
        public static void MustBeSizedAtLeast<T>(Span<T> source, int minLength, string parameterName)
        {
            if (source.Length < minLength)
            {
                throw new ArgumentException($"Span-s must be at least of length {minLength}!", parameterName);
            }
        }
    }
}

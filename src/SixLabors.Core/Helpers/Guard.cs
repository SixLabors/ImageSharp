// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SixLabors
{
    /// <summary>
    /// Provides methods to protect against invalid parameters.
    /// </summary>
    [DebuggerStepThrough]
    internal static class Guard
    {
        /// <summary>
        /// Verifies, that the method parameter with specified object value is not null
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="target">The target object, which cannot be null.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <param name="message">The error message, if any to add to the exception.</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static void NotNull(object target, string parameterName, string message = "")
        {
            if (target == null)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    throw new ArgumentNullException(parameterName, message);
                }

                throw new ArgumentNullException(parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the string method parameter with specified object value and message
        /// is not null, not empty and does not contain only blanks and throws an exception
        /// if the object is null.
        /// </summary>
        /// <param name="target">The target string, which should be checked against being null or empty.</param>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <param name="message">The error message, if any to add to the exception.</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="target"/> is empty or contains only blanks.</exception>
        public static void NotNullOrEmpty(string target, string parameterName, string message = "")
        {
            NotNull(target, parameterName, message);

            if (string.IsNullOrWhiteSpace(target))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    throw new ArgumentException(message, parameterName);
                }

                throw new ArgumentException("Value cannot be null, empty, or cannot contain only whitespace.", parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the enumeration is not null and not empty.
        /// </summary>
        /// <typeparam name="T">The type of objects in the <paramref name="target"/></typeparam>
        /// <param name="target">The target enumeration, which should be checked against being null or empty.</param>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <param name="message">The error message, if any to add to the exception.</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="target"/> is empty.</exception>
        public static void NotNullOrEmpty<T>(IEnumerable<T> target, string parameterName, string message = "")
        {
            NotNull(target, parameterName, message);

            if (!target.Any())
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    throw new ArgumentException(message, parameterName);
                }

                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        public static void MustBeLessThan<TValue>(TValue value, TValue max, string parameterName)
                    where TValue : IComparable<TValue>
        {
            if (value.CompareTo(max) >= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value must be less than {max}.");
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
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        public static void MustBeLessThanOrEqualTo<TValue>(TValue value, TValue max, string parameterName)
                    where TValue : IComparable<TValue>
        {
            if (value.CompareTo(max) > 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value must be less than or equal to {max}.");
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
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        public static void MustBeGreaterThan<TValue>(TValue value, TValue min, string parameterName)
            where TValue : IComparable<TValue>
        {
            if (value.CompareTo(min) <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value must be greater than {min}.");
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
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        public static void MustBeGreaterThanOrEqualTo<TValue>(TValue value, TValue min, string parameterName)
            where TValue : IComparable<TValue>
        {
            if (value.CompareTo(min) < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value must be greater than or equal to {min}.");
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
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        public static void MustBeBetweenOrEqualTo<TValue>(TValue value, TValue min, TValue max, string parameterName)
            where TValue : IComparable<TValue>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"Value must be greater than or equal to {min} and less than or equal to {max}.");
            }
        }

        /// <summary>
        /// Verifies, that the method parameter with specified target value is true
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="value">
        /// The target value, which cannot be false.
        /// </param>
        /// <param name="parameterName">
        /// The name of the parameter that is to be checked.
        /// </param>
        /// <param name="message">
        /// The error message, if any to add to the exception.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is false
        /// </exception>
        public static void IsTrue(bool value, string parameterName, string message)
        {
            if (!value)
            {
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the method parameter with specified target value is false
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="value">The target value, which cannot be true.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <param name="message">The error message, if any to add to the exception.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is true
        /// </exception>
        public static void IsFalse(bool value, string parameterName, string message)
        {
            if (value)
            {
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the `target` span has the length of 'minLength', or longer.
        /// </summary>
        /// <typeparam name="T">The element type of the spans</typeparam>
        /// <param name="value">The target span.</param>
        /// <param name="minLength">The minimum length.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// The length of <paramref name="value"/> is less than <paramref name="minLength"/>.
        /// </exception>
        public static void MustBeSizedAtLeast<T>(T[] value, int minLength, string parameterName)
        {
            if (value.Length < minLength)
            {
                throw new ArgumentException($"The size must be at least {minLength}.", parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the `target` span has the length of 'minLength', or longer.
        /// </summary>
        /// <typeparam name="T">The element type of the spans</typeparam>
        /// <param name="value">The target span.</param>
        /// <param name="minLength">The minimum length.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// The length of <paramref name="value"/> is less than <paramref name="minLength"/>.
        /// </exception>
        public static void MustBeSizedAtLeast<T>(ReadOnlySpan<T> value, int minLength, string parameterName)
        {
            if (value.Length < minLength)
            {
                throw new ArgumentException($"The size must be at least {minLength}.", parameterName);
            }
        }
    }
}

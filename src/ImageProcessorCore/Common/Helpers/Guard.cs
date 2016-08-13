// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Guard.cs" company="James Jackson-South">
//   Copyright (c) James Jackson-South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides methods to protect against invalid parameters.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ImageProcessorCore.Tests")]
namespace ImageProcessorCore
{
    using System;
    using System.Diagnostics;

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
        /// <param name="target">
        /// The target object, which cannot be null.
        /// </param>
        /// <param name="parameterName">
        /// The name of the parameter that is to be checked.
        /// </param>
        /// <param name="message">
        /// The error message, if any to add to the exception.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="target"/> is null
        /// </exception>
        public static void NotNull(object target, string parameterName, string message = "")
        {
            if (target == null)
            {
                if (string.IsNullOrWhiteSpace(message))
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
        /// <exception cref="ArgumentNullException">
        /// <paramref name="target"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/> is
        /// empty or contains only blanks.
        /// </exception>
        public static void NotNullOrEmpty(string target, string parameterName)
        {
            if (target == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("Value cannot be null or empty and cannot contain only blanks.", parameterName);
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
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        public static void MustBeLessThan<TValue>(TValue value, TValue max, string parameterName)
                    where TValue : IComparable<TValue>
        {
            if (value.CompareTo(max) >= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Value must be less than {max}.");
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
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Value must be less than or equal to {max}.");
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
                    $"Value must be greater than {min}.");
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
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Value must be greater than or equal to {min}.");
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
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Value must be greater than or equal to {min} and less than or equal to {max}.");
            }
        }

        /// <summary>
        /// Verifies, that the method parameter with specified target value is true
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="target">
        /// The target value, which cannot be false.
        /// </param>
        /// <param name="parameterName">
        /// The name of the parameter that is to be checked.
        /// </param>
        /// <param name="message">
        /// The error message, if any to add to the exception.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/> is null
        /// </exception>
        public static void IsTrue(bool target, string parameterName, string message = "")
        {
            if (!target)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new ArgumentException(parameterName, message);
                }

                throw new ArgumentException(parameterName);
            }
        }

        /// <summary>
        /// Verifies, that the method parameter with specified target value is false
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="target">
        /// The target value, which cannot be true.
        /// </param>
        /// <param name="parameterName">
        /// The name of the parameter that is to be checked.
        /// </param>
        /// <param name="message">
        /// The error message, if any to add to the exception.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/> is null
        /// </exception>
        public static void IsFalse(bool target, string parameterName, string message = "")
        {
            if (target)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new ArgumentException(parameterName, message);
                }

                throw new ArgumentException(parameterName);
            }
        }
    }
}

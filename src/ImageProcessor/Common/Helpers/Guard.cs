// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Guard.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides methods to protect against invalid parameters.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Provides methods to protect against invalid parameters.
    /// </summary>
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
                throw new ArgumentException("String parameter cannot be null or empty and cannot contain only blanks.", parameterName);
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
        public static void LessThan<TValue>(TValue value, TValue max, string parameterName) where TValue : IComparable<TValue>
        {
            if (value.CompareTo(max) >= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    string.Format(CultureInfo.CurrentCulture, "Value must be less than {0}", max));
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
        public static void GreaterThan<TValue>(TValue value, TValue min, string parameterName) where TValue : IComparable<TValue>
        {
            if (value.CompareTo(min) <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    string.Format(CultureInfo.CurrentCulture, "Value must be greater than {0}", min));
            }
        }
    }
}

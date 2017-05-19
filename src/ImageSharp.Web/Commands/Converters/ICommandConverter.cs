// <copyright file="ICommandConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands.Converters
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Defines a contract for converting the value of a string into a different data type.
    /// Implementations of this interface should be stateless by design.
    /// </summary>
    public interface ICommandConverter
    {
        /// <summary>
        /// Converts the given string to the type of this converter, using the specified culture information.
        /// </summary>
        /// <returns>
        /// An <see cref="string"/> that represents the converted value.
        /// </returns>
        /// <param name="culture">
        /// The <see cref="CultureInfo"/> to use as the current culture.
        /// </param>
        /// <param name="value">The <see cref="string"/> to convert. </param>
        /// <param name="propertyType">The property type that the converter will convert to.</param>
        /// <exception cref="NotSupportedException">The conversion cannot be performed.</exception>
        object ConvertFrom(CultureInfo culture, string value, Type propertyType);

        /// <summary>
        /// Converts the given string to the converter's native type using the invariant culture.
        /// </summary>
        /// <param name="text">The value to convert from.</param>
        /// <param name="propertyType">The property type that the converter will convert to.</param>
        /// <returns>
        /// An <see cref="object"/> that represents the converted value.
        /// </returns>
        object ConvertFromInvariantString(string text, Type propertyType);
    }
}
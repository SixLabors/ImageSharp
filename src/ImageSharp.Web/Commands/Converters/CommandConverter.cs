// <copyright file="CommandConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands.Converters
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Converts the value of a string into a different data type.
    /// </summary>
    /// <remarks>
    /// The code here is adapted from the TypeConverter class. We do not use that class as we only need "From" methods and
    /// there are several classes within the .NET Framework that automatically wire up type converters.
    /// We do not ever want to interfere with that.
   /// </remarks>
    public abstract class CommandConverter : ICommandConverter
    {
        /// <inheritdoc/>
        public virtual object ConvertFrom(CultureInfo culture, string value, Type propertyType) => throw this.GetConvertFromException(value);

        /// <inheritdoc/>
        public object ConvertFromInvariantString(string text, Type propertyType) => this.ConvertFrom(CultureInfo.InvariantCulture, text, propertyType);

        /// <summary>
        /// Gets a suitable exception to throw when a conversion cannot be performed.
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <returns><see cref="NotSupportedException"/></returns>
        protected Exception GetConvertFromException(object value)
        {
            string valueTypeName = value == null ? "null" : value.GetType().FullName;
            throw new NotSupportedException($"{this.GetType().Name} cannot convert from {valueTypeName}");
        }
    }
}
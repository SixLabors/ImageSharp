// <copyright file="ArrayConverter{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Converts the value of an string to a generic array.
    /// </summary>
    /// <typeparam name="T">The parameter type to convert to.</typeparam>
    internal sealed class ArrayConverter<T> : ListConverter<T>
    {
        /// <inheritdoc/>
        public override object ConvertFrom(CultureInfo culture, string value, Type propertyType)
        {
            object result = base.ConvertFrom(culture, value, propertyType);

            var list = result as IList<T>;
            return list?.ToArray() ?? result;
        }
    }
}
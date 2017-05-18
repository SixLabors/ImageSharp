// <copyright file="SimpleCommandConverter{T}.cs" company="James Jackson-South">
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
    /// Converts the value of an string to a generic list.
    /// </summary>
    /// <typeparam name="T">The type to convert from.</typeparam>
    internal class ListConverter<T> : CommandConverter
    {
        /// <inheritdoc/>
        public override object ConvertFrom(CultureInfo culture, string value, Type propertyType)
        {
            Type type = typeof(T);
            ICommandConverter paramConverter = CommandDescriptor.GetConverter(type);

            if (paramConverter == null)
            {
                throw new InvalidOperationException("No type converter exists for type " + type.FullName);
            }

            var result = new List<T>();

            if (value != null)
            {
                string[] items = this.GetStringArray(value, culture);

                foreach (string s in items)
                {
                    object item = paramConverter.ConvertFromInvariantString(s, propertyType);
                    if (item != null)
                    {
                        result.Add((T)item);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Splits a string by separator to return an array of string values.
        /// </summary>
        /// <param name="input">The input string to split.</param>
        /// <param name="culture">A <see cref="CultureInfo"/>. The current culture to split string by.</param>
        /// <returns>The <see cref="T:String[]"/></returns>
        protected string[] GetStringArray(string input, CultureInfo culture)
        {
            char separator = culture.TextInfo.ListSeparator[0];
            string[] result = input.Split(separator).Select(s => s.Trim()).ToArray();

            return result;
        }
    }
}
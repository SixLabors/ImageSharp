// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GenericListTypeConverter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Converts the value of an string to and from a List{T}.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Converts the value of an string to and from a List{T}.
    /// </summary>
    /// <typeparam name="T">
    /// The type to convert from.
    /// </typeparam>
    public class GenericListTypeConverter<T> : TypeConverter
    {
        /// <summary>
        /// The type converter.
        /// </summary>
        private readonly TypeConverter typeConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericListTypeConverter{T}"/> class.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no converter exists for the given type.
        /// </exception>
        public GenericListTypeConverter()
        {
            Type type = typeof(T);
            this.typeConverter = TypeDescriptor.GetConverter(type);
            if (this.typeConverter == null)
            {
                throw new InvalidOperationException("No type converter exists for type " + type.FullName);
            }
        }

        /// <summary>
        /// Returns whether this converter can convert an object of the given type to the type of this converter, 
        /// using the specified context.
        /// </summary>
        /// <returns>
        /// true if this converter can perform the conversion; otherwise, false.
        /// </returns>
        /// <param name="context">
        /// An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a 
        /// format context. </param>
        /// <param name="sourceType">
        /// A <see cref="T:System.Type"/> that represents the type you want to convert from. 
        /// </param>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        /// <summary>
        /// Converts the given object to the type of this converter, using the specified context and culture 
        /// information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <param name="context">
        /// An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. 
        /// </param>
        /// <param name="culture">
        /// The <see cref="T:System.Globalization.CultureInfo"/> to use as the current culture. 
        /// </param>
        /// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed.</exception>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string input = value as string;
            if (input != null)
            {
                string[] items = this.GetStringArray(input, culture);

                List<T> result = new List<T>();

                Array.ForEach(
                    items,
                    s =>
                    {
                        object item = this.typeConverter.ConvertFromInvariantString(s);
                        if (item != null)
                        {
                            result.Add((T)item);
                        }
                    });

                return result;
            }

            return base.ConvertFrom(context, culture, value);
        }

        /// <summary>
        /// Converts the given value object to the specified type, using the specified context and culture 
        /// information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <param name="context">
        /// An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. 
        /// </param>
        /// <param name="culture">
        /// A <see cref="T:System.Globalization.CultureInfo"/>. If null is passed, the current culture is assumed. 
        /// </param>
        /// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
        /// <param name="destinationType">
        /// The <see cref="T:System.Type"/> to convert the <paramref name="value"/> parameter to. 
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// The <paramref name="destinationType"/> parameter is null. 
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed. 
        /// </exception>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                if (culture == null)
                {
                    culture = CultureInfo.CurrentCulture;
                }

                string separator = culture.TextInfo.ListSeparator;
                return string.Join(separator, (IList<T>)value);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <summary>
        /// Splits a string by comma to return an array of string values.
        /// </summary>
        /// <param name="input">
        /// The input string to split.
        /// </param>
        /// <param name="culture">
        /// A <see cref="T:System.Globalization.CultureInfo"/>. The current culture to split string by. 
        /// </param>
        /// <returns>
        /// The <see cref="string"/> array from the comma separated values.
        /// </returns>
        protected string[] GetStringArray(string input, CultureInfo culture)
        {
            if (culture == null)
            {
                culture = CultureInfo.CurrentCulture;
            }

            char separator = culture.TextInfo.ListSeparator[0];
            string[] result = input.Split(separator).Select(s => s.Trim()).ToArray();

            return result;
        }
    }
}

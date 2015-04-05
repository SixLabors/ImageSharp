// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FontFamilyConverter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   FontFamilyConverter - converter class for converting between the <see cref="FontFamily" />
//   and <see cref="String" /> types.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.ComponentModel;
    using System.Drawing;
    using System.Globalization;

    /// <summary>
    /// FontFamilyConverter - converter class for converting between the <see cref="FontFamily"/>
    /// and <see cref="String"/> types.
    /// </summary>
    public class FontFamilyConverter : TypeConverter
    {
        /// <summary>
        /// Returns whether this converter can convert an object of the given type to the type of 
        /// this converter, using the specified context.
        /// </summary>
        /// <returns>
        /// true if this converter can perform the conversion; otherwise, false.
        /// </returns>
        /// <param name="context">
        /// An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. 
        /// </param>
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
        /// Returns whether this converter can convert the object to the specified type, using 
        /// the specified context.
        /// </summary>
        /// <returns>
        /// true if this converter can perform the conversion; otherwise, false.
        /// </returns>
        /// <param name="context">
        /// An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. 
        /// </param>
        /// <param name="destinationType">
        /// A <see cref="T:System.Type"/> that represents the type you want to convert to. 
        /// </param>
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string) || destinationType == typeof(FontFamily))
            {
                return true;
            }

            return base.CanConvertTo(context, destinationType);
        }

        /// <summary>
        /// Converts the given object to the type of this converter, using the specified context and 
        /// culture information.
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
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed. </exception>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string s = value as string;
            if (!string.IsNullOrWhiteSpace(s))
            {
                return new FontFamily(s);
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
        /// </param><param name="value">The <see cref="T:System.Object"/> to convert. </param>
        /// <param name="destinationType">
        /// The <see cref="T:System.Type"/> to convert the <paramref name="value"/> parameter to. 
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="destinationType"/> parameter is null. </exception>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed. </exception>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (null == value)
            {
                throw new ArgumentNullException("value");
            }

            FontFamily fontFamily = value as FontFamily;
            if (fontFamily == null)
            {
                throw new ArgumentException("value");
            }

            if (null == destinationType)
            {
                throw new ArgumentNullException("destinationType");
            }

            if (destinationType == typeof(string))
            {
                return fontFamily.Name;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}

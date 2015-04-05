// --------------------------------------------------------------------------------------------------------------------
// <copyright file="QueryParamParser.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The query parameter parser that converts string values to different types.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Web;

    /// <summary>
    /// The query parameter parser that converts string values to different types.
    /// </summary>
    public class QueryParamParser
    {
        /// <summary>
        /// A new instance of the <see cref="QueryParamParser"/> class.
        /// with lazy initialization.
        /// </summary>
        private static readonly Lazy<QueryParamParser> Lazy = new Lazy<QueryParamParser>(() => new QueryParamParser());

        /// <summary>
        /// The cache for storing created default types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, object> TypeDefaultsCache = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Prevents a default instance of the <see cref="QueryParamParser"/> class from being created.
        /// </summary>
        private QueryParamParser()
        {
            this.AddColorConverters();
            this.AddFontFamilyConverters();
            this.AddListConverters();
            this.AddArrayConverters();
        }

        /// <summary>
        /// Gets the current <see cref="QueryParamParser"/> instance.
        /// </summary>
        public static QueryParamParser Instance
        {
            get
            {
                return Lazy.Value;
            }
        }

        /// <summary>
        /// Parses the given string value converting it to the given type.
        /// </summary>
        /// <param name="value">
        /// The <see cref="String"/> value to parse.
        /// </param>
        /// <param name="culture">
        /// The <see cref="CultureInfo"/> to use as the current culture. 
        /// <remarks>If not set will parse using <see cref="CultureInfo.InvariantCulture"/></remarks>
        /// </param>
        /// <typeparam name="T">
        /// The <see cref="Type"/> to convert the string to.
        /// </typeparam>
        /// <returns>
        /// The <see cref="T"/>.
        /// </returns>
        public T ParseValue<T>(string value, CultureInfo culture = null)
        {
            return (T)this.ParseValue(typeof(T), value, culture);
        }

        /// <summary>
        /// Parses the given string value converting it to the given type.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type"/> to convert the string to.
        /// </param>
        /// <param name="value">
        /// The <see cref="String"/> value to parse.
        /// </param>
        /// <param name="culture">
        /// The <see cref="CultureInfo"/> to use as the current culture. 
        /// <remarks>If not set will parse using <see cref="CultureInfo.InvariantCulture"/></remarks>
        /// </param>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        public object ParseValue(Type type, string value, CultureInfo culture = null)
        {
            if (culture == null)
            {
                culture = CultureInfo.InvariantCulture;
            }

            TypeConverter converter = TypeDescriptor.GetConverter(type);
            try
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return converter.ConvertFrom(null, culture, HttpUtility.UrlDecode(value));
            }
            catch
            {
                // Return the default value
                return TypeDefaultsCache.GetOrAdd(type, t => this.GetDefaultValue(type));
            }
        }

        /// <summary>
        /// Adds a type converter to the parser.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type"/> to add a converter for.
        /// </param>
        /// <param name="converterType">
        /// The type of <see cref="TypeConverter"/> to add.
        /// </param>
        /// <returns>
        /// The <see cref="TypeDescriptionProvider"/>.
        /// </returns>
        public TypeDescriptionProvider AddTypeConverter(Type type, Type converterType)
        {
            return TypeDescriptor.AddAttributes(type, new TypeConverterAttribute(converterType));
        }

        /// <summary>
        /// Adds color converters.
        /// </summary>
        private void AddColorConverters()
        {
            this.AddTypeConverter(typeof(Color), typeof(ExtendedColorTypeConverter));
        }

        /// <summary>
        /// Adds font family converters.
        /// </summary>
        private void AddFontFamilyConverters()
        {
            this.AddTypeConverter(typeof(FontFamily), typeof(FontFamilyConverter));
        }

        /// <summary>
        /// Adds a selection of default list type converters.
        /// </summary>
        private void AddListConverters()
        {
            this.AddTypeConverter(typeof(List<sbyte>), typeof(GenericListTypeConverter<sbyte>));
            this.AddTypeConverter(typeof(List<byte>), typeof(GenericListTypeConverter<byte>));

            this.AddTypeConverter(typeof(List<short>), typeof(GenericListTypeConverter<short>));
            this.AddTypeConverter(typeof(List<ushort>), typeof(GenericListTypeConverter<ushort>));

            this.AddTypeConverter(typeof(List<int>), typeof(GenericListTypeConverter<int>));
            this.AddTypeConverter(typeof(List<uint>), typeof(GenericListTypeConverter<uint>));

            this.AddTypeConverter(typeof(List<long>), typeof(GenericListTypeConverter<long>));
            this.AddTypeConverter(typeof(List<ulong>), typeof(GenericListTypeConverter<ulong>));

            this.AddTypeConverter(typeof(List<decimal>), typeof(GenericListTypeConverter<decimal>));
            this.AddTypeConverter(typeof(List<float>), typeof(GenericListTypeConverter<float>));
            this.AddTypeConverter(typeof(List<double>), typeof(GenericListTypeConverter<double>));

            this.AddTypeConverter(typeof(List<string>), typeof(GenericListTypeConverter<string>));

            this.AddTypeConverter(typeof(List<Color>), typeof(GenericListTypeConverter<Color>));
        }

        /// <summary>
        /// Adds a selection of default array type converters.
        /// </summary>
        private void AddArrayConverters()
        {
            this.AddTypeConverter(typeof(sbyte[]), typeof(GenericArrayTypeConverter<sbyte>));
            this.AddTypeConverter(typeof(byte[]), typeof(GenericArrayTypeConverter<byte>));

            this.AddTypeConverter(typeof(short[]), typeof(GenericArrayTypeConverter<short>));
            this.AddTypeConverter(typeof(ushort[]), typeof(GenericArrayTypeConverter<ushort>));

            this.AddTypeConverter(typeof(int[]), typeof(GenericArrayTypeConverter<int>));
            this.AddTypeConverter(typeof(uint[]), typeof(GenericArrayTypeConverter<uint>));

            this.AddTypeConverter(typeof(long[]), typeof(GenericArrayTypeConverter<long>));
            this.AddTypeConverter(typeof(ulong[]), typeof(GenericArrayTypeConverter<ulong>));

            this.AddTypeConverter(typeof(decimal[]), typeof(GenericArrayTypeConverter<decimal>));
            this.AddTypeConverter(typeof(float[]), typeof(GenericArrayTypeConverter<float>));
            this.AddTypeConverter(typeof(double[]), typeof(GenericArrayTypeConverter<double>));

            this.AddTypeConverter(typeof(string[]), typeof(GenericArrayTypeConverter<string>));

            this.AddTypeConverter(typeof(Color[]), typeof(GenericArrayTypeConverter<Color>));
        }

        /// <summary>
        /// Returns the default value for the given type.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type"/> to return.
        /// </param>
        /// <returns>
        /// The <see cref="object"/> representing the default value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the given <see cref="Type"/> is null.
        /// </exception>
        private object GetDefaultValue(Type type)
        {
            // Validate parameters.
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            // We want an Func<object> which returns the default.
            // Create that expression here.
            // Have to convert to object.
            // The default value, always get what the *code* tells us.
            Expression<Func<object>> e =
                Expression.Lambda<Func<object>>(
                Expression.Convert(Expression.Default(type), typeof(object)));

            // Compile and return the value.
            return e.Compile()();
        }
    }
}

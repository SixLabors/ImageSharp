// <copyright file="CommandParser.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Net;

    /// <summary>
    /// Parses querystring values into usable commands for processors.
    /// </summary>
    public class CommandParser
    {
        /// <summary>
        /// A new instance of the <see cref="CommandParser"/> class with lazy initialization.
        /// </summary>
        private static readonly Lazy<CommandParser> Lazy = new Lazy<CommandParser>(() => new CommandParser());

        /// <summary>
        /// The cache for storing created default types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, object> TypeDefaultsCache = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Prevents a default instance of the <see cref="CommandParser"/> class from being created.
        /// </summary>
        private CommandParser()
        {
            this.AddSimpleConverters();
            this.AddListConverters();
            this.AddArrayConverters();
        }

        /// <summary>
        /// Gets the current <see cref="CommandParser"/> instance.
        /// </summary>
        public static CommandParser Instance => Lazy.Value;

        /// <summary>
        /// Adds a command converter to the parser.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to add a converter for. </param>
        /// <param name="converterType">The type of <see cref="CommandConverter"/> to add.</param>
        public void AddConverter(Type type, Type converterType)
        {
            CommandDescriptor.AddConverter(type, converterType);
        }

        /// <summary>
        /// Parses the given string value converting it to the given using the invariant culture.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <typeparam name="T">
        /// The <see cref="Type"/> to convert the string to.
        /// </typeparam>
        /// <returns>The converted instance or the default.</returns>
        public T ParseValue<T>(string value)
        {
            return this.ParseValue<T>(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the given string value converting it to the given type.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <param name="culture">The <see cref="CultureInfo"/> to use as the current culture.</param>
        /// <typeparam name="T">
        /// The <see cref="Type"/> to convert the string to.
        /// </typeparam>
        /// <returns>The converted instance or the default.</returns>
        public T ParseValue<T>(string value, CultureInfo culture)
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
        /// The <see cref="string"/> value to parse.
        /// </param>
        /// <param name="culture">
        /// The <see cref="CultureInfo"/> to use as the current culture.
        /// <remarks>If not set will parse using <see cref="CultureInfo.InvariantCulture"/></remarks>
        /// </param>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        internal object ParseValue(Type type, string value, CultureInfo culture)
        {
            if (culture == null)
            {
                culture = CultureInfo.InvariantCulture;
            }

            ICommandConverter converter = CommandDescriptor.GetConverter(type);
            try
            {
                return converter.ConvertFrom(culture, WebUtility.UrlDecode(value), type);
            }
            catch
            {
                // Return the default value
                return TypeDefaultsCache.GetOrAdd(type, t => this.GetDefaultValue(type));
            }
        }

        /// <summary>
        /// Add the generic converters
        /// </summary>
        private void AddSimpleConverters()
        {
            this.AddConverter(typeof(sbyte), typeof(IntegralNumberConverter<sbyte>));
            this.AddConverter(typeof(byte), typeof(IntegralNumberConverter<byte>));

            this.AddConverter(typeof(short), typeof(IntegralNumberConverter<short>));
            this.AddConverter(typeof(ushort), typeof(IntegralNumberConverter<ushort>));

            this.AddConverter(typeof(int), typeof(IntegralNumberConverter<int>));
            this.AddConverter(typeof(uint), typeof(IntegralNumberConverter<uint>));

            this.AddConverter(typeof(long), typeof(IntegralNumberConverter<long>));
            this.AddConverter(typeof(ulong), typeof(IntegralNumberConverter<ulong>));

            this.AddConverter(typeof(decimal), typeof(SimpleCommandConverter<decimal>));
            this.AddConverter(typeof(float), typeof(SimpleCommandConverter<float>));

            this.AddConverter(typeof(double), typeof(SimpleCommandConverter<double>));
            this.AddConverter(typeof(string), typeof(SimpleCommandConverter<string>));

            this.AddConverter(typeof(bool), typeof(SimpleCommandConverter<bool>));
        }

        /// <summary>
        /// Adds a selection of default list type converters.
        /// </summary>
        private void AddListConverters()
        {
            this.AddConverter(typeof(List<sbyte>), typeof(ListConverter<sbyte>));
            this.AddConverter(typeof(List<byte>), typeof(ListConverter<byte>));

            this.AddConverter(typeof(List<short>), typeof(ListConverter<short>));
            this.AddConverter(typeof(List<ushort>), typeof(ListConverter<ushort>));

            this.AddConverter(typeof(List<int>), typeof(ListConverter<int>));
            this.AddConverter(typeof(List<uint>), typeof(ListConverter<uint>));

            this.AddConverter(typeof(List<long>), typeof(ListConverter<long>));
            this.AddConverter(typeof(List<ulong>), typeof(ListConverter<ulong>));

            this.AddConverter(typeof(List<decimal>), typeof(ListConverter<decimal>));
            this.AddConverter(typeof(List<float>), typeof(ListConverter<float>));
            this.AddConverter(typeof(List<double>), typeof(ListConverter<double>));

            this.AddConverter(typeof(List<string>), typeof(ListConverter<string>));
        }

        /// <summary>
        /// Adds a selection of default array type converters.
        /// </summary>
        private void AddArrayConverters()
        {
            this.AddConverter(typeof(sbyte[]), typeof(ArrayConverter<sbyte>));
            this.AddConverter(typeof(byte[]), typeof(ArrayConverter<byte>));

            this.AddConverter(typeof(short[]), typeof(ArrayConverter<short>));
            this.AddConverter(typeof(ushort[]), typeof(ArrayConverter<ushort>));

            this.AddConverter(typeof(int[]), typeof(ArrayConverter<int>));
            this.AddConverter(typeof(uint[]), typeof(ArrayConverter<uint>));

            this.AddConverter(typeof(long[]), typeof(ArrayConverter<long>));
            this.AddConverter(typeof(ulong[]), typeof(ArrayConverter<ulong>));

            this.AddConverter(typeof(decimal[]), typeof(ArrayConverter<decimal>));
            this.AddConverter(typeof(float[]), typeof(ArrayConverter<float>));
            this.AddConverter(typeof(double[]), typeof(ArrayConverter<double>));

            this.AddConverter(typeof(string[]), typeof(ArrayConverter<string>));
        }

        /// <summary>
        /// Returns the default value for the given type.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to return.</param>
        /// <returns>The <see cref="object"/> representing the default value.</returns>
        private object GetDefaultValue(Type type)
        {
            Guard.NotNull(type, nameof(type));

            // We want an Func<object> which returns the default value.
            // Create that expression, convert to object.
            // The default value, will always be what the *code* tells us.
            var e = Expression.Lambda<Func<object>>(Expression.Convert(Expression.Default(type), typeof(object)));

            return e.Compile()();
        }
    }
}
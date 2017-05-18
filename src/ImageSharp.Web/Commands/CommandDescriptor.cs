namespace ImageSharp.Web.Commands
{
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;

    /// <summary>
    /// Hold the collection of <see cref="ICommandConverter"/> converters
    /// </summary>
    internal static class CommandDescriptor
    {
        /// <summary>
        /// A reusable enum converter
        /// </summary>
        private static readonly ICommandConverter EnumConverter = (ICommandConverter)Activator.CreateInstance(typeof(EnumConverter));

        /// <summary>
        /// The converter cache.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, ICommandConverter> ConverterCache = new ConcurrentDictionary<Type, ICommandConverter>();

        /// <summary>
        /// Returns an instance of the correct converter for the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>
        /// The <see cref="ICommandConverter"/>.
        /// </returns>
        public static ICommandConverter GetConverter(Type type)
        {
            if (type.GetTypeInfo().IsEnum)
            {
                return EnumConverter;
            }

            return ConverterCache.ContainsKey(type) ? ConverterCache[type] : null;
        }

        /// <summary>
        /// Adds the given converter for the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="converter">The converter.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if the converter does not implement <see cref="ICommandConverter"/>.
        /// </exception>
        public static void AddConverter(Type type, Type converter)
        {
            Guard.IsTrue(typeof(ICommandConverter).IsAssignableFrom(converter), nameof(converter), "Converter does not implement ICommandConverter.");

            if (ConverterCache.ContainsKey(type))
            {
                return;
            }

            ConverterCache[type] = (ICommandConverter)Activator.CreateInstance(converter);
        }
    }
}
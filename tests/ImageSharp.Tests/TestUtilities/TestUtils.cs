// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Various utility and extension methods.
    /// </summary>
    public static class TestUtils
    {
        private static readonly Dictionary<Type, PixelTypes> ClrTypes2PixelTypes = new Dictionary<Type, PixelTypes>();

        private static readonly Assembly ImageSharpAssembly = typeof(Rgba32).GetTypeInfo().Assembly;

        private static readonly Dictionary<PixelTypes, Type> PixelTypes2ClrTypes = new Dictionary<PixelTypes, Type>();

        private static readonly PixelTypes[] AllConcretePixelTypes = GetAllPixelTypes()
            .Except(new[] { PixelTypes.Undefined, PixelTypes.All })
            .ToArray();

        static TestUtils()
        {
            // Add Rgba32 Our default.
            Type defaultPixelFormatType = typeof(Rgba32);
            PixelTypes2ClrTypes[PixelTypes.Rgba32] = defaultPixelFormatType;
            ClrTypes2PixelTypes[defaultPixelFormatType] = PixelTypes.Rgba32;

            // Add PixelFormat types
            string nameSpace = typeof(Alpha8).FullName;
            nameSpace = nameSpace.Substring(0, nameSpace.Length - typeof(Alpha8).Name.Length - 1);
            foreach (PixelTypes pt in AllConcretePixelTypes.Where(pt => pt != PixelTypes.Rgba32))
            {
                string typeName = $"{nameSpace}.{pt}";
                Type t = ImageSharpAssembly.GetType(typeName);
                PixelTypes2ClrTypes[pt] = t ?? throw new InvalidOperationException($"Could not find: {typeName}");
                ClrTypes2PixelTypes[t] = pt;
            }
        }

        public static bool HasFlag(this PixelTypes pixelTypes, PixelTypes flag) => (pixelTypes & flag) == flag;

        public static bool IsEquivalentTo<TPixel>(this Image<TPixel> a, Image<TPixel> b, bool compareAlpha = true)
            where TPixel : struct, IPixel<TPixel>
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return false;
            }

            var rgb1 = default(Rgb24);
            var rgb2 = default(Rgb24);

            using (PixelAccessor<TPixel> pixA = a.Lock())
            {
                using (PixelAccessor<TPixel> pixB = b.Lock())
                {
                    for (int y = 0; y < a.Height; y++)
                    {
                        for (int x = 0; x < a.Width; x++)
                        {
                            TPixel ca = pixA[x, y];
                            TPixel cb = pixB[x, y];

                            if (compareAlpha)
                            {
                                if (!ca.Equals(cb))
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                ca.ToRgb24(ref rgb1);
                                cb.ToRgb24(ref rgb2);

                                if (rgb1.R != rgb2.R ||
                                    rgb1.G != rgb2.G ||
                                    rgb1.B != rgb2.B)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        public static string ToCsv<T>(this IEnumerable<T> items, string separator = ",")
        {
            return string.Join(separator, items.Select(o => string.Format(CultureInfo.InvariantCulture, "{0}", o)));
        }

        public static Type GetClrType(this PixelTypes pixelType) => PixelTypes2ClrTypes[pixelType];

        /// <summary>
        /// Returns the <see cref="PixelTypes"/> enumerations for the given type.
        /// </summary>
        /// <param name="colorStructClrType"></param>
        /// <returns></returns>
        public static PixelTypes GetPixelType(this Type colorStructClrType) => ClrTypes2PixelTypes[colorStructClrType];



        public static IEnumerable<KeyValuePair<PixelTypes, Type>> ExpandAllTypes(this PixelTypes pixelTypes)
        {
            if (pixelTypes == PixelTypes.Undefined)
            {
                return Enumerable.Empty<KeyValuePair<PixelTypes, Type>>();
            }
            else if (pixelTypes == PixelTypes.All)
            {
                // TODO: Need to return unknown types here without forcing CLR to load all types in ImageSharp assembly
                return PixelTypes2ClrTypes;
            }

            var result = new Dictionary<PixelTypes, Type>();
            foreach (PixelTypes pt in AllConcretePixelTypes)
            {
                if (pixelTypes.HasAll(pt))
                {
                    result[pt] = pt.GetClrType();
                }
            }
            return result;
        }

        internal static bool HasAll(this PixelTypes pixelTypes, PixelTypes flagsToCheck) =>
            (pixelTypes & flagsToCheck) == flagsToCheck;

        /// <summary>
        /// Enumerate all available <see cref="PixelTypes"/>-s
        /// </summary>
        /// <returns>The pixel types</returns>
        internal static PixelTypes[] GetAllPixelTypes() => (PixelTypes[])Enum.GetValues(typeof(PixelTypes));
    }
}
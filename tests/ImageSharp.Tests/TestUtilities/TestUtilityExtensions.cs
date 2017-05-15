// <copyright file="TestUtilityExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Extension methods for TestUtilities
    /// </summary>
    public static class TestUtilityExtensions
    {
        private static readonly Dictionary<Type, PixelTypes> ClrTypes2PixelTypes = new Dictionary<Type, PixelTypes>();

        private static readonly Assembly ImageSharpAssembly = typeof(Rgba32).GetTypeInfo().Assembly;

        private static readonly Dictionary<PixelTypes, Type> PixelTypes2ClrTypes = new Dictionary<PixelTypes, Type>();

        private static readonly PixelTypes[] AllConcretePixelTypes = GetAllPixelTypes()
            .Except(new[] { PixelTypes.Undefined, PixelTypes.All })
            .ToArray();

        static TestUtilityExtensions()
        {
            // Add Rgba32 Our default.
            Type defaultPixelFormatType = typeof(Rgba32);
            PixelTypes2ClrTypes[PixelTypes.Rgba32] = defaultPixelFormatType;
            ClrTypes2PixelTypes[defaultPixelFormatType] = PixelTypes.Rgba32;

            // Add PixelFormat types
            string nameSpace = typeof(Alpha8).FullName;
            nameSpace = nameSpace.Substring(0, nameSpace.Length - typeof(Alpha8).Name.Length - 1);
            foreach (PixelTypes pt in AllConcretePixelTypes.Where(pt => pt != PixelTypes.StandardImageClass && pt != PixelTypes.Rgba32))
            {
                string typeName = $"{nameSpace}.{pt}";
                Type t = ImageSharpAssembly.GetType(typeName);
                PixelTypes2ClrTypes[pt] = t ?? throw new InvalidOperationException($"Could not find: {typeName}");
                ClrTypes2PixelTypes[t] = pt;
            }
            PixelTypes2ClrTypes[PixelTypes.StandardImageClass] = defaultPixelFormatType;
        }

        public static bool HasFlag(this PixelTypes pixelTypes, PixelTypes flag) => (pixelTypes & flag) == flag;

        public static bool IsEquivalentTo<TPixel>(this Image<TPixel> a, Image<TPixel> b, bool compareAlpha = true)
            where TPixel : struct, IPixel<TPixel>
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return false;
            }

            byte[] bytesA = new byte[3];
            byte[] bytesB = new byte[3];

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
                                ca.ToXyzBytes(bytesA, 0);
                                cb.ToXyzBytes(bytesB, 0);

                                if (bytesA[0] != bytesB[0] ||
                                    bytesA[1] != bytesB[1] ||
                                    bytesA[2] != bytesB[2])
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

        public static Type ToType(this PixelTypes pixelType) => PixelTypes2ClrTypes[pixelType];

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

            return AllConcretePixelTypes
                .Where(pt => pixelTypes.HasFlag(pt))
                .Select(pt => new KeyValuePair<PixelTypes, Type>(pt, pt.ToType()));
        }

        /// <summary>
        /// Enumerate all available <see cref="PixelTypes"/>-s
        /// </summary>
        /// <returns>The pixel types</returns>
        internal static PixelTypes[] GetAllPixelTypes() => (PixelTypes[])Enum.GetValues(typeof(PixelTypes));
    }
}
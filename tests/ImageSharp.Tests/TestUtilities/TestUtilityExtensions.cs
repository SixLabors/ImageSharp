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

    /// <summary>
    /// Extension methods for TestUtilities
    /// </summary>
    public static class TestUtilityExtensions
    {
        private static readonly Dictionary<Type, PixelTypes> ClrTypes2PixelTypes = new Dictionary<Type, PixelTypes>();

        private static readonly Assembly ImageSharpAssembly = typeof(Color).GetTypeInfo().Assembly;

        private static readonly Dictionary<PixelTypes, Type> PixelTypes2ClrTypes = new Dictionary<PixelTypes, Type>();

        private static readonly PixelTypes[] AllConcretePixelTypes = GetAllPixelTypes()
            .Except(new [] {PixelTypes.Undefined, PixelTypes.All })
            .ToArray();

        static TestUtilityExtensions()
        {
            string nameSpace = typeof(Color).FullName;
            nameSpace = nameSpace.Substring(0, nameSpace.Length - typeof(Color).Name.Length - 1);
            foreach (PixelTypes pt in AllConcretePixelTypes.Where(pt => pt != PixelTypes.StandardImageClass))
            {
                string typeName = $"{nameSpace}.{pt.ToString()}";
                var t = ImageSharpAssembly.GetType(typeName);
                if (t == null)
                {
                    throw new InvalidOperationException($"Could not find: {typeName}");
                }

                PixelTypes2ClrTypes[pt] = t;
                ClrTypes2PixelTypes[t] = pt;
            }
            PixelTypes2ClrTypes[PixelTypes.StandardImageClass] = typeof(Color);
        }

        public static Type GetPackedType(Type pixelType)
        {
            var intrfcType =
                pixelType.GetInterfaces()
                    .Single(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IPackedPixel<>));

            return intrfcType.GetGenericArguments().Single();
        }

        public static bool HasFlag(this PixelTypes pixelTypes, PixelTypes flag) => (pixelTypes & flag) == flag;

        public static bool IsEquivalentTo<TColor>(this Image<TColor> a, Image<TColor> b, bool compareAlpha = true)
            where TColor : struct, IPixel<TColor>
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return false;
            }

            byte[] bytesA = new byte[3];
            byte[] bytesB = new byte[3];

            using (var pixA = a.Lock())
            {
                using (var pixB = b.Lock())
                {
                    for (int y = 0; y < a.Height; y++)
                    {
                        for (int x = 0; x < a.Width; x++)
                        {
                            var ca = pixA[x, y];
                            var cb = pixB[x, y];

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
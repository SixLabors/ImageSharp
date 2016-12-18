namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using Xunit;

    public static class TestUtilityExtensions
    {
        private static readonly PixelTypes[] PixelTypesExpanded =
            FlagsHelper<PixelTypes>.GetSortedValues().Where(t => t != PixelTypes.All && t != PixelTypes.None).ToArray();

        private static readonly Assembly ImageSharpAssembly = typeof(Color).GetTypeInfo().Assembly;

        private static readonly Dictionary<PixelTypes, Type> PixelTypes2ClrTypes = new Dictionary<PixelTypes, Type>();

        static TestUtilityExtensions()
        {
            Assembly assembly = typeof(Color).GetTypeInfo().Assembly;
            string nameSpace = typeof(Color).FullName;
            nameSpace = nameSpace.Substring(0, nameSpace.Length - typeof(Color).Name.Length - 1);
            foreach (PixelTypes pt in PixelTypesExpanded)
            {
                string typeName = $"{nameSpace}.{FlagsHelper<PixelTypes>.ToString(pt)}";
                var t = assembly.GetType(typeName);
                if (t != null)
                {
                    PixelTypes2ClrTypes[pt] = t;
                }
            }
        }

        public static bool HasFlag(this PixelTypes pixelTypes, PixelTypes flag) => (pixelTypes & flag) == flag;

        public static Type ToType(this PixelTypes pixelType) => PixelTypes2ClrTypes[pixelType];

        public static IEnumerable<Type> ToTypes(this PixelTypes pixelTypes)
        {
            if (pixelTypes == PixelTypes.None)
            {
                return Enumerable.Empty<Type>();
            }
            else if (pixelTypes == PixelTypes.All)
            {
                // TODO: Need to return unknown types here without forcing CLR to load all types in ImageSharp assembly
                return PixelTypes2ClrTypes.Values;
            }
            return PixelTypesExpanded.Where(pt => pixelTypes.HasFlag(pt)).Select(pt => pt.ToType());
        }

        public static Type GetPackedType(Type pixelType)
        {
            var intrfcType =
                pixelType.GetInterfaces()
                    .Single(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IPackedPixel<>));

            return intrfcType.GetGenericArguments().Single();
        }

        public static bool IsEquivalentTo<TColor, TPacked>(this Image<TColor, TPacked> a, Image<TColor, TPacked> b, bool compareAlpha = true)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
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
                                ca.ToBytes(bytesA, 0, ComponentOrder.XYZ);
                                cb.ToBytes(bytesB, 0, ComponentOrder.XYZ);
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
    }
}
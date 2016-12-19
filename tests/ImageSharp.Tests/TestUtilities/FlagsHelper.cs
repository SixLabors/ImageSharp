// <copyright file="FlagsHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Helper class for flag manipulation, based on
    /// <see>
    ///     <cref>http://www.codeproject.com/KB/dotnet/enum.aspx</cref>
    /// </see>
    /// </summary>
    /// <typeparam name="T">Must be enum type (declared using <c>enum</c> keyword)</typeparam>
    public class FlagsHelper<T>
        where T : struct, IConvertible
    {
        private static readonly EnumConverter Converter;

        static FlagsHelper()
        {
            Type type = typeof(T);
            string[] names = Enum.GetNames(type);
            var values = (T[])Enum.GetValues(type);

            Converter = new FlagsEnumConverter(names, values);
        }

        public static T[] GetSortedValues()
        {
            T[] vals = (T[])Enum.GetValues(typeof(T));
            Array.Sort(vals);
            return vals;
        }

        public static T Parse(string value, bool ignoreCase = false, bool parseNumeric = true)
        {
            return (T)Enum.ToObject(typeof(T), Converter.ParseInternal(value, ignoreCase, parseNumeric));
        }

        /// <summary>
        /// Converts enum value to string
        /// </summary>
        /// <param name="value">Enum value converted to int</param>
        /// <returns>If <paramref name="value"/> is defined, the enum member name; otherwise the string representation of the <paramref name="value"/>.
        /// If <see cref="FlagsAttribute"/> is applied, can return comma-separated list of values</returns>
        public static string ToString(int value)
        {
            return Converter.ToStringInternal(value);
        }

        /// <summary>
        /// Converts enum value to string
        /// </summary>
        /// <param name="value">Enum value</param>
        /// <returns>If <paramref name="value"/> is defined, the enum member name; otherwise the string representation of the <paramref name="value"/>.
        /// If <see cref="FlagsAttribute"/> is applied, can return comma-separated list of values</returns>
        public static string ToString(T value)
        {
            return Converter.ToStringInternal(value.ToInt32(null));
        }

        public static bool TryParse(string value, bool ignoreCase, bool parseNumeric, out T result)
        {
            int ir;
            bool b = Converter.TryParseInternal(value, ignoreCase, parseNumeric, out ir);
            result = (T)Enum.ToObject(typeof(T), ir);
            return b;
        }

        public static bool TryParse(string value, bool ignoreCase, out T result)
        {
            int ir;
            bool b = Converter.TryParseInternal(value, ignoreCase, true, out ir);
            result = (T)Enum.ToObject(typeof(T), ir);
            return b;
        }

        public static bool TryParse(string value, out T result)
        {
            int ir;
            bool b = Converter.TryParseInternal(value, false, true, out ir);
            result = (T)Enum.ToObject(typeof(T), ir);
            return b;
        }

        class DictionaryEnumConverter : EnumConverter
        {
            protected readonly Dictionary<int, string> Dic;

            public DictionaryEnumConverter(string[] names, T[] values)
            {
                this.Dic = new Dictionary<int, string>(names.Length);
                for (int j = 0; j < names.Length; j++) this.Dic.Add(Convert.ToInt32(values[j], null), names[j]);
            }

            public override int ParseInternal(string value, bool ignoreCase, bool parseNumber)
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Length == 0) throw new ArgumentException("Value is empty", nameof(value));
                char f = value[0];
                if (parseNumber && (char.IsDigit(f) || f == '+' || f == '-')) return int.Parse(value);
                StringComparison stringComparison = ignoreCase
                                                        ? StringComparison.OrdinalIgnoreCase
                                                        : StringComparison.Ordinal;
                foreach (KeyValuePair<int, string> pair in this.Dic)
                {
                    if (pair.Value.Equals(value, stringComparison)) return pair.Key;
                }

                throw new ArgumentException("Enum value wasn't found", nameof(value));
            }

            public override string ToStringInternal(int value)
            {
                string n;
                return this.Dic.TryGetValue(value, out n) ? n : value.ToString();
            }

            public override bool TryParseInternal(string value, bool ignoreCase, bool parseNumber, out int result)
            {
                result = 0;
                if (string.IsNullOrEmpty(value)) return false;
                char f = value[0];
                if (parseNumber && (char.IsDigit(f) || f == '+' || f == '-'))
                {
                    int i;
                    if (int.TryParse(value, out i))
                    {
                        result = i;
                        return true;
                    }

                    return false;
                }

                StringComparison stringComparison = ignoreCase
                                                        ? StringComparison.OrdinalIgnoreCase
                                                        : StringComparison.Ordinal;
                foreach (KeyValuePair<int, string> pair in this.Dic)
                {
                    if (pair.Value.Equals(value, stringComparison))
                    {
                        result = pair.Key;
                        return true;
                    }
                }

                return false;
            }
        }

        abstract class EnumConverter
        {
            public abstract int ParseInternal(string value, bool ignoreCase, bool parseNumber);

            public abstract string ToStringInternal(int value);

            public abstract bool TryParseInternal(string value, bool ignoreCase, bool parseNumber, out int result);
        }

        class FlagsEnumConverter : DictionaryEnumConverter
        {
            private static readonly string[] Seps = new[] { "," };

            private readonly uint[] values;

            public FlagsEnumConverter(string[] names, T[] values)
                : base(names, values)
            {
                this.values = new uint[values.Length];
                for (int i = 0; i < values.Length; i++) this.values[i] = values[i].ToUInt32(null);
            }

            public override int ParseInternal(string value, bool ignoreCase, bool parseNumber)
            {
                string[] parts = value.Split(Seps, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return base.ParseInternal(value, ignoreCase, parseNumber);
                int val = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    int t = base.ParseInternal(part.Trim(), ignoreCase, parseNumber);
                    val |= t;
                }

                return val;
            }

            public override string ToStringInternal(int value)
            {
                string n;
                if (this.Dic.TryGetValue(value, out n)) return n;
                var sb = new StringBuilder();
                const string sep = ", ";
                uint uval;
                unchecked
                {
                    uval = (uint)value;

                    for (int i = this.values.Length - 1; i >= 0; i--)
                    {
                        uint v = this.values[i];
                        if (v == 0) continue;
                        if ((v & uval) == v)
                        {
                            uval &= ~v;
                            sb.Insert(0, sep).Insert(0, this.Dic[(int)v]);
                        }
                    }
                }

                return uval == 0 && sb.Length > sep.Length ? sb.ToString(0, sb.Length - sep.Length) : value.ToString();
            }

            public override bool TryParseInternal(string value, bool ignoreCase, bool parseNumber, out int result)
            {
                string[] parts = value.Split(Seps, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return base.TryParseInternal(value, ignoreCase, parseNumber, out result);
                int val = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    int t;
                    if (!base.TryParseInternal(part.Trim(), ignoreCase, parseNumber, out t))
                    {
                        result = 0;
                        return false;
                    }

                    val |= t;
                }

                result = val;
                return true;
            }
        }
    }
}
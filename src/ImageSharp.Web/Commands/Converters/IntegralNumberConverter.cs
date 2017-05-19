// <copyright file="IntegralNumberConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands.Converters
{
    using System;
    using System.Globalization;

    /// <summary>
    /// The generic converter for integral types.
    /// </summary>
    /// <typeparam name="T">The type of object to convert to.</typeparam>
    internal sealed class IntegralNumberConverter<T> : CommandConverter
        where T : struct, IConvertible, IComparable<T>
    {
        /// <inheritdoc/>
        public override object ConvertFrom(CultureInfo culture, string value, Type propertyType)
        {
            if (value == null || Array.IndexOf(TypeConstants.IntegralTypes, propertyType) < 0)
            {
                return base.ConvertFrom(culture, null, propertyType);
            }

            // Round the value to the nearest decimal value
            decimal rounded = Math.Round((decimal)Convert.ChangeType(value, typeof(decimal), culture), MidpointRounding.AwayFromZero);

            // Now clamp it to the type ranges
            if (propertyType == TypeConstants.Sbyte)
            {
                rounded = rounded.Clamp(sbyte.MinValue, sbyte.MaxValue);
            }
            else if (propertyType == TypeConstants.Byte)
            {
                rounded = rounded.Clamp(byte.MinValue, byte.MaxValue);
            }
            else if (propertyType == TypeConstants.Short)
            {
                rounded = rounded.Clamp(short.MinValue, short.MaxValue);
            }
            else if (propertyType == TypeConstants.UShort)
            {
                rounded = rounded.Clamp(ushort.MinValue, ushort.MaxValue);
            }
            else if (propertyType == TypeConstants.Int)
            {
                rounded = rounded.Clamp(int.MinValue, int.MaxValue);
            }
            else if (propertyType == TypeConstants.UInt)
            {
                rounded = rounded.Clamp(uint.MinValue, uint.MaxValue);
            }
            else if (propertyType == TypeConstants.Long)
            {
                rounded = rounded.Clamp(long.MinValue, long.MaxValue);
            }
            else if (propertyType == TypeConstants.ULong)
            {
                rounded = rounded.Clamp(ulong.MinValue, ulong.MaxValue);
            }

            // Now it's rounded an clamped we should be able to correctly parse the string.
            return (T)Convert.ChangeType(rounded.ToString(CultureInfo.InvariantCulture), typeof(T), culture);
        }
    }
}
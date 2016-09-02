// <copyright file="ComparableExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;

    /// <summary>
    /// Extension methods for classes that implement <see cref="IComparable{T}"/>.
    /// </summary>
    internal static class ComparableExtensions
    {
        /// <summary>
        /// Restricts a <see cref="byte"/> to be within a specified range.
        /// </summary>
        /// <param name="value">The The value to clamp.</param>
        /// <param name="min">The minimum value. If value is less than min, min will be returned.</param>
        /// <param name="max">The maximum value. If value is greater than max, max will be returned.</param>
        /// <returns>
        /// The <see cref="byte"/> representing the clamped value.
        /// </returns>
        public static byte Clamp(this byte value, byte min, byte max)
        {
            // Order is important here as someone might set min to higher than max.
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Restricts a <see cref="uint"/> to be within a specified range.
        /// </summary>
        /// <param name="value">The The value to clamp.</param>
        /// <param name="min">The minimum value. If value is less than min, min will be returned.</param>
        /// <param name="max">The maximum value. If value is greater than max, max will be returned.</param>
        /// <returns>
        /// The <see cref="int"/> representing the clamped value.
        /// </returns>
        public static uint Clamp(this uint value, uint min, uint max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Restricts a <see cref="int"/> to be within a specified range.
        /// </summary>
        /// <param name="value">The The value to clamp.</param>
        /// <param name="min">The minimum value. If value is less than min, min will be returned.</param>
        /// <param name="max">The maximum value. If value is greater than max, max will be returned.</param>
        /// <returns>
        /// The <see cref="int"/> representing the clamped value.
        /// </returns>
        public static int Clamp(this int value, int min, int max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Restricts a <see cref="float"/> to be within a specified range.
        /// </summary>
        /// <param name="value">The The value to clamp.</param>
        /// <param name="min">The minimum value. If value is less than min, min will be returned.</param>
        /// <param name="max">The maximum value. If value is greater than max, max will be returned.</param>
        /// <returns>
        /// The <see cref="float"/> representing the clamped value.
        /// </returns>
        public static float Clamp(this float value, float min, float max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Restricts a <see cref="double"/> to be within a specified range.
        /// </summary>
        /// <param name="value">The The value to clamp.</param>
        /// <param name="min">The minimum value. If value is less than min, min will be returned.</param>
        /// <param name="max">The maximum value. If value is greater than max, max will be returned.</param>
        /// <returns>
        /// The <see cref="double"/> representing the clamped value.
        /// </returns>
        public static double Clamp(this double value, double min, double max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Converts an <see cref="int"/> to a <see cref="byte"/> first restricting the value between the
        /// minimum and maximum allowable ranges.
        /// </summary>
        /// <param name="value">The <see cref="int"/> this method extends.</param>
        /// <returns>The <see cref="byte"/></returns>
        public static byte ToByte(this int value)
        {
            return (byte)value.Clamp(0, 255);
        }

        /// <summary>
        /// Converts an <see cref="float"/> to a <see cref="byte"/> first restricting the value between the
        /// minimum and maximum allowable ranges.
        /// </summary>
        /// <param name="value">The <see cref="float"/> this method extends.</param>
        /// <returns>The <see cref="byte"/></returns>
        public static byte ToByte(this float value)
        {
            return (byte)value.Clamp(0, 255);
        }

        /// <summary>
        /// Converts an <see cref="double"/> to a <see cref="byte"/> first restricting the value between the
        /// minimum and maximum allowable ranges.
        /// </summary>
        /// <param name="value">The <see cref="double"/> this method extends.</param>
        /// <returns>The <see cref="byte"/></returns>
        public static byte ToByte(this double value)
        {
            return (byte)value.Clamp(0, 255);
        }
    }
}

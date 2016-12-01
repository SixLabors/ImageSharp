// <copyright file="PackedPixelConverterHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Assists with the conversion of known packed pixel formats from one to another.
    /// </summary>
    internal static class PackedPixelConverterHelper
    {
        /// <summary>
        /// A non operative function. Simply returns the original vector.
        /// </summary>
        private static readonly Func<Vector4, Vector4> Noop = vector4 => vector4;

        /// <summary>
        /// Returns the correct scaling function for the given types The compute scale function.
        /// </summary>
        /// <param name="scaleFunc">The scale function.</param>
        /// <typeparam name="TColor">The source pixel format.</typeparam>
        /// <typeparam name="TColor2">The target pixel format.</typeparam>
        /// <returns>The <see cref="Func{Vector4,Vector4}"/></returns>
        public static Func<Vector4, Vector4> ComputeScaleFunction<TColor, TColor2>(Func<Vector4, Vector4> scaleFunc)
        {
            // Custom type with a custom function.
            if (scaleFunc != null)
            {
                return scaleFunc;
            }

            Type source = typeof(TColor);
            Type target = typeof(TColor2);

            // Standard to offset
            if (IsStandardNormalizedType(source))
            {
                if (IsOffsetNormalizedType(target) || IsOffsetTwoComponentNormalizedType(target))
                {
                    // Expand the range then offset the center down.
                    return vector4 => (2F * vector4) - Vector4.One;
                }

                if (IsOffsetType(target) || IsOffsetTwoComponentType(target))
                {
                    return v => (65534 * v) - new Vector4(32767);
                }
            }

            // Normalized offsets. All four components. 
            if (IsOffsetNormalizedType(source))
            {
                return FromOffsetNormalizedType(target);
            }

            // Offset. All four components. 
            if (IsOffsetType(source))
            {
                return FromOffsetType(target);
            }

            // Normalized offsets. First component pair only. 
            if (IsOffsetTwoComponentNormalizedType(source))
            {
                return FromOffsetTwoComponentNormalizedType(target);
            }

            // Offsets. First component pair only. 
            if (IsOffsetTwoComponentType(source))
            {
                return FromOffsetTwoComponentType(target);
            }

            return Noop;
        }

        /// <summary>
        /// Returns the correct conversion function to convert from types having vector values representing all four components
        /// ranging from -1 to 1.
        /// </summary>
        /// <param name="target">The target type</param>
        /// <returns>The <see cref="Func{Vector4,Vector4}"/></returns>
        private static Func<Vector4, Vector4> FromOffsetNormalizedType(Type target)
        {
            if (IsStandardNormalizedType(target))
            {
                // Compress the range then offset the center up.
                return vector4 => (vector4 / 2F) + new Vector4(.5F);
            }

            if (IsOffsetType(target) || IsOffsetTwoComponentType(target))
            {
                // Multiply out the range, two component won't read the last two values.
                return vector4 => (vector4 * 32767F);
            }

            return Noop;
        }

        /// <summary>
        /// Returns the correct conversion function to convert from types having vector values representing all four components
        /// ranging from -32767 to 32767.
        /// </summary>
        /// <param name="target">The target type</param>
        /// <returns>The <see cref="Func{Vector4,Vector4}"/></returns>
        private static Func<Vector4, Vector4> FromOffsetType(Type target)
        {
            if (IsStandardNormalizedType(target))
            {
                // Compress the range then offset the center up.
                return vector4 => (vector4 / 65534) + new Vector4(.5F);
            }

            if (IsOffsetNormalizedType(target) || IsOffsetTwoComponentNormalizedType(target))
            {
                // Compress the range. Two component won't read the last two values.
                return vector4 => (vector4 / 32767);
            }

            return Noop;
        }

        /// <summary>
        /// Returns the correct conversion function to convert from types having vector with the first component pair ranging from -1 to 1.
        /// and the second component pair ranging from 0 to 1.
        /// </summary>
        /// <param name="target">The target type</param>
        /// <returns>The <see cref="Func{Vector4,Vector4}"/></returns>
        private static Func<Vector4, Vector4> FromOffsetTwoComponentNormalizedType(Type target)
        {
            if (IsStandardNormalizedType(target))
            {
                return vector4 =>
                {
                    // Compress the range then offset the center up for first pair. 
                    Vector4 v = (vector4 / 2F) + new Vector4(.5F);
                    return new Vector4(v.X, v.Y, 0, 1);
                };
            }

            if (IsOffsetNormalizedType(target))
            {
                // Copy the first two components and set second pair to 0 and 1 equivalent.
                return vector4 => new Vector4(vector4.X, vector4.Y, -1, 1);
            }

            if (IsOffsetTwoComponentType(target))
            {
                // Multiply. Two component won't read the last two values.
                return vector4 => (vector4 * 32767);
            }

            if (IsOffsetType(target))
            {
                return vector4 =>
                {
                    // Multiply the first two components and set second pair to 0 and 1 equivalent.
                    Vector4 v = vector4 * 32767;
                    return new Vector4(v.X, v.Y, -32767, 32767);
                };
            }

            return Noop;
        }

        /// <summary>
        /// Returns the correct conversion function to convert from types having vector with the first component pair ranging from -32767 to 32767.
        /// and the second component pair ranging from 0 to 1.
        /// </summary>
        /// <param name="target">The target type</param>
        /// <returns>The <see cref="Func{Vector4,Vector4}"/></returns>
        private static Func<Vector4, Vector4> FromOffsetTwoComponentType(Type target)
        {
            if (IsStandardNormalizedType(target))
            {
                return vector4 =>
                {
                    Vector4 v = (vector4 / 65534) + new Vector4(.5F);
                    return new Vector4(v.X, v.Y, 0, 1);
                };
            }

            if (IsOffsetType(target))
            {
                // Copy the first two components and set second pair to 0 and 1 equivalent.
                return vector4 => new Vector4(vector4.X, vector4.Y, -32767, 32767);
            }

            if (IsOffsetNormalizedType(target))
            {
                return vector4 =>
                {
                    // Divide the first two components and set second pair to 0 and 1 equivalent.
                    Vector4 v = vector4 / 32767;
                    return new Vector4(v.X, v.Y, -1, 1);
                };
            }

            if (IsOffsetTwoComponentNormalizedType(target))
            {
                // Divide. Two component won't read the last two values.
                return vector4 => (vector4 / 32767);
            }

            return Noop;
        }

        /// <summary>
        /// Identifies the type as having vector component values ranging from 0 to 1.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsStandardNormalizedType(Type type)
        {
            return type == typeof(Color)
                || type == typeof(Bgr565)
                || type == typeof(Bgra4444)
                || type == typeof(Bgra5551)
                || type == typeof(Byte4)
                || type == typeof(HalfSingle)
                || type == typeof(HalfVector2)
                || type == typeof(HalfVector4)
                || type == typeof(Rg32)
                || type == typeof(Rgba1010102)
                || type == typeof(Rgba64);
        }

        /// <summary>
        /// Identifies the type as having vector values representing the first component pair ranging from -1 to 1.
        /// and the second component pair ranging from 0 to 1.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsOffsetTwoComponentNormalizedType(Type type)
        {
            return type == typeof(NormalizedByte2)
                || type == typeof(NormalizedShort2);
        }

        /// <summary>
        /// Identifies the type as having vector values representing all four components ranging from -1 to 1.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsOffsetNormalizedType(Type type)
        {
            return type == typeof(NormalizedByte4)
                || type == typeof(NormalizedShort4);
        }

        /// <summary>
        /// Identifies the type as having vector values representing the first component pair ranging from -32767 to 32767.
        /// and the second component pair ranging from 0 to 1.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsOffsetTwoComponentType(Type type)
        {
            return type == typeof(Short2);
        }

        /// <summary>
        /// Identifies the type as having vector values representing all four components ranging from -32767 to 32767.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsOffsetType(Type type)
        {
            return type == typeof(Short4);
        }
    }
}
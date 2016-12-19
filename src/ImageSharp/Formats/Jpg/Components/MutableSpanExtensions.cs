// <copyright file="MutableSpanExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg.Components
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// MutableSpan Extensions
    /// </summary>
    internal static class MutableSpanExtensions
    {
        /// <summary>
        /// Slice <see cref="MutableSpan{T}"/>
        /// </summary>
        /// <typeparam name="T">The type of the data in the span</typeparam>
        /// <param name="array">The data array</param>
        /// <param name="offset">The offset</param>
        /// <returns>The new <see cref="MutableSpan{T}"/></returns>
        public static MutableSpan<T> Slice<T>(this T[] array, int offset) => new MutableSpan<T>(array, offset);

        /// <summary>
        /// Save to a Vector4
        /// </summary>
        /// <param name="data">The data</param>
        /// <param name="v">The vector to save to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SaveTo(this MutableSpan<float> data, ref Vector4 v)
        {
            v.X = data[0];
            v.Y = data[1];
            v.Z = data[2];
            v.W = data[3];
        }

        /// <summary>
        /// Save to a Vector4
        /// </summary>
        /// <param name="data">The data</param>
        /// <param name="v">The vector to save to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SaveTo(this MutableSpan<int> data, ref Vector4 v)
        {
            v.X = data[0];
            v.Y = data[1];
            v.Z = data[2];
            v.W = data[3];
        }

        /// <summary>
        /// Load from Vector4
        /// </summary>
        /// <param name="data">The data</param>
        /// <param name="v">The vector to load from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadFrom(this MutableSpan<float> data, ref Vector4 v)
        {
            data[0] = v.X;
            data[1] = v.Y;
            data[2] = v.Z;
            data[3] = v.W;
        }

        /// <summary>
        /// Load from Vector4
        /// </summary>
        /// <param name="data">The data</param>
        /// <param name="v">The vector to load from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadFrom(this MutableSpan<int> data, ref Vector4 v)
        {
            data[0] = (int)v.X;
            data[1] = (int)v.Y;
            data[2] = (int)v.Z;
            data[3] = (int)v.W;
        }
    }
}

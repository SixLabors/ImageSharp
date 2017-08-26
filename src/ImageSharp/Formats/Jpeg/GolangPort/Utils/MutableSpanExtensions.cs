// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Utils
{
    using System;

    /// <summary>
    /// Span Extensions
    /// </summary>
    internal static class SpanExtensions
    {
        /// <summary>
        /// Save to a Vector4
        /// </summary>
        /// <param name="data">The data</param>
        /// <param name="v">The vector to save to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SaveTo(this Span<float> data, ref Vector4 v)
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
        public static void SaveTo(this Span<int> data, ref Vector4 v)
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
        public static void LoadFrom(this Span<float> data, ref Vector4 v)
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
        public static void LoadFrom(this Span<int> data, ref Vector4 v)
        {
            data[0] = (int)v.X;
            data[1] = (int)v.Y;
            data[2] = (int)v.Z;
            data[3] = (int)v.W;
        }

        /// <summary>
        /// Converts all int values of src to float
        /// </summary>
        /// <param name="src">Source</param>
        /// <returns>A new <see cref="Span{T}"/> with float values</returns>
        public static float[] ConvertAllToFloat(this int[] src)
        {
            float[] result = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                result[i] = (float)src[i];
            }

            return result;
        }

        /// <summary>
        /// Converts all float values of src to int
        /// </summary>
        /// <param name="src">Source</param>
        /// <returns>A new <see cref="Span{T}"/> with float values</returns>
        public static Span<int> ConvertToInt32Span(this Span<float> src)
        {
            int[] result = new int[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                result[i] = (int)src[i];
            }

            return result;
        }

        /// <summary>
        /// Add a scalar to all values of src
        /// </summary>
        /// <param name="src">The source</param>
        /// <param name="scalar">The scalar value to add</param>
        /// <returns>A new instance of <see cref="Span{T}"/></returns>
        public static Span<float> AddScalarToAllValues(this Span<float> src, float scalar)
        {
            float[] result = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                result[i] = src[i] + scalar;
            }

            return result;
        }

        /// <summary>
        /// Add a scalar to all values of src
        /// </summary>
        /// <param name="src">The source</param>
        /// <param name="scalar">The scalar value to add</param>
        /// <returns>A new instance of <see cref="Span{T}"/></returns>
        public static Span<int> AddScalarToAllValues(this Span<int> src, int scalar)
        {
            int[] result = new int[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                result[i] = src[i] + scalar;
            }

            return result;
        }

        /// <summary>
        /// Copy all values in src to a new <see cref="Span{T}"/> instance
        /// </summary>
        /// <typeparam name="T">Element type</typeparam>
        /// <param name="src">The source</param>
        /// <returns>A new instance of <see cref="Span{T}"/></returns>
        public static Span<T> Copy<T>(this Span<T> src)
        {
            T[] result = new T[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                result[i] = src[i];
            }

            return result;
        }
    }
}

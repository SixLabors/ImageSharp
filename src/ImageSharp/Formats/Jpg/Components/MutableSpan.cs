// <copyright file="MutableSpan.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Like corefxlab Span, but with an AddOffset() method for efficiency.
    /// TODO: When Span will be official, consider replacing this class!
    /// </summary>
    /// <see>
    ///     <cref>https://github.com/dotnet/corefxlab/blob/master/src/System.Slices/System/Span.cs</cref>
    /// </see>
    /// <typeparam name="T"></typeparam>
    internal struct MutableSpan<T>
    {
        public T[] Data;

        public int Offset;

        public int TotalCount => this.Data.Length - this.Offset;

        public MutableSpan(int size, int offset = 0)
        {
            this.Data = new T[size];
            this.Offset = offset;
        }

        public MutableSpan(T[] data, int offset = 0)
        {
            this.Data = data;
            this.Offset = offset;
        }

        public T this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.Data[idx + this.Offset];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.Data[idx + this.Offset] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MutableSpan<T> Slice(int offset)
        {
            return new MutableSpan<T>(this.Data, this.Offset + offset);
        }

        public static implicit operator MutableSpan<T>(T[] data) => new MutableSpan<T>(data, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset(int offset)
        {
            this.Offset += offset;
        }
    }

    internal static class MutableSpanExtensions
    {
        public static MutableSpan<T> Slice<T>(this T[] array, int offset) => new MutableSpan<T>(array, offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SaveTo(this MutableSpan<float> data, ref Vector4 v)
        {
            v.X = data[0];
            v.Y = data[1];
            v.Z = data[2];
            v.W = data[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SaveTo(this MutableSpan<int> data, ref Vector4 v)
        {
            v.X = data[0];
            v.Y = data[1];
            v.Z = data[2];
            v.W = data[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadFrom(this MutableSpan<float> data, ref Vector4 v)
        {
            data[0] = v.X;
            data[1] = v.Y;
            data[2] = v.Z;
            data[3] = v.W;
        }

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
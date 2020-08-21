// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public struct MemoryGroupIndex : IEquatable<MemoryGroupIndex>
    {
        public override bool Equals(object obj) => obj is MemoryGroupIndex other && this.Equals(other);

        public override int GetHashCode() => HashCode.Combine(this.BufferLength, this.BufferIndex, this.ElementIndex);

        public int BufferLength { get; }

        public int BufferIndex { get; }

        public int ElementIndex { get; }

        public MemoryGroupIndex(int bufferLength, int bufferIndex, int elementIndex)
        {
            this.BufferLength = bufferLength;
            this.BufferIndex = bufferIndex;
            this.ElementIndex = elementIndex;
        }

        public static MemoryGroupIndex operator +(MemoryGroupIndex idx, int val)
        {
            int nextElementIndex = idx.ElementIndex + val;
            return new MemoryGroupIndex(
                idx.BufferLength,
                idx.BufferIndex + (nextElementIndex / idx.BufferLength),
                nextElementIndex % idx.BufferLength);
        }

        public bool Equals(MemoryGroupIndex other)
        {
            if (this.BufferLength != other.BufferLength)
            {
                throw new InvalidOperationException();
            }

            return this.BufferIndex == other.BufferIndex && this.ElementIndex == other.ElementIndex;
        }

        public static bool operator ==(MemoryGroupIndex a, MemoryGroupIndex b) => a.Equals(b);

        public static bool operator !=(MemoryGroupIndex a, MemoryGroupIndex b) => !a.Equals(b);

        public static bool operator <(MemoryGroupIndex a, MemoryGroupIndex b)
        {
            if (a.BufferLength != b.BufferLength)
            {
                throw new InvalidOperationException();
            }

            if (a.BufferIndex < b.BufferIndex)
            {
                return true;
            }

            if (a.BufferIndex == b.BufferIndex)
            {
                return a.ElementIndex < b.ElementIndex;
            }

            return false;
        }

        public static bool operator >(MemoryGroupIndex a, MemoryGroupIndex b)
        {
            if (a.BufferLength != b.BufferLength)
            {
                throw new InvalidOperationException();
            }

            if (a.BufferIndex > b.BufferIndex)
            {
                return true;
            }

            if (a.BufferIndex == b.BufferIndex)
            {
                return a.ElementIndex > b.ElementIndex;
            }

            return false;
        }
    }

    internal static class MemoryGroupIndexExtensions
    {
        public static T GetElementAt<T>(this IMemoryGroup<T> group, MemoryGroupIndex idx)
            where T : struct
        {
            return group[idx.BufferIndex].Span[idx.ElementIndex];
        }

        public static void SetElementAt<T>(this IMemoryGroup<T> group, MemoryGroupIndex idx, T value)
            where T : struct
        {
            group[idx.BufferIndex].Span[idx.ElementIndex] = value;
        }

        public static MemoryGroupIndex MinIndex<T>(this IMemoryGroup<T> group)
            where T : struct
        {
            return new MemoryGroupIndex(group.BufferLength, 0, 0);
        }

        public static MemoryGroupIndex MaxIndex<T>(this IMemoryGroup<T> group)
            where T : struct
        {
            return group.Count == 0
                ? new MemoryGroupIndex(group.BufferLength, 0, 0)
                : new MemoryGroupIndex(group.BufferLength, group.Count - 1, group[group.Count - 1].Length);
        }
    }
}

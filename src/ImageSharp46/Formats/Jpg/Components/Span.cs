using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ImageSharp.Formats
{
    public struct Span<T>
    {
        public T[] Data;
        public int Offset;

        public int TotalCount => Data.Length - Offset;

        public Span(int size, int offset = 0)
        {
            Data = new T[size];
            Offset = offset;
        }

        public Span(T[] data, int offset = 0)
        {
            Data = data;
            Offset = offset;
        }

        public T this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return Data[idx + Offset]; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)] set { Data[idx + Offset] = value; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> Slice(int offset)
        {
            return new Span<T>(Data, Offset + offset);
        }

        public static implicit operator Span<T>(T[] data) => new Span<T>(data, 0);

        private static readonly ArrayPool<T> Pool = ArrayPool<T>.Create(128, 10);

        public static Span<T> RentFromPool(int size, int offset = 0)
        {
            return new Span<T>(Pool.Rent(size), offset);
        }

        public void ReturnToPool()
        {
            Pool.Return(Data, true);
            Data = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset(int offset)
        {
            Offset += offset;
        }
    }

    public static class SpanExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SaveTo(this Span<float> data, ref Vector4 v)
        {
            v.X = data[0];
            v.Y = data[1];
            v.Z = data[2];
            v.W = data[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SaveTo(this Span<int> data, ref Vector4 v)
        {
            v.X = data[0];
            v.Y = data[1];
            v.Z = data[2];
            v.W = data[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadFrom(this Span<float> data, ref Vector4 v)
        {
            data[0] = v.X;
            data[1] = v.Y;
            data[2] = v.Z;
            data[3] = v.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadFrom(this Span<int> data, ref Vector4 v)
        {
            data[0] = (int)v.X;
            data[1] = (int)v.Y;
            data[2] = (int)v.Z;
            data[3] = (int)v.W;
        }


    }
}
// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SixLabors;

internal static class Extensions
{
    public const MethodImplOptions AggressiveOptimization =
#if !NET6_0_OR_GREATER
        (MethodImplOptions)512;
#else
        MethodImplOptions.AggressiveOptimization;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PixelTypeInfo GetPixelTypeInfo<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
    {
#if !NET6_0_OR_GREATER
        return default(TPixel).GetPixelTypeInfo();
#else
        return TPixel.GetPixelTypeInfo();
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void* UnsafeAdd<T>(void* source, int elementOffset)
    {
        return Unsafe.Add<T>(source, elementOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T UnsafeAdd<T>(ref T source, int elementOffset)
    {
        return ref Unsafe.Add(ref source, elementOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T UnsafeAdd<T>(ref T source, nint elementOffset)
    {
        return ref Unsafe.Add(ref source, elementOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T UnsafeAdd<T>(ref T source, nuint elementOffset)
    {
#if !NET6_0_OR_GREATER
        return ref Unsafe.Add(ref source, (nint)elementOffset);
#else
        return ref Unsafe.Add(ref source, elementOffset);
#endif
    }

#if !NET6_0_OR_GREATER
    private static class DummyValueCache<T>
    {
        public static T Value;
    }
#endif

    public static ref T GetUnsafeArrayDataReference<T>(T[] array)
    {
#if !NET6_0_OR_GREATER
        if (array.Length == 0)
        {
            return ref DummyValueCache<T>.Value;
        }
        return ref array[0];
#else
        return ref MemoryMarshal.GetArrayDataReference(array);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BitsToSingle(ReadOnlySpan<byte> bytes)
    {
#if !NET6_0_OR_GREATER
        return MemoryMarshal.Cast<byte, float>(bytes)[0];
#else
        return BitConverter.ToSingle(bytes);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnsafeSkipInit<T>(out T variable)
    {
#if !NET6_0_OR_GREATER
        variable = default;
#else
        Unsafe.SkipInit(out variable);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEnumDefined<TEnum>(TEnum value) where TEnum : struct, Enum
    {
#if !NET6_0_OR_GREATER
        return Enum.IsDefined(typeof(TEnum), value);
#else
        return Enum.IsDefined(value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum[] GetEnumValues<TEnum>() where TEnum : struct, Enum
    {
#if !NET6_0_OR_GREATER
        return (TEnum[])Enum.GetValues(typeof(TEnum));
#else
        return Enum.GetValues<TEnum>();
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Min(nuint x, nuint y)
    {
#if !NET6_0_OR_GREATER
        return x < y ? x : y;
#else
        return Math.Min(x, y);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max)
    {
#if !NET6_0_OR_GREATER
        if (min > max)
        {
            throw new ArgumentException($"'{min}' cannot be greater than {max}.");
        }

        if (value < min)
        {
            return min;
        }
        else if (value > max)
        {
            return max;
        }

        return value;
#else
        return Math.Clamp(value, min, max);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Clamp(uint value, uint min, uint max)
    {
#if !NET6_0_OR_GREATER
        if (min > max)
        {
            throw new ArgumentException($"'{min}' cannot be greater than {max}.");
        }

        if (value < min)
        {
            return min;
        }
        else if (value > max)
        {
            return max;
        }

        return value;
#else
        return Math.Clamp(value, min, max);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max)
    {
#if !NET6_0_OR_GREATER
        if (min > max)
        {
            throw new ArgumentException($"'{min}' cannot be greater than {max}.");
        }

        if (value < min)
        {
            return min;
        }
        else if (value > max)
        {
            return max;
        }

        return value;
#else
        return Math.Clamp(value, min, max);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear<T>(this T[] array)
    {
#if !NET6_0_OR_GREATER
        Array.Clear(array, 0, array.Length);
#else
        Array.Clear(array);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear<T>(this T[,] array)
    {
#if !NET6_0_OR_GREATER
        Array.Clear(array, 0, array.Length);
#else
        Array.Clear(array);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear<T>(this T[,,] array)
    {
#if !NET6_0_OR_GREATER
        Array.Clear(array, 0, array.Length);
#else
        Array.Clear(array);
#endif
    }

#if !NET6_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder AppendLine(this StringBuilder stringBuilder, IFormatProvider? provider, FormattableString str)
    {
        return stringBuilder.AppendLine(str.ToString(provider));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this Stream stream, byte[] array)
    {
        stream.Write(array, 0, array.Length);
    }

    public static void Write(this Stream stream, ReadOnlySpan<byte> span)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(buffer.AsSpan());
            stream.Write(buffer, 0, span.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static int Read(this Stream stream, Span<byte> span)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(span.Length);
        try
        {
            int count = stream.Read(buffer, 0, span.Length);
            buffer.AsSpan(0, count).CopyTo(span);
            return count;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<TTo> As<TFrom, TTo>(this Vector<TFrom> vector)
        where TTo : struct
        where TFrom : struct
    {
        return Unsafe.As<Vector<TFrom>, Vector<TTo>>(ref vector);
    }

    public static void Sort<T>(this Span<T> span)
    {
        var buffer = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(buffer.AsSpan());
            Array.Sort(buffer, 0, span.Length);
            buffer.AsSpan(0, span.Length).CopyTo(span);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buffer);
        }
    }

    public static void Sort<T>(this Span<T> span, IComparer<T>? comparer)
    {
        var buffer = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(buffer.AsSpan());
            Array.Sort(buffer, 0, span.Length, comparer);
            buffer.AsSpan(0, span.Length).CopyTo(span);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buffer);
        }
    }

    public static void Sort<T>(this Span<T> span, Comparison<T> comparison)
    {
        var buffer = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(buffer.AsSpan());
            Array.Sort(buffer, 0, span.Length, new ComparisonComparer<T>(comparison));
            buffer.AsSpan(0, span.Length).CopyTo(span);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buffer);
        }
    }

    private class ComparisonComparer<T> : IComparer<T>
    {
        readonly Comparison<T> comparison;

        public ComparisonComparer(Comparison<T> comparison)
        {
            this.comparison = comparison;
        }

        public int Compare(T a, T b)
        {
            return comparison(a, b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe string GetString(this Encoding encoding, Span<byte> bytes)
    {
        fixed (byte* bytesPtr = bytes)
        {
            return encoding.GetString(bytesPtr, bytes.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        fixed (byte* bytesPtr = bytes)
        {
            return encoding.GetString(bytesPtr, bytes.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int GetBytes(this Encoding encoding, Span<char> chars, Span<byte> bytes)
    {
        fixed (char* charsPtr = chars)
        fixed (byte* bytesPtr = bytes)
        {
            return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        fixed (char* charsPtr = chars)
        fixed (byte* bytesPtr = bytes)
        {
            return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int GetBytes(this Encoding encoding, string chars, Span<byte> bytes)
    {
        fixed (char* charsPtr = chars)
        fixed (byte* bytesPtr = bytes)
        {
            return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWith(this string str, char c)
    {
        return str.Length > 0 && str[0] == c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EndsWith(this string str, char c)
    {
        var length = str.Length;
        return length > 0 && str[length - 1] == c;
    }
#endif
}

#if !NET6_0_OR_GREATER
internal static class MathF
{
    public const float PI = (float)Math.PI;

    public static float Pow(float x, float y)
    {
        return (float)Math.Pow(x, y);
    }

    public static float Exp(float f)
    {
        return (float)Math.Exp(f);
    }

    public static float Sin(float x)
    {
        return (float)Math.Sin(x);
    }

    public static float Cos(float x)
    {
        return (float)Math.Cos(x);
    }

    public static float Atan2(float y, float x)
    {
        return (float)Math.Atan2(y, x);
    }

    public static float Abs(float x)
    {
        return (float)Math.Abs(x);
    }

    public static float Sqrt(float x)
    {
        return (float)Math.Sqrt(x);
    }

    public static float Round(float f)
    {
        return (float)Math.Round(f);
    }

    public static float Round(float f, int digits)
    {
        return (float)Math.Round(f, digits);
    }

    public static float Round(float f, int digits, MidpointRounding mode)
    {
        return (float)Math.Round(f, digits, mode);
    }

    public static float Round(float f, MidpointRounding mode)
    {
        return (float)Math.Round(f, mode);
    }

    public static float Floor(float f)
    {
        return (float)Math.Floor(f);
    }

    public static float Ceiling(float f)
    {
        return (float)Math.Ceiling(f);
    }

    public static float Min(float a, float b)
    {
        return (float)Math.Min(a, b);
    }

    public static float Max(float a, float b)
    {
        return (float)Math.Max(a, b);
    }
}
#endif

#if !NET6_0_OR_GREATER
internal class ArgumentNullException : System.ArgumentNullException
{
    public ArgumentNullException(string? paramName) : base(paramName)
    {

    }

    public ArgumentNullException(string? paramName, string? message) : base(paramName, message)
    {

    }

    public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            throw new System.ArgumentNullException(paramName);
        }
    }
}
#endif

#if !NET8_0_OR_GREATER
internal class ArgumentOutOfRangeException : System.ArgumentOutOfRangeException
{
    public ArgumentOutOfRangeException(string? paramName) : base(paramName)
    {

    }

    public ArgumentOutOfRangeException(string? paramName, string? message) : base(paramName, message)
    {

    }

    public ArgumentOutOfRangeException(string? paramName, object? actualValue, string? message) : base(paramName, actualValue, message)
    {

    }

    public static void ThrowIfNegativeOrZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= 0)
        {
            throw new System.ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be a non-negative and non-zero value.");
        }
    }
}
#endif

#if !NET7_0_OR_GREATER
internal class ObjectDisposedException : System.ObjectDisposedException
{
    public ObjectDisposedException(string? objectName) : base(objectName)
    {

    }

    public ObjectDisposedException(string? objectName, string? message) : base(objectName, message)
    {

    }

    public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
    {
        if (condition)
        {
            throw new System.ObjectDisposedException(instance?.GetType().FullName);
        }
    }
}
#endif

#if !NET6_0_OR_GREATER
internal static class BitOperations
{
    private static readonly byte[] trailingZeroCountDeBruijn = // 32
    {
        00, 01, 28, 02, 29, 14, 24, 03,
        30, 22, 20, 15, 25, 17, 04, 08,
        31, 27, 13, 23, 21, 19, 16, 07,
        26, 12, 18, 06, 11, 05, 10, 09
    };
    
    private static readonly byte[] log2DeBruijn = // 32
    {
        00, 09, 01, 10, 13, 21, 02, 29,
        11, 14, 16, 18, 22, 25, 03, 30,
        08, 12, 20, 28, 15, 17, 24, 07,
        19, 27, 23, 06, 26, 05, 04, 31
    };

    private static ReadOnlySpan<byte> Log2DeBruijn => log2DeBruijn;

    private static ReadOnlySpan<byte> TrailingZeroCountDeBruijn => TrailingZeroCountDeBruijn;

    [CLSCompliant(false)]
    public static int Log2(uint value)
    {
        // No AggressiveInlining due to large method size
        // Has conventional contract 0->0 (Log(0) is undefined)

        // Fill trailing zeros with ones, eg 00010010 becomes 00011111
        value |= value >> 01;
        value |= value >> 02;
        value |= value >> 04;
        value |= value >> 08;
        value |= value >> 16;

        // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
        return Unsafe.AddByteOffset(
            // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_1100_0100_1010_1100_1101_1101u
            ref MemoryMarshal.GetReference(Log2DeBruijn),
            // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
            (IntPtr)(int)((value * 0x07C4ACDDu) >> 27));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static int LeadingZeroCount(uint value)
    {
        // Unguarded fallback contract is 0->31, BSR contract is 0->undefined
        if (value == 0)
        {
            return 32;
        }

        return 31 ^ Log2(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static int LeadingZeroCount(ulong value)
    {
        uint hi = (uint)(value >> 32);

        if (hi == 0)
        {
            return 32 + LeadingZeroCount((uint)value);
        }

        return LeadingZeroCount(hi);
    }
    
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(uint value)
    {
        // Unguarded fallback contract is 0->0, BSF contract is 0->undefined
        if (value == 0)
        {
            return 32;
        }
        
        // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
        return Unsafe.AddByteOffset(
            // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_0111_1100_1011_0101_0011_0001u
            ref MemoryMarshal.GetReference(TrailingZeroCountDeBruijn),
            // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
            (IntPtr)(int)(((value & (uint)-(int)value) * 0x077CB531u) >> 27)); // Multi-cast mitigates redundant conv.u8
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(int value)
        => TrailingZeroCount((uint)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static int TrailingZeroCount(ulong value)
    {
        uint lo = (uint)value;
 
        if (lo == 0)
        {
            return 32 + TrailingZeroCount((uint)(value >> 32));
        }
 
        return TrailingZeroCount(lo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static uint RotateRight(uint value, int offset)
        => (value >> offset) | (value << (32 - offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static uint RotateLeft(uint value, int offset)
        => (value << offset) | (value >> (32 - offset));
}
#endif

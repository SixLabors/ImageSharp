// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Defines the lane arithmetic of the self-guided traversals. One lane is one output column, so every stage kernel
/// is written once over a lane type and each register width supplies only its own arithmetic.
/// </content>
internal static partial class Av1SelfGuidedFilter
{
    /// <summary>
    /// Supplies the 32-bit lane arithmetic of one register width, or of one column for the scalar tail.
    /// </summary>
    /// <remarks>
    /// Multiplication and addition wrap in 32 bits, as the reference's unsigned products do. The logical shift and the
    /// unsigned minimum read the lanes as unsigned values.
    /// </remarks>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    private interface ILaneOperator<TLanes>
        where TLanes : unmanaged
    {
        /// <summary>
        /// Gets the number of columns the lane type carries.
        /// </summary>
        public static abstract int Count { get; }

        /// <summary>
        /// Replicates a value into every lane.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The lanes.</returns>
        public static abstract TLanes Create(int value);

        /// <summary>
        /// Loads consecutive 32-bit values.
        /// </summary>
        /// <param name="source">The first value of the buffer.</param>
        /// <param name="offset">The index of the first value to load.</param>
        /// <returns>The lanes.</returns>
        public static abstract TLanes Load(ref int source, nuint offset);

        /// <summary>
        /// Stores consecutive 32-bit values.
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <param name="destination">The first value of the buffer.</param>
        /// <param name="offset">The index of the first value to store.</param>
        public static abstract void Store(TLanes value, ref int destination, nuint offset);

        /// <summary>
        /// Loads consecutive samples at their stored precision, one per lane.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <param name="source">The first sample of the plane.</param>
        /// <param name="offset">The index of the first sample to load.</param>
        /// <returns>The sample values.</returns>
        public static abstract TLanes LoadSamples<TSample>(ref TSample source, nuint offset)
            where TSample : unmanaged;

        /// <summary>
        /// Stores already clipped values as consecutive samples at their stored precision.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <param name="value">The clipped sample values.</param>
        /// <param name="destination">The first sample of the plane.</param>
        /// <param name="offset">The index of the first sample to store.</param>
        public static abstract void StoreSamples<TSample>(TLanes value, ref TSample destination, nuint offset)
            where TSample : unmanaged;

        /// <summary>
        /// Adds lanes.
        /// </summary>
        /// <param name="left">The first addend.</param>
        /// <param name="right">The second addend.</param>
        /// <returns>The sums.</returns>
        public static abstract TLanes Add(TLanes left, TLanes right);

        /// <summary>
        /// Subtracts lanes.
        /// </summary>
        /// <param name="left">The minuend.</param>
        /// <param name="right">The subtrahend.</param>
        /// <returns>The differences.</returns>
        public static abstract TLanes Subtract(TLanes left, TLanes right);

        /// <summary>
        /// Multiplies lanes, keeping the low 32 bits of each product.
        /// </summary>
        /// <param name="left">The first factor.</param>
        /// <param name="right">The second factor.</param>
        /// <returns>The products.</returns>
        public static abstract TLanes Multiply(TLanes left, TLanes right);

        /// <summary>
        /// Shifts lanes left.
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <param name="count">The shift count.</param>
        /// <returns>The shifted lanes.</returns>
        public static abstract TLanes ShiftLeft(TLanes value, int count);

        /// <summary>
        /// Shifts lanes right with sign extension.
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <param name="count">The shift count.</param>
        /// <returns>The shifted lanes.</returns>
        public static abstract TLanes ShiftRightArithmetic(TLanes value, int count);

        /// <summary>
        /// Shifts lanes right with zero extension.
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <param name="count">The shift count.</param>
        /// <returns>The shifted lanes.</returns>
        public static abstract TLanes ShiftRightLogical(TLanes value, int count);

        /// <summary>
        /// Takes the signed maximum of lanes.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>The maxima.</returns>
        public static abstract TLanes Max(TLanes left, TLanes right);

        /// <summary>
        /// Takes the signed minimum of lanes.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>The minima.</returns>
        public static abstract TLanes Min(TLanes left, TLanes right);

        /// <summary>
        /// Takes the unsigned minimum of lanes.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>The minima.</returns>
        public static abstract TLanes MinUnsigned(TLanes left, TLanes right);

        /// <summary>
        /// Computes the inclusive prefix sum across the lanes.
        /// </summary>
        /// <param name="values">The independent values.</param>
        /// <returns>The inclusive prefix sum in each lane.</returns>
        public static abstract TLanes Scan(TLanes values);

        /// <summary>
        /// Gets the last lane.
        /// </summary>
        /// <param name="values">The lanes.</param>
        /// <returns>The value of the last lane.</returns>
        public static abstract int Last(TLanes values);

        /// <summary>
        /// Reads one table entry per lane.
        /// </summary>
        /// <param name="table">The first entry of the table.</param>
        /// <param name="indices">The entry index of each lane, each inside the table.</param>
        /// <returns>The entries.</returns>
        public static abstract TLanes Gather(ref int table, TLanes indices);
    }

    /// <summary>
    /// Filters one column at a time.
    /// </summary>
    private readonly struct ScalarLaneOperator : ILaneOperator<int>
    {
        /// <inheritdoc/>
        public static int Count => 1;

        /// <inheritdoc/>
        public static int Create(int value) => value;

        /// <inheritdoc/>
        public static int Load(ref int source, nuint offset) => Unsafe.Add(ref source, offset);

        /// <inheritdoc/>
        public static void Store(int value, ref int destination, nuint offset) => Unsafe.Add(ref destination, offset) = value;

        /// <inheritdoc/>
        public static int LoadSamples<TSample>(ref TSample source, nuint offset)
            where TSample : unmanaged
            => Av1RestorationSampleOperations.Load(Unsafe.Add(ref source, offset));

        /// <inheritdoc/>
        public static void StoreSamples<TSample>(int value, ref TSample destination, nuint offset)
            where TSample : unmanaged
            => Unsafe.Add(ref destination, offset) = Av1RestorationSampleOperations.FromInt32<TSample>(value);

        /// <inheritdoc/>
        public static int Add(int left, int right) => left + right;

        /// <inheritdoc/>
        public static int Subtract(int left, int right) => left - right;

        /// <inheritdoc/>
        public static int Multiply(int left, int right) => left * right;

        /// <inheritdoc/>
        public static int ShiftLeft(int value, int count) => value << count;

        /// <inheritdoc/>
        public static int ShiftRightArithmetic(int value, int count) => value >> count;

        /// <inheritdoc/>
        public static int ShiftRightLogical(int value, int count) => value >>> count;

        /// <inheritdoc/>
        public static int Max(int left, int right) => Math.Max(left, right);

        /// <inheritdoc/>
        public static int Min(int left, int right) => Math.Min(left, right);

        /// <inheritdoc/>
        public static int MinUnsigned(int left, int right) => (int)Math.Min((uint)left, (uint)right);

        /// <inheritdoc/>
        public static int Scan(int values) => values;

        /// <inheritdoc/>
        public static int Last(int values) => values;

        /// <inheritdoc/>
        public static int Gather(ref int table, int indices) => Unsafe.Add(ref table, indices);
    }

    /// <summary>
    /// Filters four columns at a time.
    /// </summary>
    private readonly struct Vector128LaneOperator : ILaneOperator<Vector128<int>>
    {
        /// <inheritdoc/>
        public static int Count => Vector128<int>.Count;

        /// <inheritdoc/>
        public static Vector128<int> Create(int value) => Vector128.Create(value);

        /// <inheritdoc/>
        public static Vector128<int> Load(ref int source, nuint offset) => Vector128.LoadUnsafe(ref source, offset);

        /// <inheritdoc/>
        public static void Store(Vector128<int> value, ref int destination, nuint offset) => value.StoreUnsafe(ref destination, offset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadSamples<TSample>(ref TSample source, nuint offset)
            where TSample : unmanaged
            => Av1RestorationSampleOperations.LoadToInt32(ref Unsafe.Add(ref source, offset), Vector128<int>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSamples<TSample>(Vector128<int> value, ref TSample destination, nuint offset)
            where TSample : unmanaged
            => Av1RestorationSampleOperations.Store(
                Vector128.Narrow(value.AsUInt32(), Vector128<uint>.Zero).GetLower(),
                ref Unsafe.Add(ref destination, offset));

        /// <inheritdoc/>
        public static Vector128<int> Add(Vector128<int> left, Vector128<int> right) => left + right;

        /// <inheritdoc/>
        public static Vector128<int> Subtract(Vector128<int> left, Vector128<int> right) => left - right;

        /// <inheritdoc/>
        public static Vector128<int> Multiply(Vector128<int> left, Vector128<int> right) => left * right;

        /// <inheritdoc/>
        public static Vector128<int> ShiftLeft(Vector128<int> value, int count) => value << count;

        /// <inheritdoc/>
        public static Vector128<int> ShiftRightArithmetic(Vector128<int> value, int count) => value >> count;

        /// <inheritdoc/>
        public static Vector128<int> ShiftRightLogical(Vector128<int> value, int count) => value >>> count;

        /// <inheritdoc/>
        public static Vector128<int> Max(Vector128<int> left, Vector128<int> right) => Vector128.Max(left, right);

        /// <inheritdoc/>
        public static Vector128<int> Min(Vector128<int> left, Vector128<int> right) => Vector128.Min(left, right);

        /// <inheritdoc/>
        public static Vector128<int> MinUnsigned(Vector128<int> left, Vector128<int> right)
            => Vector128.Min(left.AsUInt32(), right.AsUInt32()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Scan(Vector128<int> values)
        {
            // The portable shuffle is required here because its out-of-range indices produce zero.
            // ShuffleNative may mask those indices and wrap them back into the input on some ISAs.
            Vector128<int> scan = values + Vector128.Shuffle(values, Vector128.Create(4, 0, 1, 2));
            return scan + Vector128.Shuffle(scan, Vector128.Create(4, 4, 0, 1));
        }

        /// <inheritdoc/>
        public static int Last(Vector128<int> values) => values.GetElement(Vector128<int>.Count - 1);

        /// <inheritdoc/>
        public static Vector128<int> Gather(ref int table, Vector128<int> indices) => Vector128_.Gather(ref table, indices);
    }

    /// <summary>
    /// Filters eight columns at a time.
    /// </summary>
    private readonly struct Vector256LaneOperator : ILaneOperator<Vector256<int>>
    {
        /// <inheritdoc/>
        public static int Count => Vector256<int>.Count;

        /// <inheritdoc/>
        public static Vector256<int> Create(int value) => Vector256.Create(value);

        /// <inheritdoc/>
        public static Vector256<int> Load(ref int source, nuint offset) => Vector256.LoadUnsafe(ref source, offset);

        /// <inheritdoc/>
        public static void Store(Vector256<int> value, ref int destination, nuint offset) => value.StoreUnsafe(ref destination, offset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadSamples<TSample>(ref TSample source, nuint offset)
            where TSample : unmanaged
            => Av1RestorationSampleOperations.LoadToInt32(ref Unsafe.Add(ref source, offset), Vector256<int>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSamples<TSample>(Vector256<int> value, ref TSample destination, nuint offset)
            where TSample : unmanaged
        {
            // Narrowing with a zero upper vector places the eight ordered samples in the lower 128 bits, which store
            // directly without an ISA-specific lane permutation.
            Av1RestorationSampleOperations.Store(
                Vector256.Narrow(value.AsUInt32(), Vector256<uint>.Zero).GetLower(),
                ref Unsafe.Add(ref destination, offset));
        }

        /// <inheritdoc/>
        public static Vector256<int> Add(Vector256<int> left, Vector256<int> right) => left + right;

        /// <inheritdoc/>
        public static Vector256<int> Subtract(Vector256<int> left, Vector256<int> right) => left - right;

        /// <inheritdoc/>
        public static Vector256<int> Multiply(Vector256<int> left, Vector256<int> right) => left * right;

        /// <inheritdoc/>
        public static Vector256<int> ShiftLeft(Vector256<int> value, int count) => value << count;

        /// <inheritdoc/>
        public static Vector256<int> ShiftRightArithmetic(Vector256<int> value, int count) => value >> count;

        /// <inheritdoc/>
        public static Vector256<int> ShiftRightLogical(Vector256<int> value, int count) => value >>> count;

        /// <inheritdoc/>
        public static Vector256<int> Max(Vector256<int> left, Vector256<int> right) => Vector256.Max(left, right);

        /// <inheritdoc/>
        public static Vector256<int> Min(Vector256<int> left, Vector256<int> right) => Vector256.Min(left, right);

        /// <inheritdoc/>
        public static Vector256<int> MinUnsigned(Vector256<int> left, Vector256<int> right)
            => Vector256.Min(left.AsUInt32(), right.AsUInt32()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Scan(Vector256<int> values)
        {
            // Byte shifts inside each 128-bit half give the shortest dependency chain. After scanning each half, the
            // lower half's total joins the upper half, so the result is one continuous eight-lane prefix.
            Vector256<int> scan = values + Vector256_.ShiftLeftBytesInLane(values.AsByte(), sizeof(int)).AsInt32();
            scan += Vector256_.ShiftLeftBytesInLane(scan.AsByte(), sizeof(int) * 2).AsInt32();
            return scan + Vector256.Create(Vector128<int>.Zero, Vector128.Create(scan.GetElement(3)));
        }

        /// <inheritdoc/>
        public static int Last(Vector256<int> values) => values.GetElement(Vector256<int>.Count - 1);

        /// <inheritdoc/>
        public static Vector256<int> Gather(ref int table, Vector256<int> indices) => Vector256_.Gather(ref table, indices);
    }

    /// <summary>
    /// Filters sixteen columns at a time.
    /// </summary>
    private readonly struct Vector512LaneOperator : ILaneOperator<Vector512<int>>
    {
        /// <inheritdoc/>
        public static int Count => Vector512<int>.Count;

        /// <inheritdoc/>
        public static Vector512<int> Create(int value) => Vector512.Create(value);

        /// <inheritdoc/>
        public static Vector512<int> Load(ref int source, nuint offset) => Vector512.LoadUnsafe(ref source, offset);

        /// <inheritdoc/>
        public static void Store(Vector512<int> value, ref int destination, nuint offset) => value.StoreUnsafe(ref destination, offset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> LoadSamples<TSample>(ref TSample source, nuint offset)
            where TSample : unmanaged
            => Av1RestorationSampleOperations.LoadToInt32(ref Unsafe.Add(ref source, offset), Vector512<int>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSamples<TSample>(Vector512<int> value, ref TSample destination, nuint offset)
            where TSample : unmanaged
            => Av1RestorationSampleOperations.Store(
                Vector512.Narrow(value.AsUInt32(), Vector512<uint>.Zero).GetLower(),
                ref Unsafe.Add(ref destination, offset));

        /// <inheritdoc/>
        public static Vector512<int> Add(Vector512<int> left, Vector512<int> right) => left + right;

        /// <inheritdoc/>
        public static Vector512<int> Subtract(Vector512<int> left, Vector512<int> right) => left - right;

        /// <inheritdoc/>
        public static Vector512<int> Multiply(Vector512<int> left, Vector512<int> right) => left * right;

        /// <inheritdoc/>
        public static Vector512<int> ShiftLeft(Vector512<int> value, int count) => value << count;

        /// <inheritdoc/>
        public static Vector512<int> ShiftRightArithmetic(Vector512<int> value, int count) => value >> count;

        /// <inheritdoc/>
        public static Vector512<int> ShiftRightLogical(Vector512<int> value, int count) => value >>> count;

        /// <inheritdoc/>
        public static Vector512<int> Max(Vector512<int> left, Vector512<int> right) => Vector512.Max(left, right);

        /// <inheritdoc/>
        public static Vector512<int> Min(Vector512<int> left, Vector512<int> right) => Vector512.Min(left, right);

        /// <inheritdoc/>
        public static Vector512<int> MinUnsigned(Vector512<int> left, Vector512<int> right)
            => Vector512.Min(left.AsUInt32(), right.AsUInt32()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Scan(Vector512<int> values)
        {
            // Byte shifts inside each 128-bit lane scan the four lanes independently. The exclusive prefix of the lane
            // totals then joins each lane: the last element of every lane, moved one lane up, is scanned across the
            // lanes by shifts of one and two lanes. The portable shuffle turns index 16 into zero.
            Vector512<int> scan = values + Vector512_.ShiftLeftBytesInLane(values.AsByte(), sizeof(int)).AsInt32();
            scan += Vector512_.ShiftLeftBytesInLane(scan.AsByte(), sizeof(int) * 2).AsInt32();
            Vector512<int> totals = Vector512.Shuffle(scan, Vector512.Create(16, 16, 16, 16, 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11));
            totals += Vector512.Shuffle(totals, Vector512.Create(16, 16, 16, 16, 0, 0, 0, 0, 4, 4, 4, 4, 8, 8, 8, 8));
            totals += Vector512.Shuffle(totals, Vector512.Create(16, 16, 16, 16, 16, 16, 16, 16, 0, 0, 0, 0, 4, 4, 4, 4));
            return scan + totals;
        }

        /// <inheritdoc/>
        public static int Last(Vector512<int> values) => values.GetElement(Vector512<int>.Count - 1);

        /// <inheritdoc/>
        public static Vector512<int> Gather(ref int table, Vector512<int> indices) => Vector512_.Gather(ref table, indices);
    }
}

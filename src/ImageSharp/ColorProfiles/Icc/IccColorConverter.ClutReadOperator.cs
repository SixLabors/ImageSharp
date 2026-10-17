// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Reads complete CLUT nodes while their owner holds the storage pin.
    /// </summary>
    private interface IClutReadOperator
    {
        /// <summary>
        /// Gets the sample divisor: the integer maximum code, or one for float samples.
        /// </summary>
        static abstract float Divisor { get; }

        /// <summary>
        /// Reads one node as unnormalized component codes.
        /// </summary>
        /// <param name="index">The node index within the validated grid.</param>
        /// <returns>The node's components, with unused components set to zero.</returns>
        Vector4 Read(int index);

        /// <summary>
        /// Reads 4 nodes as unnormalized component planes.
        /// </summary>
        /// <param name="indices">The node indices within the validated grid.</param>
        /// <param name="x">The first component codes, one node per lane.</param>
        /// <param name="y">The second component codes.</param>
        /// <param name="z">The third component codes.</param>
        /// <param name="w">The fourth component codes.</param>
        void Read(Vector128<int> indices, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z, out Vector128<float> w);

        /// <summary>
        /// Reads 8 nodes as unnormalized component planes.
        /// </summary>
        /// <param name="indices">The node indices within the validated grid.</param>
        /// <param name="x">The first component codes, one node per lane.</param>
        /// <param name="y">The second component codes.</param>
        /// <param name="z">The third component codes.</param>
        /// <param name="w">The fourth component codes.</param>
        void Read(Vector256<int> indices, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z, out Vector256<float> w);

        /// <summary>
        /// Reads 16 nodes as unnormalized component planes.
        /// </summary>
        /// <param name="indices">The node indices within the validated grid.</param>
        /// <param name="x">The first component codes, one node per lane.</param>
        /// <param name="y">The second component codes.</param>
        /// <param name="z">The third component codes.</param>
        /// <param name="w">The fourth component codes.</param>
        void Read(Vector512<int> indices, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z, out Vector512<float> w);
    }

    /// <summary>
    /// Reads packed 8-bit nodes from a pinned, padded CLUT.
    /// </summary>
    private readonly unsafe struct ByteClutReadOperator : IClutReadOperator
    {
        private const int SampleBits = 8;
        private readonly byte* data;
        private readonly int count;
        private readonly int stride;
        private readonly int firstShift;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteClutReadOperator"/> struct.
        /// </summary>
        /// <param name="data">The pinned storage, including leading read padding.</param>
        /// <param name="count">The validated number of components, from one to four.</param>
        /// <param name="padding">The bytes before the first sample.</param>
        public ByteClutReadOperator(byte* data, int count, int padding)
        {
            this.data = data;
            this.count = count;
            this.stride = count * sizeof(byte);
            this.firstShift = BitConverter.IsLittleEndian ? padding * 8 : 32 - ((padding + sizeof(byte)) * 8);
        }

        /// <inheritdoc/>
        public static float Divisor => byte.MaxValue;

        /// <inheritdoc/>
        public Vector4 Read(int index)
        {
            // The leading padding lets a complete word end at this node's last component, including the final node.
            uint packed = Unsafe.ReadUnaligned<uint>(this.data + ((uint)index * (uint)this.stride));
            int direction = BitConverter.IsLittleEndian ? SampleBits : -SampleBits;

            return new Vector4(
                (packed >> this.firstShift) & byte.MaxValue,
                this.count > 1 ? (packed >> (this.firstShift + direction)) & byte.MaxValue : 0,
                this.count > 2 ? (packed >> (this.firstShift + (2 * direction))) & byte.MaxValue : 0,
                this.count > 3 ? (packed >> (this.firstShift + (3 * direction))) & byte.MaxValue : 0);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector128<int> indices, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z, out Vector128<float> w)
        {
            Vector128<int> offsets = indices * Vector128.Create(this.stride);

            // One four-byte gather supplies every component of each node. Padding supplies the unused leading bytes.
            Vector128<uint> packed = Vector128_.Gather(this.data, offsets).AsUInt32();
            Vector128<uint> mask = Vector128.Create((uint)byte.MaxValue);
            int direction = BitConverter.IsLittleEndian ? SampleBits : -SampleBits;
            x = Vector128.ConvertToSingle(((packed >> this.firstShift) & mask).AsInt32());
            y = this.count > 1 ? Vector128.ConvertToSingle(((packed >> (this.firstShift + direction)) & mask).AsInt32()) : default;
            z = this.count > 2 ? Vector128.ConvertToSingle(((packed >> (this.firstShift + (2 * direction))) & mask).AsInt32()) : default;
            w = this.count > 3 ? Vector128.ConvertToSingle(((packed >> (this.firstShift + (3 * direction))) & mask).AsInt32()) : default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector256<int> indices, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z, out Vector256<float> w)
        {
            Vector256<int> offsets = indices * Vector256.Create(this.stride);

            // One four-byte gather supplies every component of each node. Padding supplies the unused leading bytes.
            Vector256<uint> packed = Vector256_.Gather(this.data, offsets).AsUInt32();
            Vector256<uint> mask = Vector256.Create((uint)byte.MaxValue);
            int direction = BitConverter.IsLittleEndian ? SampleBits : -SampleBits;
            x = Vector256.ConvertToSingle(((packed >> this.firstShift) & mask).AsInt32());
            y = this.count > 1 ? Vector256.ConvertToSingle(((packed >> (this.firstShift + direction)) & mask).AsInt32()) : default;
            z = this.count > 2 ? Vector256.ConvertToSingle(((packed >> (this.firstShift + (2 * direction))) & mask).AsInt32()) : default;
            w = this.count > 3 ? Vector256.ConvertToSingle(((packed >> (this.firstShift + (3 * direction))) & mask).AsInt32()) : default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector512<int> indices, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z, out Vector512<float> w)
        {
            Vector512<int> offsets = indices * Vector512.Create(this.stride);

            // One four-byte gather supplies every component of each node. Padding supplies the unused leading bytes.
            Vector512<uint> packed = Vector512_.Gather(this.data, offsets).AsUInt32();
            Vector512<uint> mask = Vector512.Create((uint)byte.MaxValue);
            int direction = BitConverter.IsLittleEndian ? SampleBits : -SampleBits;
            x = Vector512.ConvertToSingle(((packed >> this.firstShift) & mask).AsInt32());
            y = this.count > 1 ? Vector512.ConvertToSingle(((packed >> (this.firstShift + direction)) & mask).AsInt32()) : default;
            z = this.count > 2 ? Vector512.ConvertToSingle(((packed >> (this.firstShift + (2 * direction))) & mask).AsInt32()) : default;
            w = this.count > 3 ? Vector512.ConvertToSingle(((packed >> (this.firstShift + (3 * direction))) & mask).AsInt32()) : default;
        }
    }

    /// <summary>
    /// Reads packed 16-bit nodes from a pinned, padded CLUT.
    /// </summary>
    private readonly unsafe struct UInt16ClutReadOperator : IClutReadOperator
    {
        private const int SampleBits = 16;
        private readonly byte* data;
        private readonly int count;
        private readonly int stride;
        private readonly int firstShift;
        private readonly int padding;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt16ClutReadOperator"/> struct.
        /// </summary>
        /// <param name="data">The pinned storage, including leading read padding.</param>
        /// <param name="count">The validated number of components, from one to four.</param>
        /// <param name="padding">The bytes before the first sample.</param>
        public UInt16ClutReadOperator(byte* data, int count, int padding)
        {
            this.data = data;
            this.count = count;
            this.stride = count * sizeof(ushort);
            this.firstShift = BitConverter.IsLittleEndian ? padding * 8 : 64 - ((padding + sizeof(ushort)) * 8);
            this.padding = padding;
        }

        /// <inheritdoc/>
        public static float Divisor => ushort.MaxValue;

        /// <inheritdoc/>
        public Vector4 Read(int index)
        {
            // The leading padding lets a complete word end at this node's last component, including the final node.
            ulong packed = Unsafe.ReadUnaligned<ulong>(this.data + ((uint)index * (uint)this.stride));
            int direction = BitConverter.IsLittleEndian ? SampleBits : -SampleBits;

            return new Vector4(
                (packed >> this.firstShift) & ushort.MaxValue,
                this.count > 1 ? (packed >> (this.firstShift + direction)) & ushort.MaxValue : 0,
                this.count > 2 ? (packed >> (this.firstShift + (2 * direction))) & ushort.MaxValue : 0,
                this.count > 3 ? (packed >> (this.firstShift + (3 * direction))) & ushort.MaxValue : 0);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector128<int> indices, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z, out Vector128<float> w)
        {
            Vector128<int> offsets = indices * Vector128.Create(this.stride);
            Vector128<uint> mask = Vector128.Create((uint)ushort.MaxValue);

            // Each gather reads two adjacent 16-bit components into one 32-bit lane. For three-channel nodes,
            // the second word overlaps the first at the middle component and ends at the node's last byte.
            // One-channel nodes use the preceding padding; no gather reads beyond the owned storage.
            int firstOffset = this.count == 1 ? sizeof(uint) : this.padding;
            Vector128<uint> first = Vector128_.Gather(this.data + firstOffset, offsets).AsUInt32();
            int firstShift = this.count == 1 ? (BitConverter.IsLittleEndian ? 16 : 0) : (BitConverter.IsLittleEndian ? 0 : 16);
            x = Vector128.ConvertToSingle(((first >> firstShift) & mask).AsInt32());
            y = this.count > 1
                ? Vector128.ConvertToSingle(((first >> (BitConverter.IsLittleEndian ? 16 : 0)) & mask).AsInt32())
                : default;

            z = default;
            w = default;
            if (this.count > 2)
            {
                Vector128<uint> second = Vector128_.Gather(this.data + sizeof(uint), offsets).AsUInt32();
                int thirdShift = this.count == 3 ? (BitConverter.IsLittleEndian ? 16 : 0) : (BitConverter.IsLittleEndian ? 0 : 16);
                z = Vector128.ConvertToSingle(((second >> thirdShift) & mask).AsInt32());
                w = this.count == 4
                    ? Vector128.ConvertToSingle(((second >> (BitConverter.IsLittleEndian ? 16 : 0)) & mask).AsInt32())
                    : default;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector256<int> indices, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z, out Vector256<float> w)
        {
            Vector256<int> offsets = indices * Vector256.Create(this.stride);
            Vector256<uint> mask = Vector256.Create((uint)ushort.MaxValue);

            // Each gather reads two adjacent 16-bit components into one 32-bit lane. For three-channel nodes,
            // the second word overlaps the first at the middle component and ends at the node's last byte.
            // One-channel nodes use the preceding padding; no gather reads beyond the owned storage.
            int firstOffset = this.count == 1 ? sizeof(uint) : this.padding;
            Vector256<uint> first = Vector256_.Gather(this.data + firstOffset, offsets).AsUInt32();
            int firstShift = this.count == 1 ? (BitConverter.IsLittleEndian ? 16 : 0) : (BitConverter.IsLittleEndian ? 0 : 16);
            x = Vector256.ConvertToSingle(((first >> firstShift) & mask).AsInt32());
            y = this.count > 1
                ? Vector256.ConvertToSingle(((first >> (BitConverter.IsLittleEndian ? 16 : 0)) & mask).AsInt32())
                : default;

            z = default;
            w = default;
            if (this.count > 2)
            {
                Vector256<uint> second = Vector256_.Gather(this.data + sizeof(uint), offsets).AsUInt32();
                int thirdShift = this.count == 3 ? (BitConverter.IsLittleEndian ? 16 : 0) : (BitConverter.IsLittleEndian ? 0 : 16);
                z = Vector256.ConvertToSingle(((second >> thirdShift) & mask).AsInt32());
                w = this.count == 4
                    ? Vector256.ConvertToSingle(((second >> (BitConverter.IsLittleEndian ? 16 : 0)) & mask).AsInt32())
                    : default;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector512<int> indices, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z, out Vector512<float> w)
        {
            Vector512<int> offsets = indices * Vector512.Create(this.stride);
            Vector512<uint> mask = Vector512.Create((uint)ushort.MaxValue);

            // Each gather reads two adjacent 16-bit components into one 32-bit lane. For three-channel nodes,
            // the second word overlaps the first at the middle component and ends at the node's last byte.
            // One-channel nodes use the preceding padding; no gather reads beyond the owned storage.
            int firstOffset = this.count == 1 ? sizeof(uint) : this.padding;
            Vector512<uint> first = Vector512_.Gather(this.data + firstOffset, offsets).AsUInt32();
            int firstShift = this.count == 1 ? (BitConverter.IsLittleEndian ? 16 : 0) : (BitConverter.IsLittleEndian ? 0 : 16);
            x = Vector512.ConvertToSingle(((first >> firstShift) & mask).AsInt32());
            y = this.count > 1
                ? Vector512.ConvertToSingle(((first >> (BitConverter.IsLittleEndian ? 16 : 0)) & mask).AsInt32())
                : default;

            z = default;
            w = default;
            if (this.count > 2)
            {
                Vector512<uint> second = Vector512_.Gather(this.data + sizeof(uint), offsets).AsUInt32();
                int thirdShift = this.count == 3 ? (BitConverter.IsLittleEndian ? 16 : 0) : (BitConverter.IsLittleEndian ? 0 : 16);
                z = Vector512.ConvertToSingle(((second >> thirdShift) & mask).AsInt32());
                w = this.count == 4
                    ? Vector512.ConvertToSingle(((second >> (BitConverter.IsLittleEndian ? 16 : 0)) & mask).AsInt32())
                    : default;
            }
        }
    }

    /// <summary>
    /// Reads float samples through the same node contract as the packed integer readers.
    /// </summary>
    private readonly unsafe struct FloatClutReadOperator : IClutReadOperator
    {
        private readonly float* data;
        private readonly int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="FloatClutReadOperator"/> struct over the pinned float array.
        /// </summary>
        /// <param name="data">The first sample in the pinned table.</param>
        /// <param name="count">The number of output components in each node.</param>
        public FloatClutReadOperator(float* data, int count)
        {
            this.data = data;
            this.count = count;
        }

        /// <inheritdoc/>
        public static float Divisor => 1F;

        /// <inheritdoc/>
        public Vector4 Read(int index)
        {
            float* node = this.data + ((uint)index * (uint)this.count);
            return new Vector4(node[0], this.count > 1 ? node[1] : 0F, this.count > 2 ? node[2] : 0F, this.count > 3 ? node[3] : 0F);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector128<int> indices, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z, out Vector128<float> w)
        {
            // Each lane selects one node. Node indices become sample indices here, so interpolation
            // remains independent of the channel count and storage type. Each gather reads exactly one float per lane.
            Vector128<int> offsets = indices * Vector128.Create(this.count);
            x = Vector128_.Gather((int*)this.data, offsets).AsSingle();
            y = this.count > 1 ? Vector128_.Gather((int*)this.data, offsets + Vector128<int>.One).AsSingle() : default;
            z = this.count > 2 ? Vector128_.Gather((int*)this.data, offsets + Vector128.Create(2)).AsSingle() : default;
            w = this.count > 3 ? Vector128_.Gather((int*)this.data, offsets + Vector128.Create(3)).AsSingle() : default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector256<int> indices, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z, out Vector256<float> w)
        {
            // Each lane selects one node. Node indices become sample indices here, so interpolation
            // remains independent of the channel count and storage type. Each gather reads exactly one float per lane.
            Vector256<int> offsets = indices * Vector256.Create(this.count);
            x = Vector256_.Gather((int*)this.data, offsets).AsSingle();
            y = this.count > 1 ? Vector256_.Gather((int*)this.data, offsets + Vector256<int>.One).AsSingle() : default;
            z = this.count > 2 ? Vector256_.Gather((int*)this.data, offsets + Vector256.Create(2)).AsSingle() : default;
            w = this.count > 3 ? Vector256_.Gather((int*)this.data, offsets + Vector256.Create(3)).AsSingle() : default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Vector512<int> indices, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z, out Vector512<float> w)
        {
            // Each lane selects one node. Node indices become sample indices here, so interpolation
            // remains independent of the channel count and storage type. Each gather reads exactly one float per lane.
            Vector512<int> offsets = indices * Vector512.Create(this.count);
            x = Vector512_.Gather((int*)this.data, offsets).AsSingle();
            y = this.count > 1 ? Vector512_.Gather((int*)this.data, offsets + Vector512<int>.One).AsSingle() : default;
            z = this.count > 2 ? Vector512_.Gather((int*)this.data, offsets + Vector512.Create(2)).AsSingle() : default;
            w = this.count > 3 ? Vector512_.Gather((int*)this.data, offsets + Vector512.Create(3)).AsSingle() : default;
        }
    }
}

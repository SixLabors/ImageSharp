// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Interpolates one- to four-channel ICC tables across independent color lanes.
    /// </summary>
    internal readonly struct ClutOperator
    {
        private readonly float[] table;
        private readonly InlineArray4<int> strides;
        private readonly Vector4 maximum;
        private readonly int inputCount;
        private readonly int outputCount;
        private readonly bool tetrahedral;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClutOperator"/> struct.
        /// </summary>
        /// <param name="clut">The table, with dimensions and storage validated by <see cref="IccClut"/>.</param>
        /// <param name="useTrilinearInterpolation">Whether to blend all cell corners instead of tetrahedral slices.</param>
        public ClutOperator(IccClut clut, bool useTrilinearInterpolation)
        {
            Guard.MustBeBetweenOrEqualTo(clut.InputChannelCount, 1, 4, nameof(clut.InputChannelCount));
            Guard.MustBeBetweenOrEqualTo(clut.OutputChannelCount, 1, 4, nameof(clut.OutputChannelCount));
            this.table = clut.Values;
            this.inputCount = clut.InputChannelCount;
            this.outputCount = clut.OutputChannelCount;
            this.tetrahedral = !useTrilinearInterpolation && this.inputCount >= 3;
            InlineArray4<int> strides = default;
            Vector4 maximum = default;
            int stride = this.outputCount;
            for (int axis = this.inputCount - 1; axis >= 0; axis--)
            {
                int points = clut.GridPointCount[axis];
                maximum[axis] = points - 1;

                // A single-node axis is constant. Both corners read that node with a zero stride.
                strides[axis] = points == 1 ? 0 : stride;
                stride *= points;
            }

            this.maximum = maximum;
            this.strides = strides;
        }

        /// <summary>
        /// Interpolates one color with the same cell and vertex rules as the SIMD paths.
        /// </summary>
        /// <param name="value">The normalized device or PCS components.</param>
        /// <returns>The output components; unused components are zero.</returns>
        public Vector4 Convert(Vector4 value)
        {
            Vector4 position = Numerics.Clamp(value, Vector4.Zero, Vector4.One) * this.maximum;
            Vector4 fraction = default;
            int offset = 0;
            for (int axis = 0; axis < this.inputCount; axis++)
            {
                // Use the preceding cell at the upper edge. A one-node axis always uses node zero.
                int index = Math.Min((int)position[axis], Math.Max((int)this.maximum[axis] - 1, 0));
                fraction[axis] = position[axis] - index;
                offset += index * this.strides[axis];
            }

            Vector4 result = default;
            if (this.tetrahedral)
            {
                int axis = this.inputCount - 3;
                float first = fraction[axis];
                float second = fraction[axis + 1];
                float third = fraction[axis + 2];
                int a = this.strides[axis];
                int b = this.strides[axis + 1];
                int c = this.strides[axis + 2];

                // Sort fractions and their strides together. Strict comparisons retain X/Y/Z order on ties.
                // The two largest fractions select the two intermediate tetrahedron vertices.
                if (first < second)
                {
                    (first, second) = (second, first);
                    (a, b) = (b, a);
                }

                if (second < third)
                {
                    (second, third) = (third, second);
                    (b, c) = (c, b);
                }

                if (first < second)
                {
                    (first, second) = (second, first);
                    (a, b) = (b, a);
                }

                int upper = a + b + c;
                for (int channel = 0; channel < this.outputCount; channel++)
                {
                    int index = offset + channel;
                    float lower = this.table[index];
                    float vertex1 = this.table[index + a];
                    float vertex2 = this.table[index + a + b];
                    float top = this.table[index + upper];
                    float converted = lower + ((vertex1 - lower) * first) + ((vertex2 - vertex1) * second) + ((top - vertex2) * third);
                    if (this.inputCount == 4)
                    {
                        // The first axis blends two tetrahedral slices with the same three fractions.
                        index += this.strides[0];
                        lower = this.table[index];
                        vertex1 = this.table[index + a];
                        vertex2 = this.table[index + a + b];
                        top = this.table[index + upper];
                        float next = lower + ((vertex1 - lower) * first) + ((vertex2 - vertex1) * second) + ((top - vertex2) * third);
                        converted += (next - converted) * fraction.X;
                    }

                    result[channel] = converted;
                }

                return result;
            }

            // The low corner bit selects the first axis. Multiply weights from the last axis to the first,
            // then accumulate corners in encoded order, retaining the existing interpolation order.
            for (int corner = 0; corner < 1 << this.inputCount; corner++)
            {
                float weight = 1F;
                int index = offset;
                for (int axis = this.inputCount - 1; axis >= 0; axis--)
                {
                    bool upper = (corner & (1 << axis)) != 0;
                    weight *= upper ? fraction[axis] : 1F - fraction[axis];
                    index += upper ? this.strides[axis] : 0;
                }

                for (int channel = 0; channel < this.outputCount; channel++)
                {
                    result[channel] += this.table[index + channel] * weight;
                }
            }

            return result;
        }

        /// <summary>
        /// Interpolates 4 colors, with one component per register and one color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            GetCell(x, this.maximum.X, this.strides[0], out Vector128<int> ix, out Vector128<float> fx);
            GetCell(y, this.maximum.Y, this.strides[1], out Vector128<int> iy, out Vector128<float> fy);
            GetCell(z, this.maximum.Z, this.strides[2], out Vector128<int> iz, out Vector128<float> fz);
            GetCell(w, this.maximum.W, this.strides[3], out Vector128<int> iw, out Vector128<float> fw);
            Vector128<int> offset = ix + iy + iz + iw;
            ref int table = ref Unsafe.As<float, int>(ref MemoryMarshal.GetArrayDataReference(this.table));
            x = Vector128<float>.Zero;
            y = Vector128<float>.Zero;
            z = Vector128<float>.Zero;
            w = Vector128<float>.Zero;

            if (this.tetrahedral)
            {
                int axis = this.inputCount - 3;
                Vector128<float> first = axis == 0 ? fx : fy;
                Vector128<float> second = axis == 0 ? fy : fz;
                Vector128<float> third = axis == 0 ? fz : fw;
                Vector128<int> a = Vector128.Create(this.strides[axis]);
                Vector128<int> b = Vector128.Create(this.strides[axis + 1]);
                Vector128<int> c = Vector128.Create(this.strides[axis + 2]);

                // Sort fractions and strides in each lane. Three masked swaps select the tetrahedron
                // for every color without per-color branches. Equal fractions retain their original axis order.
                Vector128<float> swap = Vector128.LessThan(first, second);
                Vector128<float> saved = first;
                first = Vector128.ConditionalSelect(swap, second, first);
                second = Vector128.ConditionalSelect(swap, saved, second);
                Vector128<int> savedStride = a;
                a = Vector128.ConditionalSelect(swap.AsInt32(), b, a);
                b = Vector128.ConditionalSelect(swap.AsInt32(), savedStride, b);
                swap = Vector128.LessThan(second, third);
                saved = second;
                second = Vector128.ConditionalSelect(swap, third, second);
                third = Vector128.ConditionalSelect(swap, saved, third);
                savedStride = b;
                b = Vector128.ConditionalSelect(swap.AsInt32(), c, b);
                c = Vector128.ConditionalSelect(swap.AsInt32(), savedStride, c);
                swap = Vector128.LessThan(first, second);
                saved = first;
                first = Vector128.ConditionalSelect(swap, second, first);
                second = Vector128.ConditionalSelect(swap, saved, second);
                savedStride = a;
                a = Vector128.ConditionalSelect(swap.AsInt32(), b, a);
                b = Vector128.ConditionalSelect(swap.AsInt32(), savedStride, b);
                Vector128<int> upper = a + b + c;
                b += a;

                for (int channel = 0; channel < this.outputCount; channel++)
                {
                    Vector128<int> index = offset + Vector128.Create(channel);
                    Vector128<float> converted = InterpolateTetrahedron(ref table, index, a, b, upper, first, second, third);
                    if (this.inputCount == 4)
                    {
                        Vector128<float> next = InterpolateTetrahedron(ref table, index + Vector128.Create(this.strides[0]), a, b, upper, first, second, third);
                        converted += (next - converted) * fx;
                    }

                    // Select the output component. Every calculation above processes a complete SIMD block.
                    switch (channel)
                    {
                        case 0:
                            x = converted;
                            break;
                        case 1:
                            y = converted;
                            break;
                        case 2:
                            z = converted;
                            break;
                        case 3:
                            w = converted;
                            break;
                    }
                }

                return;
            }

            // Multilinear interpolation uses every cell corner. Retain the scalar corner order and
            // multiply weights from the last axis to the first. Inactive axes have zero stride and fraction.
            for (int corner = 0; corner < 1 << this.inputCount; corner++)
            {
                Vector128<float> weight = this.inputCount == 4 ? ((corner & 8) == 0 ? Vector128<float>.One - fw : fw) : Vector128<float>.One;
                if (this.inputCount >= 3)
                {
                    weight *= (corner & 4) == 0 ? Vector128<float>.One - fz : fz;
                }

                if (this.inputCount >= 2)
                {
                    weight *= (corner & 2) == 0 ? Vector128<float>.One - fy : fy;
                }

                weight *= (corner & 1) == 0 ? Vector128<float>.One - fx : fx;
                int stride = ((corner & 1) == 0 ? 0 : this.strides[0]) + ((corner & 2) == 0 ? 0 : this.strides[1])
                    + ((corner & 4) == 0 ? 0 : this.strides[2]) + ((corner & 8) == 0 ? 0 : this.strides[3]);
                Vector128<int> index = offset + Vector128.Create(stride);
                x += Vector128_.Gather(ref table, index).AsSingle() * weight;
                if (this.outputCount > 1)
                {
                    y += Vector128_.Gather(ref table, index + Vector128<int>.One).AsSingle() * weight;
                }

                if (this.outputCount > 2)
                {
                    z += Vector128_.Gather(ref table, index + Vector128.Create(2)).AsSingle() * weight;
                }

                if (this.outputCount > 3)
                {
                    w += Vector128_.Gather(ref table, index + Vector128.Create(3)).AsSingle() * weight;
                }
            }
        }

        /// <summary>
        /// Finds the lower cell offset and fraction for each lane, including endpoints and constant axes.
        /// </summary>
        /// <param name="value">The normalized input component.</param>
        /// <param name="maximum">The last grid node on this axis.</param>
        /// <param name="stride">The distance between nodes, or zero for a constant axis.</param>
        /// <param name="offset">The lower node's table offset.</param>
        /// <param name="fraction">The distance from the lower node in grid cells.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetCell(Vector128<float> value, float maximum, int stride, out Vector128<int> offset, out Vector128<float> fraction)
        {
            // Ordered comparisons map NaN to zero, as Numerics.Clamp does. Bound indices before unchecked gathers.
            value = Vector128.ConditionalSelect(Vector128.GreaterThan(value, Vector128<float>.One), Vector128<float>.One, value);
            value = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(value, Vector128<float>.Zero), value, Vector128<float>.Zero);
            Vector128<float> position = value * Vector128.Create(maximum);
            Vector128<int> index = Vector128.Min(Vector128.ConvertToInt32(position), Vector128.Create(Math.Max((int)maximum - 1, 0)));
            fraction = position - Vector128.ConvertToSingle(index);
            offset = index * Vector128.Create(stride);
        }

        /// <summary>
        /// Interpolates an output channel along each lane's selected tetrahedron edges.
        /// </summary>
        /// <param name="table">The table words, reinterpreted as floats after each gather.</param>
        /// <param name="index">The lower cell corner.</param>
        /// <param name="a">The first vertex offset.</param>
        /// <param name="b">The second vertex offset.</param>
        /// <param name="upper">The upper corner offset.</param>
        /// <param name="first">The largest axis fractions.</param>
        /// <param name="second">The middle axis fractions.</param>
        /// <param name="third">The smallest axis fractions.</param>
        /// <returns>The interpolated channel values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> InterpolateTetrahedron(
            ref int table,
            Vector128<int> index,
            Vector128<int> a,
            Vector128<int> b,
            Vector128<int> upper,
            Vector128<float> first,
            Vector128<float> second,
            Vector128<float> third)
        {
            Vector128<float> lower = Vector128_.Gather(ref table, index).AsSingle();
            Vector128<float> vertex1 = Vector128_.Gather(ref table, index + a).AsSingle();
            Vector128<float> vertex2 = Vector128_.Gather(ref table, index + b).AsSingle();
            Vector128<float> top = Vector128_.Gather(ref table, index + upper).AsSingle();
            return lower + ((vertex1 - lower) * first) + ((vertex2 - vertex1) * second) + ((top - vertex2) * third);
        }

        /// <summary>
        /// Interpolates 8 colors, with one component per register and one color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            GetCell(x, this.maximum.X, this.strides[0], out Vector256<int> ix, out Vector256<float> fx);
            GetCell(y, this.maximum.Y, this.strides[1], out Vector256<int> iy, out Vector256<float> fy);
            GetCell(z, this.maximum.Z, this.strides[2], out Vector256<int> iz, out Vector256<float> fz);
            GetCell(w, this.maximum.W, this.strides[3], out Vector256<int> iw, out Vector256<float> fw);
            Vector256<int> offset = ix + iy + iz + iw;
            ref int table = ref Unsafe.As<float, int>(ref MemoryMarshal.GetArrayDataReference(this.table));
            x = Vector256<float>.Zero;
            y = Vector256<float>.Zero;
            z = Vector256<float>.Zero;
            w = Vector256<float>.Zero;

            if (this.tetrahedral)
            {
                int axis = this.inputCount - 3;
                Vector256<float> first = axis == 0 ? fx : fy;
                Vector256<float> second = axis == 0 ? fy : fz;
                Vector256<float> third = axis == 0 ? fz : fw;
                Vector256<int> a = Vector256.Create(this.strides[axis]);
                Vector256<int> b = Vector256.Create(this.strides[axis + 1]);
                Vector256<int> c = Vector256.Create(this.strides[axis + 2]);

                // Sort fractions and strides in each lane. Three masked swaps select the tetrahedron
                // for every color without per-color branches. Equal fractions retain their original axis order.
                Vector256<float> swap = Vector256.LessThan(first, second);
                Vector256<float> saved = first;
                first = Vector256.ConditionalSelect(swap, second, first);
                second = Vector256.ConditionalSelect(swap, saved, second);
                Vector256<int> savedStride = a;
                a = Vector256.ConditionalSelect(swap.AsInt32(), b, a);
                b = Vector256.ConditionalSelect(swap.AsInt32(), savedStride, b);
                swap = Vector256.LessThan(second, third);
                saved = second;
                second = Vector256.ConditionalSelect(swap, third, second);
                third = Vector256.ConditionalSelect(swap, saved, third);
                savedStride = b;
                b = Vector256.ConditionalSelect(swap.AsInt32(), c, b);
                c = Vector256.ConditionalSelect(swap.AsInt32(), savedStride, c);
                swap = Vector256.LessThan(first, second);
                saved = first;
                first = Vector256.ConditionalSelect(swap, second, first);
                second = Vector256.ConditionalSelect(swap, saved, second);
                savedStride = a;
                a = Vector256.ConditionalSelect(swap.AsInt32(), b, a);
                b = Vector256.ConditionalSelect(swap.AsInt32(), savedStride, b);
                Vector256<int> upper = a + b + c;
                b += a;

                for (int channel = 0; channel < this.outputCount; channel++)
                {
                    Vector256<int> index = offset + Vector256.Create(channel);
                    Vector256<float> converted = InterpolateTetrahedron(ref table, index, a, b, upper, first, second, third);
                    if (this.inputCount == 4)
                    {
                        Vector256<float> next = InterpolateTetrahedron(ref table, index + Vector256.Create(this.strides[0]), a, b, upper, first, second, third);
                        converted += (next - converted) * fx;
                    }

                    // Select the output component. Every calculation above processes a complete SIMD block.
                    switch (channel)
                    {
                        case 0:
                            x = converted;
                            break;
                        case 1:
                            y = converted;
                            break;
                        case 2:
                            z = converted;
                            break;
                        case 3:
                            w = converted;
                            break;
                    }
                }

                return;
            }

            // Multilinear interpolation uses every cell corner. Retain the scalar corner order and
            // multiply weights from the last axis to the first. Inactive axes have zero stride and fraction.
            for (int corner = 0; corner < 1 << this.inputCount; corner++)
            {
                Vector256<float> weight = this.inputCount == 4 ? ((corner & 8) == 0 ? Vector256<float>.One - fw : fw) : Vector256<float>.One;
                if (this.inputCount >= 3)
                {
                    weight *= (corner & 4) == 0 ? Vector256<float>.One - fz : fz;
                }

                if (this.inputCount >= 2)
                {
                    weight *= (corner & 2) == 0 ? Vector256<float>.One - fy : fy;
                }

                weight *= (corner & 1) == 0 ? Vector256<float>.One - fx : fx;
                int stride = ((corner & 1) == 0 ? 0 : this.strides[0]) + ((corner & 2) == 0 ? 0 : this.strides[1])
                    + ((corner & 4) == 0 ? 0 : this.strides[2]) + ((corner & 8) == 0 ? 0 : this.strides[3]);
                Vector256<int> index = offset + Vector256.Create(stride);
                x += Vector256_.Gather(ref table, index).AsSingle() * weight;
                if (this.outputCount > 1)
                {
                    y += Vector256_.Gather(ref table, index + Vector256<int>.One).AsSingle() * weight;
                }

                if (this.outputCount > 2)
                {
                    z += Vector256_.Gather(ref table, index + Vector256.Create(2)).AsSingle() * weight;
                }

                if (this.outputCount > 3)
                {
                    w += Vector256_.Gather(ref table, index + Vector256.Create(3)).AsSingle() * weight;
                }
            }
        }

        /// <summary>
        /// Finds the lower cell offset and fraction for each lane, including endpoints and constant axes.
        /// </summary>
        /// <param name="value">The normalized input component.</param>
        /// <param name="maximum">The last grid node on this axis.</param>
        /// <param name="stride">The distance between nodes, or zero for a constant axis.</param>
        /// <param name="offset">The lower node's table offset.</param>
        /// <param name="fraction">The distance from the lower node in grid cells.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetCell(Vector256<float> value, float maximum, int stride, out Vector256<int> offset, out Vector256<float> fraction)
        {
            // Ordered comparisons map NaN to zero, as Numerics.Clamp does. Bound indices before unchecked gathers.
            value = Vector256.ConditionalSelect(Vector256.GreaterThan(value, Vector256<float>.One), Vector256<float>.One, value);
            value = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(value, Vector256<float>.Zero), value, Vector256<float>.Zero);
            Vector256<float> position = value * Vector256.Create(maximum);
            Vector256<int> index = Vector256.Min(Vector256.ConvertToInt32(position), Vector256.Create(Math.Max((int)maximum - 1, 0)));
            fraction = position - Vector256.ConvertToSingle(index);
            offset = index * Vector256.Create(stride);
        }

        /// <summary>
        /// Interpolates an output channel along each lane's selected tetrahedron edges.
        /// </summary>
        /// <param name="table">The table words, reinterpreted as floats after each gather.</param>
        /// <param name="index">The lower cell corner.</param>
        /// <param name="a">The first vertex offset.</param>
        /// <param name="b">The second vertex offset.</param>
        /// <param name="upper">The upper corner offset.</param>
        /// <param name="first">The largest axis fractions.</param>
        /// <param name="second">The middle axis fractions.</param>
        /// <param name="third">The smallest axis fractions.</param>
        /// <returns>The interpolated channel values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> InterpolateTetrahedron(
            ref int table,
            Vector256<int> index,
            Vector256<int> a,
            Vector256<int> b,
            Vector256<int> upper,
            Vector256<float> first,
            Vector256<float> second,
            Vector256<float> third)
        {
            Vector256<float> lower = Vector256_.Gather(ref table, index).AsSingle();
            Vector256<float> vertex1 = Vector256_.Gather(ref table, index + a).AsSingle();
            Vector256<float> vertex2 = Vector256_.Gather(ref table, index + b).AsSingle();
            Vector256<float> top = Vector256_.Gather(ref table, index + upper).AsSingle();
            return lower + ((vertex1 - lower) * first) + ((vertex2 - vertex1) * second) + ((top - vertex2) * third);
        }

        /// <summary>
        /// Interpolates 16 colors, with one component per register and one color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            GetCell(x, this.maximum.X, this.strides[0], out Vector512<int> ix, out Vector512<float> fx);
            GetCell(y, this.maximum.Y, this.strides[1], out Vector512<int> iy, out Vector512<float> fy);
            GetCell(z, this.maximum.Z, this.strides[2], out Vector512<int> iz, out Vector512<float> fz);
            GetCell(w, this.maximum.W, this.strides[3], out Vector512<int> iw, out Vector512<float> fw);
            Vector512<int> offset = ix + iy + iz + iw;
            ref int table = ref Unsafe.As<float, int>(ref MemoryMarshal.GetArrayDataReference(this.table));
            x = Vector512<float>.Zero;
            y = Vector512<float>.Zero;
            z = Vector512<float>.Zero;
            w = Vector512<float>.Zero;

            if (this.tetrahedral)
            {
                int axis = this.inputCount - 3;
                Vector512<float> first = axis == 0 ? fx : fy;
                Vector512<float> second = axis == 0 ? fy : fz;
                Vector512<float> third = axis == 0 ? fz : fw;
                Vector512<int> a = Vector512.Create(this.strides[axis]);
                Vector512<int> b = Vector512.Create(this.strides[axis + 1]);
                Vector512<int> c = Vector512.Create(this.strides[axis + 2]);

                // Sort fractions and strides in each lane. Three masked swaps select the tetrahedron
                // for every color without per-color branches. Equal fractions retain their original axis order.
                Vector512<float> swap = Vector512.LessThan(first, second);
                Vector512<float> saved = first;
                first = Vector512.ConditionalSelect(swap, second, first);
                second = Vector512.ConditionalSelect(swap, saved, second);
                Vector512<int> savedStride = a;
                a = Vector512.ConditionalSelect(swap.AsInt32(), b, a);
                b = Vector512.ConditionalSelect(swap.AsInt32(), savedStride, b);
                swap = Vector512.LessThan(second, third);
                saved = second;
                second = Vector512.ConditionalSelect(swap, third, second);
                third = Vector512.ConditionalSelect(swap, saved, third);
                savedStride = b;
                b = Vector512.ConditionalSelect(swap.AsInt32(), c, b);
                c = Vector512.ConditionalSelect(swap.AsInt32(), savedStride, c);
                swap = Vector512.LessThan(first, second);
                saved = first;
                first = Vector512.ConditionalSelect(swap, second, first);
                second = Vector512.ConditionalSelect(swap, saved, second);
                savedStride = a;
                a = Vector512.ConditionalSelect(swap.AsInt32(), b, a);
                b = Vector512.ConditionalSelect(swap.AsInt32(), savedStride, b);
                Vector512<int> upper = a + b + c;
                b += a;

                for (int channel = 0; channel < this.outputCount; channel++)
                {
                    Vector512<int> index = offset + Vector512.Create(channel);
                    Vector512<float> converted = InterpolateTetrahedron(ref table, index, a, b, upper, first, second, third);
                    if (this.inputCount == 4)
                    {
                        Vector512<float> next = InterpolateTetrahedron(ref table, index + Vector512.Create(this.strides[0]), a, b, upper, first, second, third);
                        converted += (next - converted) * fx;
                    }

                    // Select the output component. Every calculation above processes a complete SIMD block.
                    switch (channel)
                    {
                        case 0:
                            x = converted;
                            break;
                        case 1:
                            y = converted;
                            break;
                        case 2:
                            z = converted;
                            break;
                        case 3:
                            w = converted;
                            break;
                    }
                }

                return;
            }

            // Multilinear interpolation uses every cell corner. Retain the scalar corner order and
            // multiply weights from the last axis to the first. Inactive axes have zero stride and fraction.
            for (int corner = 0; corner < 1 << this.inputCount; corner++)
            {
                Vector512<float> weight = this.inputCount == 4 ? ((corner & 8) == 0 ? Vector512<float>.One - fw : fw) : Vector512<float>.One;
                if (this.inputCount >= 3)
                {
                    weight *= (corner & 4) == 0 ? Vector512<float>.One - fz : fz;
                }

                if (this.inputCount >= 2)
                {
                    weight *= (corner & 2) == 0 ? Vector512<float>.One - fy : fy;
                }

                weight *= (corner & 1) == 0 ? Vector512<float>.One - fx : fx;
                int stride = ((corner & 1) == 0 ? 0 : this.strides[0]) + ((corner & 2) == 0 ? 0 : this.strides[1])
                    + ((corner & 4) == 0 ? 0 : this.strides[2]) + ((corner & 8) == 0 ? 0 : this.strides[3]);
                Vector512<int> index = offset + Vector512.Create(stride);
                x += Vector512_.Gather(ref table, index).AsSingle() * weight;
                if (this.outputCount > 1)
                {
                    y += Vector512_.Gather(ref table, index + Vector512<int>.One).AsSingle() * weight;
                }

                if (this.outputCount > 2)
                {
                    z += Vector512_.Gather(ref table, index + Vector512.Create(2)).AsSingle() * weight;
                }

                if (this.outputCount > 3)
                {
                    w += Vector512_.Gather(ref table, index + Vector512.Create(3)).AsSingle() * weight;
                }
            }
        }

        /// <summary>
        /// Finds the lower cell offset and fraction for each lane, including endpoints and constant axes.
        /// </summary>
        /// <param name="value">The normalized input component.</param>
        /// <param name="maximum">The last grid node on this axis.</param>
        /// <param name="stride">The distance between nodes, or zero for a constant axis.</param>
        /// <param name="offset">The lower node's table offset.</param>
        /// <param name="fraction">The distance from the lower node in grid cells.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetCell(Vector512<float> value, float maximum, int stride, out Vector512<int> offset, out Vector512<float> fraction)
        {
            // Ordered comparisons map NaN to zero, as Numerics.Clamp does. Bound indices before unchecked gathers.
            value = Vector512.ConditionalSelect(Vector512.GreaterThan(value, Vector512<float>.One), Vector512<float>.One, value);
            value = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(value, Vector512<float>.Zero), value, Vector512<float>.Zero);
            Vector512<float> position = value * Vector512.Create(maximum);
            Vector512<int> index = Vector512.Min(Vector512.ConvertToInt32(position), Vector512.Create(Math.Max((int)maximum - 1, 0)));
            fraction = position - Vector512.ConvertToSingle(index);
            offset = index * Vector512.Create(stride);
        }

        /// <summary>
        /// Interpolates an output channel along each lane's selected tetrahedron edges.
        /// </summary>
        /// <param name="table">The table words, reinterpreted as floats after each gather.</param>
        /// <param name="index">The lower cell corner.</param>
        /// <param name="a">The first vertex offset.</param>
        /// <param name="b">The second vertex offset.</param>
        /// <param name="upper">The upper corner offset.</param>
        /// <param name="first">The largest axis fractions.</param>
        /// <param name="second">The middle axis fractions.</param>
        /// <param name="third">The smallest axis fractions.</param>
        /// <returns>The interpolated channel values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> InterpolateTetrahedron(
            ref int table,
            Vector512<int> index,
            Vector512<int> a,
            Vector512<int> b,
            Vector512<int> upper,
            Vector512<float> first,
            Vector512<float> second,
            Vector512<float> third)
        {
            Vector512<float> lower = Vector512_.Gather(ref table, index).AsSingle();
            Vector512<float> vertex1 = Vector512_.Gather(ref table, index + a).AsSingle();
            Vector512<float> vertex2 = Vector512_.Gather(ref table, index + b).AsSingle();
            Vector512<float> top = Vector512_.Gather(ref table, index + upper).AsSingle();
            return lower + ((vertex1 - lower) * first) + ((vertex2 - vertex1) * second) + ((top - vertex2) * third);
        }
    }
}

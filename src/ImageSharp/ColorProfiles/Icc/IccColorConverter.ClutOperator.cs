// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
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
        private readonly IccClut table;
        private readonly byte[] encoded;
        private readonly int padding;
        private readonly IccClutDataType storageType;
        private readonly InlineArray4<int> strides;
        private readonly InlineArray4<int> lastCells;
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
            this.table = clut;
            this.storageType = clut.TryGetEncodedValues(out byte[] encoded, out int padding) ? clut.DataType : IccClutDataType.Float;
            this.encoded = encoded;
            this.padding = padding;
            this.inputCount = clut.InputChannelCount;
            this.outputCount = clut.OutputChannelCount;
            this.tetrahedral = !useTrilinearInterpolation && this.inputCount >= 3;
            InlineArray4<int> strides = default;
            InlineArray4<int> lastCells = default;
            Vector4 maximum = default;

            // Strides count complete nodes. Each reader applies its own number of channels and sample size.
            int stride = 1;
            for (int axis = this.inputCount - 1; axis >= 0; axis--)
            {
                int points = clut.GridPointCount[axis];
                maximum[axis] = points - 1;

                // The final node belongs to the preceding cell. A one-node axis has only cell zero.
                lastCells[axis] = Math.Max(points - 2, 0);

                // A single-node axis is constant. Both corners read that node with a zero stride.
                strides[axis] = points == 1 ? 0 : stride;
                stride *= points;
            }

            this.maximum = maximum;
            this.strides = strides;
            this.lastCells = lastCells;
        }

        /// <summary>
        /// Interpolates one color with the same cell and vertex rules as the SIMD paths.
        /// </summary>
        /// <param name="value">The normalized device or PCS components.</param>
        /// <returns>The output components; unused components are zero.</returns>
        public unsafe Vector4 Convert(Vector4 value)
        {
            // Pin the stored samples for every node read. The reader supplies layout and scale;
            // cell selection and interpolation are shared by byte, ushort and float tables.
            if (this.storageType == IccClutDataType.Float)
            {
                fixed (float* data = this.table.FloatValues)
                {
                    return this.Convert(new FloatClutReadOperator(data, this.outputCount), value);
                }
            }
            else
            {
                fixed (byte* data = this.encoded)
                {
                    return this.storageType == IccClutDataType.UInt8
                        ? this.Convert(new ByteClutReadOperator(data, this.outputCount, this.padding), value)
                        : this.Convert(new UInt16ClutReadOperator(data, this.outputCount, this.padding), value);
                }
            }
        }

        /// <summary>
        /// Interpolates 4 colors, with one component per register and one color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public unsafe void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Pin the stored samples for every node read. The reader supplies layout and scale;
            // cell selection and interpolation are shared by byte, ushort and float tables.
            if (this.storageType == IccClutDataType.Float)
            {
                fixed (float* data = this.table.FloatValues)
                {
                    this.Convert(new FloatClutReadOperator(data, this.outputCount), ref x, ref y, ref z, ref w);
                }
            }
            else
            {
                fixed (byte* data = this.encoded)
                {
                    if (this.storageType == IccClutDataType.UInt8)
                    {
                        this.Convert(new ByteClutReadOperator(data, this.outputCount, this.padding), ref x, ref y, ref z, ref w);
                    }
                    else
                    {
                        this.Convert(new UInt16ClutReadOperator(data, this.outputCount, this.padding), ref x, ref y, ref z, ref w);
                    }
                }
            }
        }

        /// <summary>
        /// Interpolates node samples and normalizes the final result once.
        /// </summary>
        /// <typeparam name="TReader">The reader for the stored node samples.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="value">The normalized input components.</param>
        /// <returns>The normalized output components.</returns>
        private Vector4 Convert<TReader>(TReader reader, Vector4 value)
            where TReader : struct, IClutReadOperator
        {
            Vector4 position = ColorNumerics.ClampToFinite(value, Vector4.Zero, Vector4.One) * this.maximum;
            Vector4 fraction = default;
            int offset = 0;
            for (int axis = 0; axis < this.inputCount; axis++)
            {
                int index = Math.Min((int)position[axis], this.lastCells[axis]);
                fraction[axis] = position[axis] - index;
                offset += index * this.strides[axis];
            }

            Vector4 result = default;
            if (this.tetrahedral)
            {
                // Four-input tables use tetrahedra in the last three dimensions.
                // Interpolate between two slices along the first dimension.
                int axis = this.inputCount - 3;
                float first = fraction[axis];
                float second = fraction[axis + 1];
                float third = fraction[axis + 2];
                int a = this.strides[axis];
                int b = this.strides[axis + 1];
                int c = this.strides[axis + 2];

                // Sort the fractions and their node strides together. Equal fractions retain their axis order.
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
                b += a;
                result = Interpolate(reader, offset, a, b, upper, first, second, third);
                if (this.inputCount == 4)
                {
                    Vector4 next = Interpolate(reader, offset + this.strides[0], a, b, upper, first, second, third);
                    result = (result * (1F - fraction.X)) + (next * fraction.X);
                }

                // Use the same reciprocal multiplication as every SIMD width before later nonlinear stages.
                return result * (1F / TReader.Divisor);
            }

            // Visit corners in axis-bit order and multiply weights from the last axis to the first.
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

                result += reader.Read(index) * weight;
            }

            return result * (1F / TReader.Divisor);
        }

        /// <summary>
        /// Blends complete table nodes with nonnegative tetrahedral weights.
        /// </summary>
        /// <typeparam name="TReader">The packed node reader.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="index">The lower corner's node index.</param>
        /// <param name="a">The first vertex offset.</param>
        /// <param name="b">The second vertex offset.</param>
        /// <param name="upper">The upper corner offset.</param>
        /// <param name="first">The largest fraction.</param>
        /// <param name="second">The middle fraction.</param>
        /// <param name="third">The smallest fraction.</param>
        /// <returns>The interpolated samples before normalization.</returns>
        private static Vector4 Interpolate<TReader>(
            TReader reader,
            int index,
            int a,
            int b,
            int upper,
            float first,
            float second,
            float third)
            where TReader : struct, IClutReadOperator
        {
            // Positive weights avoid cancelling large encoded codes near black. Their sum is one.
            return (reader.Read(index) * (1F - first)) + (reader.Read(index + a) * (first - second))
                + (reader.Read(index + b) * (second - third)) + (reader.Read(index + upper) * third);
        }

        /// <summary>
        /// Interpolates 4 colors from table nodes and normalizes the final components.
        /// </summary>
        /// <typeparam name="TReader">The reader for the stored node samples.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        private void Convert<TReader>(
            TReader reader,
            ref Vector128<float> x,
            ref Vector128<float> y,
            ref Vector128<float> z,
            ref Vector128<float> w)
            where TReader : struct, IClutReadOperator
        {
            GetCell(x, this.maximum.X, this.lastCells[0], this.strides[0], out Vector128<int> ix, out Vector128<float> fx);
            GetCell(y, this.maximum.Y, this.lastCells[1], this.strides[1], out Vector128<int> iy, out Vector128<float> fy);
            GetCell(z, this.maximum.Z, this.lastCells[2], this.strides[2], out Vector128<int> iz, out Vector128<float> fz);
            GetCell(w, this.maximum.W, this.lastCells[3], this.strides[3], out Vector128<int> iw, out Vector128<float> fw);
            Vector128<int> offset = ix + iy + iz + iw;
            x = Vector128<float>.Zero;
            y = Vector128<float>.Zero;
            z = Vector128<float>.Zero;
            w = Vector128<float>.Zero;

            // Integer readers supply their maximum code; float readers supply one. Normalize once after interpolation.
            Vector128<float> normalization = Vector128.Create(1F / TReader.Divisor);

            if (this.tetrahedral)
            {
                // Four-input tables use tetrahedra in the last three dimensions.
                // Interpolate between two slices along the first dimension.
                int axis = this.inputCount - 3;
                Vector128<float> first = axis == 0 ? fx : fy;
                Vector128<float> second = axis == 0 ? fy : fz;
                Vector128<float> third = axis == 0 ? fz : fw;
                Vector128<int> a = Vector128.Create(this.strides[axis]);
                Vector128<int> b = Vector128.Create(this.strides[axis + 1]);
                Vector128<int> c = Vector128.Create(this.strides[axis + 2]);

                // Min/max select the three weights without moving a stride at every comparison.
                // The first intermediate vertex follows the largest fraction. The second excludes the smallest axis.
                Vector128<float> largestPair = Vector128.Max(first, second);
                Vector128<float> smallestPair = Vector128.Min(first, second);
                Vector128<float> largest = Vector128.Max(largestPair, third);
                Vector128<float> middle = Vector128.Max(smallestPair, Vector128.Min(largestPair, third));
                Vector128<float> smallest = Vector128.Min(smallestPair, third);

                // On ties, choose the earlier axis first and the later axis last, matching the scalar stable sort.
                Vector128<float> firstLargest = Vector128.GreaterThanOrEqual(first, second) & Vector128.GreaterThanOrEqual(first, third);
                Vector128<int> firstStep = Vector128.ConditionalSelect(
                    firstLargest.AsInt32(),
                    a,
                    Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(second, third).AsInt32(), b, c));

                Vector128<float> thirdSmallest = Vector128.LessThanOrEqual(third, first) & Vector128.LessThanOrEqual(third, second);
                Vector128<int> lastStep = Vector128.ConditionalSelect(
                    thirdSmallest.AsInt32(),
                    c,
                    Vector128.ConditionalSelect(Vector128.LessThanOrEqual(second, first).AsInt32(), b, a));

                Vector128<int> upper = a + b + c;
                a = firstStep;
                b = upper - lastStep;
                first = largest;
                second = middle;
                third = smallest;

                Interpolate(reader, offset, a, b, upper, first, second, third, out x, out y, out z, out w);
                if (this.inputCount == 4)
                {
                    Interpolate(
                        reader,
                        offset + Vector128.Create(this.strides[0]),
                        a,
                        b,
                        upper,
                        first,
                        second,
                        third,
                        out Vector128<float> nx,
                        out Vector128<float> ny,
                        out Vector128<float> nz,
                        out Vector128<float> nw);

                    Vector128<float> lowerWeight = Vector128<float>.One - fx;
                    x = (x * lowerWeight) + (nx * fx);
                    y = (y * lowerWeight) + (ny * fx);
                    z = (z * lowerWeight) + (nz * fx);
                    w = (w * lowerWeight) + (nw * fx);
                }

                x *= normalization;
                y *= normalization;
                z *= normalization;
                w *= normalization;
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
                reader.Read(index, out Vector128<float> nx, out Vector128<float> ny, out Vector128<float> nz, out Vector128<float> nw);
                x += nx * weight;
                y += ny * weight;
                z += nz * weight;
                w += nw * weight;
            }

            x *= normalization;
            y *= normalization;
            z *= normalization;
            w *= normalization;
        }

        /// <summary>
        /// Blends packed nodes with nonnegative tetrahedral weights, retaining one independent color per lane.
        /// </summary>
        /// <typeparam name="TReader">The reader for the stored node samples.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="index">The lower corner indices.</param>
        /// <param name="a">The first vertex offsets.</param>
        /// <param name="b">The second vertex offsets.</param>
        /// <param name="upper">The upper corner offsets.</param>
        /// <param name="first">The largest fractions.</param>
        /// <param name="second">The middle fractions.</param>
        /// <param name="third">The smallest fractions.</param>
        /// <param name="x">The first interpolated codes.</param>
        /// <param name="y">The second interpolated codes.</param>
        /// <param name="z">The third interpolated codes.</param>
        /// <param name="w">The fourth interpolated codes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Interpolate<TReader>(
            TReader reader,
            Vector128<int> index,
            Vector128<int> a,
            Vector128<int> b,
            Vector128<int> upper,
            Vector128<float> first,
            Vector128<float> second,
            Vector128<float> third,
            out Vector128<float> x,
            out Vector128<float> y,
            out Vector128<float> z,
            out Vector128<float> w)
            where TReader : struct, IClutReadOperator
        {
            // Inline the four node reads so interpolation can retain their component vectors and weights in registers.
            // All weights are nonnegative. Normalize once after the complete node interpolation.
            Vector128<float> weight = Vector128<float>.One - first;
            reader.Read(index, out x, out y, out z, out w);
            x *= weight;
            y *= weight;
            z *= weight;
            w *= weight;

            weight = first - second;
            reader.Read(index + a, out Vector128<float> nx, out Vector128<float> ny, out Vector128<float> nz, out Vector128<float> nw);
            x += nx * weight;
            y += ny * weight;
            z += nz * weight;
            w += nw * weight;

            weight = second - third;
            reader.Read(index + b, out nx, out ny, out nz, out nw);
            x += nx * weight;
            y += ny * weight;
            z += nz * weight;
            w += nw * weight;

            reader.Read(index + upper, out nx, out ny, out nz, out nw);
            x += nx * third;
            y += ny * third;
            z += nz * third;
            w += nw * third;
        }

        /// <summary>
        /// Interpolates 8 colors from table nodes and normalizes the final components.
        /// </summary>
        /// <typeparam name="TReader">The reader for the stored node samples.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        private void Convert<TReader>(
            TReader reader,
            ref Vector256<float> x,
            ref Vector256<float> y,
            ref Vector256<float> z,
            ref Vector256<float> w)
            where TReader : struct, IClutReadOperator
        {
            GetCell(x, this.maximum.X, this.lastCells[0], this.strides[0], out Vector256<int> ix, out Vector256<float> fx);
            GetCell(y, this.maximum.Y, this.lastCells[1], this.strides[1], out Vector256<int> iy, out Vector256<float> fy);
            GetCell(z, this.maximum.Z, this.lastCells[2], this.strides[2], out Vector256<int> iz, out Vector256<float> fz);
            GetCell(w, this.maximum.W, this.lastCells[3], this.strides[3], out Vector256<int> iw, out Vector256<float> fw);
            Vector256<int> offset = ix + iy + iz + iw;
            x = Vector256<float>.Zero;
            y = Vector256<float>.Zero;
            z = Vector256<float>.Zero;
            w = Vector256<float>.Zero;

            // Integer readers supply their maximum code; float readers supply one. Normalize once after interpolation.
            Vector256<float> normalization = Vector256.Create(1F / TReader.Divisor);

            if (this.tetrahedral)
            {
                // Four-input tables use tetrahedra in the last three dimensions.
                // Interpolate between two slices along the first dimension.
                int axis = this.inputCount - 3;
                Vector256<float> first = axis == 0 ? fx : fy;
                Vector256<float> second = axis == 0 ? fy : fz;
                Vector256<float> third = axis == 0 ? fz : fw;
                Vector256<int> a = Vector256.Create(this.strides[axis]);
                Vector256<int> b = Vector256.Create(this.strides[axis + 1]);
                Vector256<int> c = Vector256.Create(this.strides[axis + 2]);

                // Min/max select the three weights without moving a stride at every comparison.
                // The first intermediate vertex follows the largest fraction. The second excludes the smallest axis.
                Vector256<float> largestPair = Vector256.Max(first, second);
                Vector256<float> smallestPair = Vector256.Min(first, second);
                Vector256<float> largest = Vector256.Max(largestPair, third);
                Vector256<float> middle = Vector256.Max(smallestPair, Vector256.Min(largestPair, third));
                Vector256<float> smallest = Vector256.Min(smallestPair, third);

                // On ties, choose the earlier axis first and the later axis last, matching the scalar stable sort.
                Vector256<float> firstLargest = Vector256.GreaterThanOrEqual(first, second) & Vector256.GreaterThanOrEqual(first, third);
                Vector256<int> firstStep = Vector256.ConditionalSelect(
                    firstLargest.AsInt32(),
                    a,
                    Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(second, third).AsInt32(), b, c));

                Vector256<float> thirdSmallest = Vector256.LessThanOrEqual(third, first) & Vector256.LessThanOrEqual(third, second);
                Vector256<int> lastStep = Vector256.ConditionalSelect(
                    thirdSmallest.AsInt32(),
                    c,
                    Vector256.ConditionalSelect(Vector256.LessThanOrEqual(second, first).AsInt32(), b, a));

                Vector256<int> upper = a + b + c;
                a = firstStep;
                b = upper - lastStep;
                first = largest;
                second = middle;
                third = smallest;

                Interpolate(reader, offset, a, b, upper, first, second, third, out x, out y, out z, out w);
                if (this.inputCount == 4)
                {
                    Interpolate(
                        reader,
                        offset + Vector256.Create(this.strides[0]),
                        a,
                        b,
                        upper,
                        first,
                        second,
                        third,
                        out Vector256<float> nx,
                        out Vector256<float> ny,
                        out Vector256<float> nz,
                        out Vector256<float> nw);

                    Vector256<float> lowerWeight = Vector256<float>.One - fx;
                    x = (x * lowerWeight) + (nx * fx);
                    y = (y * lowerWeight) + (ny * fx);
                    z = (z * lowerWeight) + (nz * fx);
                    w = (w * lowerWeight) + (nw * fx);
                }

                x *= normalization;
                y *= normalization;
                z *= normalization;
                w *= normalization;
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
                reader.Read(index, out Vector256<float> nx, out Vector256<float> ny, out Vector256<float> nz, out Vector256<float> nw);
                x += nx * weight;
                y += ny * weight;
                z += nz * weight;
                w += nw * weight;
            }

            x *= normalization;
            y *= normalization;
            z *= normalization;
            w *= normalization;
        }

        /// <summary>
        /// Blends packed nodes with nonnegative tetrahedral weights, retaining one independent color per lane.
        /// </summary>
        /// <typeparam name="TReader">The reader for the stored node samples.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="index">The lower corner indices.</param>
        /// <param name="a">The first vertex offsets.</param>
        /// <param name="b">The second vertex offsets.</param>
        /// <param name="upper">The upper corner offsets.</param>
        /// <param name="first">The largest fractions.</param>
        /// <param name="second">The middle fractions.</param>
        /// <param name="third">The smallest fractions.</param>
        /// <param name="x">The first interpolated codes.</param>
        /// <param name="y">The second interpolated codes.</param>
        /// <param name="z">The third interpolated codes.</param>
        /// <param name="w">The fourth interpolated codes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Interpolate<TReader>(
            TReader reader,
            Vector256<int> index,
            Vector256<int> a,
            Vector256<int> b,
            Vector256<int> upper,
            Vector256<float> first,
            Vector256<float> second,
            Vector256<float> third,
            out Vector256<float> x,
            out Vector256<float> y,
            out Vector256<float> z,
            out Vector256<float> w)
            where TReader : struct, IClutReadOperator
        {
            // Inline the four node reads so interpolation can retain their component vectors and weights in registers.
            // All weights are nonnegative. Normalize once after the complete node interpolation.
            Vector256<float> weight = Vector256<float>.One - first;
            reader.Read(index, out x, out y, out z, out w);
            x *= weight;
            y *= weight;
            z *= weight;
            w *= weight;

            weight = first - second;
            reader.Read(index + a, out Vector256<float> nx, out Vector256<float> ny, out Vector256<float> nz, out Vector256<float> nw);
            x += nx * weight;
            y += ny * weight;
            z += nz * weight;
            w += nw * weight;

            weight = second - third;
            reader.Read(index + b, out nx, out ny, out nz, out nw);
            x += nx * weight;
            y += ny * weight;
            z += nz * weight;
            w += nw * weight;

            reader.Read(index + upper, out nx, out ny, out nz, out nw);
            x += nx * third;
            y += ny * third;
            z += nz * third;
            w += nw * third;
        }

        /// <summary>
        /// Interpolates 16 colors from table nodes and normalizes the final components.
        /// </summary>
        /// <typeparam name="TReader">The reader for the stored node samples.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        private void Convert<TReader>(
            TReader reader,
            ref Vector512<float> x,
            ref Vector512<float> y,
            ref Vector512<float> z,
            ref Vector512<float> w)
            where TReader : struct, IClutReadOperator
        {
            GetCell(x, this.maximum.X, this.lastCells[0], this.strides[0], out Vector512<int> ix, out Vector512<float> fx);
            GetCell(y, this.maximum.Y, this.lastCells[1], this.strides[1], out Vector512<int> iy, out Vector512<float> fy);
            GetCell(z, this.maximum.Z, this.lastCells[2], this.strides[2], out Vector512<int> iz, out Vector512<float> fz);
            GetCell(w, this.maximum.W, this.lastCells[3], this.strides[3], out Vector512<int> iw, out Vector512<float> fw);
            Vector512<int> offset = ix + iy + iz + iw;
            x = Vector512<float>.Zero;
            y = Vector512<float>.Zero;
            z = Vector512<float>.Zero;
            w = Vector512<float>.Zero;

            // Integer readers supply their maximum code; float readers supply one. Normalize once after interpolation.
            Vector512<float> normalization = Vector512.Create(1F / TReader.Divisor);

            if (this.tetrahedral)
            {
                // Four-input tables use tetrahedra in the last three dimensions.
                // Interpolate between two slices along the first dimension.
                int axis = this.inputCount - 3;
                Vector512<float> first = axis == 0 ? fx : fy;
                Vector512<float> second = axis == 0 ? fy : fz;
                Vector512<float> third = axis == 0 ? fz : fw;
                Vector512<int> a = Vector512.Create(this.strides[axis]);
                Vector512<int> b = Vector512.Create(this.strides[axis + 1]);
                Vector512<int> c = Vector512.Create(this.strides[axis + 2]);

                // Min/max select the three weights without moving a stride at every comparison.
                // The first intermediate vertex follows the largest fraction. The second excludes the smallest axis.
                Vector512<float> largestPair = Vector512.Max(first, second);
                Vector512<float> smallestPair = Vector512.Min(first, second);
                Vector512<float> largest = Vector512.Max(largestPair, third);
                Vector512<float> middle = Vector512.Max(smallestPair, Vector512.Min(largestPair, third));
                Vector512<float> smallest = Vector512.Min(smallestPair, third);

                // On ties, choose the earlier axis first and the later axis last, matching the scalar stable sort.
                Vector512<float> firstLargest = Vector512.GreaterThanOrEqual(first, second) & Vector512.GreaterThanOrEqual(first, third);
                Vector512<int> firstStep = Vector512.ConditionalSelect(
                    firstLargest.AsInt32(),
                    a,
                    Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(second, third).AsInt32(), b, c));

                Vector512<float> thirdSmallest = Vector512.LessThanOrEqual(third, first) & Vector512.LessThanOrEqual(third, second);
                Vector512<int> lastStep = Vector512.ConditionalSelect(
                    thirdSmallest.AsInt32(),
                    c,
                    Vector512.ConditionalSelect(Vector512.LessThanOrEqual(second, first).AsInt32(), b, a));

                Vector512<int> upper = a + b + c;
                a = firstStep;
                b = upper - lastStep;
                first = largest;
                second = middle;
                third = smallest;

                Interpolate(reader, offset, a, b, upper, first, second, third, out x, out y, out z, out w);
                if (this.inputCount == 4)
                {
                    Interpolate(
                        reader,
                        offset + Vector512.Create(this.strides[0]),
                        a,
                        b,
                        upper,
                        first,
                        second,
                        third,
                        out Vector512<float> nx,
                        out Vector512<float> ny,
                        out Vector512<float> nz,
                        out Vector512<float> nw);

                    Vector512<float> lowerWeight = Vector512<float>.One - fx;
                    x = (x * lowerWeight) + (nx * fx);
                    y = (y * lowerWeight) + (ny * fx);
                    z = (z * lowerWeight) + (nz * fx);
                    w = (w * lowerWeight) + (nw * fx);
                }

                x *= normalization;
                y *= normalization;
                z *= normalization;
                w *= normalization;
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
                reader.Read(index, out Vector512<float> nx, out Vector512<float> ny, out Vector512<float> nz, out Vector512<float> nw);
                x += nx * weight;
                y += ny * weight;
                z += nz * weight;
                w += nw * weight;
            }

            x *= normalization;
            y *= normalization;
            z *= normalization;
            w *= normalization;
        }

        /// <summary>
        /// Blends packed nodes with nonnegative tetrahedral weights, retaining one independent color per lane.
        /// </summary>
        /// <typeparam name="TReader">The reader for the stored node samples.</typeparam>
        /// <param name="reader">The reader valid within the caller's pin scope.</param>
        /// <param name="index">The lower corner indices.</param>
        /// <param name="a">The first vertex offsets.</param>
        /// <param name="b">The second vertex offsets.</param>
        /// <param name="upper">The upper corner offsets.</param>
        /// <param name="first">The largest fractions.</param>
        /// <param name="second">The middle fractions.</param>
        /// <param name="third">The smallest fractions.</param>
        /// <param name="x">The first interpolated codes.</param>
        /// <param name="y">The second interpolated codes.</param>
        /// <param name="z">The third interpolated codes.</param>
        /// <param name="w">The fourth interpolated codes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Interpolate<TReader>(
            TReader reader,
            Vector512<int> index,
            Vector512<int> a,
            Vector512<int> b,
            Vector512<int> upper,
            Vector512<float> first,
            Vector512<float> second,
            Vector512<float> third,
            out Vector512<float> x,
            out Vector512<float> y,
            out Vector512<float> z,
            out Vector512<float> w)
            where TReader : struct, IClutReadOperator
        {
            // Inline the four node reads so interpolation can retain their component vectors and weights in registers.
            // All weights are nonnegative. Normalize once after the complete node interpolation.
            Vector512<float> weight = Vector512<float>.One - first;
            reader.Read(index, out x, out y, out z, out w);
            x *= weight;
            y *= weight;
            z *= weight;
            w *= weight;

            weight = first - second;
            reader.Read(index + a, out Vector512<float> nx, out Vector512<float> ny, out Vector512<float> nz, out Vector512<float> nw);
            x += nx * weight;
            y += ny * weight;
            z += nz * weight;
            w += nw * weight;

            weight = second - third;
            reader.Read(index + b, out nx, out ny, out nz, out nw);
            x += nx * weight;
            y += ny * weight;
            z += nz * weight;
            w += nw * weight;

            reader.Read(index + upper, out nx, out ny, out nz, out nw);
            x += nx * third;
            y += ny * third;
            z += nz * third;
            w += nw * third;
        }

        /// <summary>
        /// Finds the lower cell offset and fraction for each lane, including endpoints and constant axes.
        /// </summary>
        /// <param name="value">The normalized input component.</param>
        /// <param name="maximum">The last grid node on this axis.</param>
        /// <param name="lastCell">The last lower cell index, computed during profile setup.</param>
        /// <param name="stride">The distance between nodes, or zero for a constant axis.</param>
        /// <param name="offset">The lower node's table offset.</param>
        /// <param name="fraction">The distance from the lower node in grid cells.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetCell(
            Vector128<float> value,
            float maximum,
            int lastCell,
            int stride,
            out Vector128<int> offset,
            out Vector128<float> fraction)
        {
            // Ordered comparisons map NaN to zero, as ColorNumerics.ClampToFinite does. Bound indices before unchecked gathers.
            value = Vector128.ConditionalSelect(Vector128.GreaterThan(value, Vector128<float>.One), Vector128<float>.One, value);
            value = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(value, Vector128<float>.Zero), value, Vector128<float>.Zero);
            Vector128<float> position = value * Vector128.Create(maximum);

            // Clamping and byte-sized grid dimensions bound positions to [0,254].
            // Native conversion is safe here and avoids the general conversion's saturation and NaN fixups.
            Vector128<int> index = Vector128.Min(Vector128.ConvertToInt32Native(position), Vector128.Create(lastCell));
            fraction = position - Vector128.ConvertToSingle(index);
            offset = index * Vector128.Create(stride);
        }

        /// <summary>
        /// Interpolates 8 colors, with one component per register and one color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public unsafe void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Pin the stored samples for every node read. The reader supplies layout and scale;
            // cell selection and interpolation are shared by byte, ushort and float tables.
            if (this.storageType == IccClutDataType.Float)
            {
                fixed (float* data = this.table.FloatValues)
                {
                    this.Convert(new FloatClutReadOperator(data, this.outputCount), ref x, ref y, ref z, ref w);
                }
            }
            else
            {
                fixed (byte* data = this.encoded)
                {
                    if (this.storageType == IccClutDataType.UInt8)
                    {
                        this.Convert(new ByteClutReadOperator(data, this.outputCount, this.padding), ref x, ref y, ref z, ref w);
                    }
                    else
                    {
                        this.Convert(new UInt16ClutReadOperator(data, this.outputCount, this.padding), ref x, ref y, ref z, ref w);
                    }
                }
            }
        }

        /// <summary>
        /// Finds the lower cell offset and fraction for each lane, including endpoints and constant axes.
        /// </summary>
        /// <param name="value">The normalized input component.</param>
        /// <param name="maximum">The last grid node on this axis.</param>
        /// <param name="lastCell">The last lower cell index, computed during profile setup.</param>
        /// <param name="stride">The distance between nodes, or zero for a constant axis.</param>
        /// <param name="offset">The lower node's table offset.</param>
        /// <param name="fraction">The distance from the lower node in grid cells.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetCell(
            Vector256<float> value,
            float maximum,
            int lastCell,
            int stride,
            out Vector256<int> offset,
            out Vector256<float> fraction)
        {
            // Ordered comparisons map NaN to zero, as ColorNumerics.ClampToFinite does. Bound indices before unchecked gathers.
            value = Vector256.ConditionalSelect(Vector256.GreaterThan(value, Vector256<float>.One), Vector256<float>.One, value);
            value = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(value, Vector256<float>.Zero), value, Vector256<float>.Zero);
            Vector256<float> position = value * Vector256.Create(maximum);

            // Clamping and byte-sized grid dimensions bound positions to [0,254].
            // Native conversion is safe here and avoids the general conversion's saturation and NaN fixups.
            Vector256<int> index = Vector256.Min(Vector256.ConvertToInt32Native(position), Vector256.Create(lastCell));
            fraction = position - Vector256.ConvertToSingle(index);
            offset = index * Vector256.Create(stride);
        }

        /// <summary>
        /// Interpolates 16 colors, with one component per register and one color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public unsafe void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Pin the stored samples for every node read. The reader supplies layout and scale;
            // cell selection and interpolation are shared by byte, ushort and float tables.
            if (this.storageType == IccClutDataType.Float)
            {
                fixed (float* data = this.table.FloatValues)
                {
                    this.Convert(new FloatClutReadOperator(data, this.outputCount), ref x, ref y, ref z, ref w);
                }
            }
            else
            {
                fixed (byte* data = this.encoded)
                {
                    if (this.storageType == IccClutDataType.UInt8)
                    {
                        this.Convert(new ByteClutReadOperator(data, this.outputCount, this.padding), ref x, ref y, ref z, ref w);
                    }
                    else
                    {
                        this.Convert(new UInt16ClutReadOperator(data, this.outputCount, this.padding), ref x, ref y, ref z, ref w);
                    }
                }
            }
        }

        /// <summary>
        /// Finds the lower cell offset and fraction for each lane, including endpoints and constant axes.
        /// </summary>
        /// <param name="value">The normalized input component.</param>
        /// <param name="maximum">The last grid node on this axis.</param>
        /// <param name="lastCell">The last lower cell index, computed during profile setup.</param>
        /// <param name="stride">The distance between nodes, or zero for a constant axis.</param>
        /// <param name="offset">The lower node's table offset.</param>
        /// <param name="fraction">The distance from the lower node in grid cells.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetCell(
            Vector512<float> value,
            float maximum,
            int lastCell,
            int stride,
            out Vector512<int> offset,
            out Vector512<float> fraction)
        {
            // Ordered comparisons map NaN to zero, as ColorNumerics.ClampToFinite does. Bound indices before unchecked gathers.
            value = Vector512.ConditionalSelect(Vector512.GreaterThan(value, Vector512<float>.One), Vector512<float>.One, value);
            value = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(value, Vector512<float>.Zero), value, Vector512<float>.Zero);
            Vector512<float> position = value * Vector512.Create(maximum);

            // Clamping and byte-sized grid dimensions bound positions to [0,254].
            // Native conversion is safe here and avoids the general conversion's saturation and NaN fixups.
            Vector512<int> index = Vector512.Min(Vector512.ConvertToInt32Native(position), Vector512.Create(lastCell));
            fraction = position - Vector512.ConvertToSingle(index);
            offset = index * Vector512.Create(stride);
        }
    }
}

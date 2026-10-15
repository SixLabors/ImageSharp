// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Applies an ICC three-channel matrix and offset while retaining the unused fourth component.
    /// </summary>
    internal readonly struct MatrixOperator
    {
        private readonly Matrix4x4 matrix;
        private readonly Vector3 offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="MatrixOperator"/> struct.
        /// </summary>
        /// <param name="matrix">The profile's three-by-three matrix, embedded in a four-by-four identity matrix.</param>
        /// <param name="offset">The offset applied after matrix multiplication.</param>
        public MatrixOperator(Matrix4x4 matrix, Vector3 offset)
        {
            this.matrix = matrix;
            this.offset = offset;
        }

        /// <summary>
        /// Applies the matrix and offset to one color.
        /// </summary>
        /// <param name="value">The source components.</param>
        /// <returns>The transformed components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Convert(Vector4 value) => Vector4.Transform(value, this.matrix) + new Vector4(this.offset, 0F);

        /// <summary>
        /// Applies the matrix and offset to 4 colors held in component registers.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z)
        {
            // The shared matrix helper keeps each color in its own lane. Profile matrices have no fourth-channel terms.
            SimdUtils.Transform3(this.matrix, x, y, z, out x, out y, out z);
            x += Vector128.Create(this.offset.X);
            y += Vector128.Create(this.offset.Y);
            z += Vector128.Create(this.offset.Z);
        }

        /// <summary>
        /// Applies the matrix and offset to 8 colors held in component registers.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z)
        {
            // The shared matrix helper keeps each color in its own lane. Profile matrices have no fourth-channel terms.
            SimdUtils.Transform3(this.matrix, x, y, z, out x, out y, out z);
            x += Vector256.Create(this.offset.X);
            y += Vector256.Create(this.offset.Y);
            z += Vector256.Create(this.offset.Z);
        }

        /// <summary>
        /// Applies the matrix and offset to 16 colors held in component registers.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z)
        {
            // The shared matrix helper keeps each color in its own lane. Profile matrices have no fourth-channel terms.
            SimdUtils.Transform3(this.matrix, x, y, z, out x, out y, out z);
            x += Vector512.Create(this.offset.X);
            y += Vector512.Create(this.offset.Y);
            z += Vector512.Create(this.offset.Z);
        }
    }
}

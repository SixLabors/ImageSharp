// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <content>
/// Holds the component arithmetic and coefficients selected before the decoder processes rows.
/// </content>
internal abstract partial class JxlColorConverter
{
    /// <summary>
    /// Restores JPEG XL's Cb, Y and Cr layout and applies the shared BT.601 inverse matrix.
    /// </summary>
    private readonly struct YCbCrOperator : IColorOperator
    {
        /// <summary>
        /// The luma bias removed by JPEG XL encoding, in units where RGB white equals one.
        /// </summary>
        private const float LumaOffset = 128F / 255F;

        /// <summary>
        /// The shared inverse matrix transposed once to Vector3.Transform order.
        /// </summary>
        private readonly Matrix4x4 matrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCrOperator"/> struct.
        /// </summary>
        public YCbCrOperator() => this.matrix = Matrix4x4.Transpose(KnownYCbCrMatrices.BT601.Inverse);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float component0, ref float component1, ref float component2)
        {
            Vector3 rgb = Vector3.Transform(new Vector3(component1 + LumaOffset, component0, component2), this.matrix);
            component0 = rgb.X;
            component1 = rgb.Y;
            component2 = rgb.Z;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> component0, ref Vector128<float> component1, ref Vector128<float> component2)
        {
            // Chroma is already centered. Only Y needs its wire-defined bias restored before the shared matrix.
            SimdUtils.Transform3(
                in this.matrix,
                component1 + Vector128.Create(LumaOffset),
                component0,
                component2,
                out component0,
                out component1,
                out component2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> component0, ref Vector256<float> component1, ref Vector256<float> component2)
        {
            // Chroma is already centered. Only Y needs its wire-defined bias restored before the shared matrix.
            SimdUtils.Transform3(
                in this.matrix,
                component1 + Vector256.Create(LumaOffset),
                component0,
                component2,
                out component0,
                out component1,
                out component2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> component0, ref Vector512<float> component1, ref Vector512<float> component2)
        {
            // Chroma is already centered. Only Y needs its wire-defined bias restored before the shared matrix.
            SimdUtils.Transform3(
                in this.matrix,
                component1 + Vector512.Create(LumaOffset),
                component0,
                component2,
                out component0,
                out component1,
                out component2);
        }
    }

    /// <summary>
    /// Reconstructs linear RGB through the shared XYB arithmetic, without an intermediate pixel conversion.
    /// </summary>
    private readonly struct XybOperator : IColorOperator
    {
        /// <summary>
        /// The inverse matrix scaled for the original reference intensity.
        /// </summary>
        private readonly Matrix4x4 matrix;

        /// <summary>
        /// The signed biases added after cubing.
        /// </summary>
        private readonly Vector3 biases;

        /// <summary>
        /// The signed cube roots removed before cubing.
        /// </summary>
        private readonly Vector3 cubeRootBiases;

        /// <summary>
        /// Initializes a new instance of the <see cref="XybOperator"/> struct.
        /// </summary>
        /// <param name="transform">The parsed inverse transform and original reference intensity.</param>
        public XybOperator(XybTransform transform)
        {
            this.matrix = transform.ScaledInverse;
            this.biases = transform.Biases;
            this.cubeRootBiases = transform.CubeRootBiases;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float component0, ref float component1, ref float component2)
            => Xyb.ConvertToRgb(
                component0,
                component1,
                component2,
                in this.matrix,
                this.biases,
                this.cubeRootBiases,
                out component0,
                out component1,
                out component2);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> component0, ref Vector128<float> component1, ref Vector128<float> component2)
            => Xyb.ConvertToRgb(
                component0,
                component1,
                component2,
                in this.matrix,
                this.biases,
                this.cubeRootBiases,
                out component0,
                out component1,
                out component2);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> component0, ref Vector256<float> component1, ref Vector256<float> component2)
            => Xyb.ConvertToRgb(
                component0,
                component1,
                component2,
                in this.matrix,
                this.biases,
                this.cubeRootBiases,
                out component0,
                out component1,
                out component2);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> component0, ref Vector512<float> component1, ref Vector512<float> component2)
            => Xyb.ConvertToRgb(
                component0,
                component1,
                component2,
                in this.matrix,
                this.biases,
                this.cubeRootBiases,
                out component0,
                out component1,
                out component2);
    }
}

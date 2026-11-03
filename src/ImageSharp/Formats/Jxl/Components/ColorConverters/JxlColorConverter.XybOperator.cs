// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Reconstructs coding XYB directly to linear sRGB with shared scalar and SIMD arithmetic.
    /// </summary>
    internal readonly struct XybOperator : IJxlColorConverterOperator
    {
        private readonly Xyb.InverseOperator inverse;

        /// <summary>
        /// Initializes a new instance of the <see cref="XybOperator"/> struct.
        /// </summary>
        /// <param name="transform">The header's inverse absorbance matrix, biases and intensity target.</param>
        public XybOperator(XybTransform transform)
        {
            // ISO coefficients are row-major. Prepare the System.Numerics row-vector layout once.
            // Linear RGB one represents the header intensity, relative to the 255-nit opsin reference.
            Matrix4x4 matrix = Matrix4x4.Transpose(transform.Inverse) * (XybTransform.ReferenceIntensity / transform.IntensityTarget);
            this.inverse = new Xyb.InverseOperator(matrix, transform.Biases, transform.CubeRootBiases);
        }

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Reconstructs coding XYB directly to prepared linear RGB coordinates with shared scalar and SIMD arithmetic.
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
            // The shared preparation leaves the default sRGB coordinates unchanged and applies
            // only the header intensity scale after transposing the serialized inverse matrix.
            this.inverse = new Xyb.InverseOperator(transform, KnownRgbWorkingSpaces.SRgb, false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XybOperator"/> struct for the original RGB or grayscale coordinates.
        /// </summary>
        /// <param name="transform">The header's inverse absorbance matrix, biases and intensity target.</param>
        /// <param name="target">The selected original working space, with its transfer applied after reconstruction.</param>
        /// <param name="grayscale">Whether the three outputs repeat the D65 luminance component.</param>
        public XybOperator(XybTransform transform, RgbWorkingSpace target, bool grayscale)
            => this.inverse = new Xyb.InverseOperator(transform, target, grayscale);

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

    /// <summary>
    /// Converts linear sRGB to coding XYB.
    /// </summary>
    internal readonly struct XybForwardOperator : IJxlColorConverterOperator
    {
        /// <summary>
        /// The shared forward arithmetic with its actual RGB-to-XYZ connection composed during image setup.
        /// </summary>
        private readonly Xyb.ForwardOperator forward;

        /// <summary>
        /// Initializes a new instance of the <see cref="XybForwardOperator"/> struct for linear sRGB input.
        /// </summary>
        /// <param name="transform">The coding XYB matrices, biases and intensity target.</param>
        public XybForwardOperator(XybTransform transform)
        {
            RgbWorkingSpace workingSpace = KnownRgbWorkingSpaces.SRgb;
            XybTransform prepared = transform.Prepare(workingSpace.RgbToCieXyzMatrix, workingSpace.CieXyzToRgbMatrix);
            Matrix4x4 rgbToXyz = workingSpace.RgbToCieXyzMatrix;

            // The shared forward matrix consumes [1,Z,Y,X]. This real RGB-to-XYZ connection maps
            // [1,B,G,R] to that order. Compose the matrices once to retain one absorbance traversal per row.
            // The first row preserves the homogeneous bias. The other rows contain blue, green and red XYZ contributions.
            Matrix4x4 connection = new(
                1F,
                0F,
                0F,
                0F,
                0F,
                rgbToXyz.M33,
                rgbToXyz.M32,
                rgbToXyz.M31,
                0F,
                rgbToXyz.M23,
                rgbToXyz.M22,
                rgbToXyz.M21,
                0F,
                rgbToXyz.M13,
                rgbToXyz.M12,
                rgbToXyz.M11);

            Matrix4x4 matrix = connection * prepared.Forward;
            this.forward = new Xyb.ForwardOperator(matrix, prepared.CubeRootBiases);
        }

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            this.forward.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Each register contains one linear RGB component from consecutive pixels. Shared XYB arithmetic
            // returns X, Y and B in the same lane order. The fourth component remains separate from color.
            this.forward.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Each register contains one linear RGB component from consecutive pixels. Shared XYB arithmetic
            // returns X, Y and B in the same lane order. The fourth component remains separate from color.
            this.forward.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Each register contains one linear RGB component from consecutive pixels. Shared XYB arithmetic
            // returns X, Y and B in the same lane order. The fourth component remains separate from color.
            this.forward.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }
    }
}

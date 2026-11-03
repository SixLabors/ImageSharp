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
    /// Converts the scaled XYB device encoding to linear sRGB without an intermediate ICC profile.
    /// </summary>
    /// <remarks>
    /// These samples differ from coding XYB. The device encoding scales X and Y separately and stores B minus Y.
    /// The fixed mapping follows libjxl lib/jxl/enc_xyb.cc, ScaleXYBRow, and lib/jxl/cms/opsin_params.h.
    /// The cube-root domain cutoff follows CreateICCLutAtoBTagForXYB in lib/jxl/cms/jxl_cms_internal.h.
    /// Conversion uses the standard 255-nit transform, independently of the image's custom coding transform.
    /// </remarks>
    /// <typeparam name="TComponentOperator">The existing RGB or YCbCr component restoration selected for this frame.</typeparam>
    internal struct XybDeviceOperator<TComponentOperator> : IJxlColorConverterOperator
        where TComponentOperator : struct, IJxlColorConverterOperator
    {
        // ScaleXYBRow stores (X + offsetX) * scaleX, Y * scaleY and (B - Y + offsetB) * scaleB.
        // Keep these encoding constants here; they describe device sample units, not the opsin color matrix.
        private const float XOffset = 0.015386134F;
        private const float BOffset = 0.27770459F;
        private const float XScale = 22.995788804F;
        private const float YScale = 1.183000077F;
        private const float BScale = 1.502141333F;

        // Keep the constrained receiver writable to avoid copying prepared component coefficients on each call.
        private TComponentOperator componentOperation;
        private readonly Matrix4x4 inputMapping;
        private readonly Xyb.InverseOperator inverse;

        /// <summary>
        /// Initializes a new instance of the <see cref="XybDeviceOperator{TComponentOperator}"/> struct.
        /// </summary>
        /// <param name="componentOperation">The prepared operation that restores the three scaled device components.</param>
        public XybDeviceOperator(TComponentOperator componentOperation)
        {
            this.componentOperation = componentOperation;
            XybTransform transform = KnownXybTransforms.Iso18181;
            Vector3 roots = transform.CubeRootBiases;
            float inverseX = 1F / XScale;
            float inverseY = 1F / YScale;
            float inverseB = 1F / BScale;

            // Combine sample unmapping with the opponent mix and root offsets. The three results are
            // Y+X-rootBias, Y-X-rootBias and B-rootBias. B also restores the Y removed by device encoding.
            this.inputMapping = new Matrix4x4(
                inverseX,
                -inverseX,
                0F,
                0F,
                inverseY,
                inverseY,
                inverseY,
                0F,
                0F,
                0F,
                inverseB,
                0F,
                -XOffset - roots.X,
                XOffset - roots.Y,
                -BOffset - roots.Z,
                1F);

            Matrix4x4 matrix = Matrix4x4.Transpose(transform.Inverse);
            this.inverse = new Xyb.InverseOperator(matrix, transform.Biases, roots);
        }

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            this.componentOperation.Invoke(ref c0, ref c1, ref c2, ref c3);
            Vector3 gamma = Vector3.Transform(new Vector3(c0, c1, c2), this.inputMapping);

            // The declared device curve is zero below its root-domain cutoff. This is part of that
            // encoding's curve, not pixel clipping. Linear RGB keeps its extended range after the matrix.
            Vector3 result = this.inverse.CubeAndTransform(Vector3.Max(gamma, Vector3.Zero));
            c0 = result.X;
            c1 = result.Y;
            c2 = result.Z;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Each register holds one component from consecutive pixels. Restore RGB/YCbCr coding first,
            // then unmap the device encoding and share the inverse cube and matrix with coding XYB.
            this.componentOperation.Invoke(ref c0, ref c1, ref c2, ref c3);
            SimdUtils.Transform3(in this.inputMapping, c0, c1, c2, out c0, out c1, out c2);
            c0 = Vector128.Max(c0, Vector128<float>.Zero);
            c1 = Vector128.Max(c1, Vector128<float>.Zero);
            c2 = Vector128.Max(c2, Vector128<float>.Zero);
            this.inverse.CubeAndTransform(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Each register holds one component from consecutive pixels. Restore RGB/YCbCr coding first,
            // then unmap the device encoding and share the inverse cube and matrix with coding XYB.
            this.componentOperation.Invoke(ref c0, ref c1, ref c2, ref c3);
            SimdUtils.Transform3(in this.inputMapping, c0, c1, c2, out c0, out c1, out c2);
            c0 = Vector256.Max(c0, Vector256<float>.Zero);
            c1 = Vector256.Max(c1, Vector256<float>.Zero);
            c2 = Vector256.Max(c2, Vector256<float>.Zero);
            this.inverse.CubeAndTransform(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Each register holds one component from consecutive pixels. Restore RGB/YCbCr coding first,
            // then unmap the device encoding and share the inverse cube and matrix with coding XYB.
            this.componentOperation.Invoke(ref c0, ref c1, ref c2, ref c3);
            SimdUtils.Transform3(in this.inputMapping, c0, c1, c2, out c0, out c1, out c2);
            c0 = Vector512.Max(c0, Vector512<float>.Zero);
            c1 = Vector512.Max(c1, Vector512<float>.Zero);
            c2 = Vector512.Max(c2, Vector512<float>.Zero);
            this.inverse.CubeAndTransform(c0, c1, c2, out c0, out c1, out c2);
        }
    }
}

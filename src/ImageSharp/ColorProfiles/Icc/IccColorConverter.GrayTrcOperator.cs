// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Converts device gray through its tone curve and the achromatic axis of its PCS.
    /// </summary>
    internal readonly struct GrayTrcOperator : IIccColorConverterOperator
    {
        private static readonly Vector3 ScaledD50 = KnownIlluminants.D50Icc.ToScaledVector4().AsVector3();
        private readonly CurveOperator curve;
        private readonly bool toPcs;
        private readonly bool isLab;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrayTrcOperator"/> struct.
        /// </summary>
        /// <param name="curve">The gray tone response curve.</param>
        /// <param name="pcs">The XYZ or Lab connection space.</param>
        /// <param name="toPcs">Whether to convert device gray to encoded PCS values.</param>
        public GrayTrcOperator(IccTagDataEntry curve, IccColorSpaceType pcs, bool toPcs)
        {
            this.curve = new CurveOperator(curve, !toPcs);
            this.toPcs = toPcs;
            this.isLab = pcs == IccColorSpaceType.CieLab;
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            if (this.toPcs)
            {
                float gray = this.curve.Convert(value.X);
                return this.isLab ? new Vector4(gray, 128F / 255F, 128F / 255F, 1F) : new Vector4(ScaledD50 * gray, 1F);
            }

            float luminance = this.isLab ? value.X : value.Y * PcsXyzDecodeScale;
            return new Vector4(this.curve.Convert(luminance));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            if (this.toPcs)
            {
                Vector128<float> gray = this.curve.Convert(x);
                if (this.isLab)
                {
                    // Neutral Lab encodes a* and b* as 128/255. L*/100 is already normalized.
                    x = gray;
                    y = Vector128.Create(128F / 255F);
                    z = y;
                }
                else
                {
                    // One gray value scales all three components of the profile's D50 white point.
                    x = gray * Vector128.Create(ScaledD50.X);
                    y = gray * Vector128.Create(ScaledD50.Y);
                    z = gray * Vector128.Create(ScaledD50.Z);
                }

                w = Vector128<float>.One;
            }
            else
            {
                // XYZ luminance is Y, not X. Undo its ICC encoding before applying the inverse gray curve.
                x = this.curve.Convert(this.isLab ? x : y * Vector128.Create(PcsXyzDecodeScale));
                y = x;
                z = x;
                w = x;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            if (this.toPcs)
            {
                Vector256<float> gray = this.curve.Convert(x);
                if (this.isLab)
                {
                    // Neutral Lab encodes a* and b* as 128/255. L*/100 is already normalized.
                    x = gray;
                    y = Vector256.Create(128F / 255F);
                    z = y;
                }
                else
                {
                    // One gray value scales all three components of the profile's D50 white point.
                    x = gray * Vector256.Create(ScaledD50.X);
                    y = gray * Vector256.Create(ScaledD50.Y);
                    z = gray * Vector256.Create(ScaledD50.Z);
                }

                w = Vector256<float>.One;
            }
            else
            {
                // XYZ luminance is Y, not X. Undo its ICC encoding before applying the inverse gray curve.
                x = this.curve.Convert(this.isLab ? x : y * Vector256.Create(PcsXyzDecodeScale));
                y = x;
                z = x;
                w = x;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            if (this.toPcs)
            {
                Vector512<float> gray = this.curve.Convert(x);
                if (this.isLab)
                {
                    // Neutral Lab encodes a* and b* as 128/255. L*/100 is already normalized.
                    x = gray;
                    y = Vector512.Create(128F / 255F);
                    z = y;
                }
                else
                {
                    // One gray value scales all three components of the profile's D50 white point.
                    x = gray * Vector512.Create(ScaledD50.X);
                    y = gray * Vector512.Create(ScaledD50.Y);
                    z = gray * Vector512.Create(ScaledD50.Z);
                }

                w = Vector512<float>.One;
            }
            else
            {
                // XYZ luminance is Y, not X. Undo its ICC encoding before applying the inverse gray curve.
                x = this.curve.Convert(this.isLab ? x : y * Vector512.Create(PcsXyzDecodeScale));
                y = x;
                z = x;
                w = x;
            }
        }
    }
}

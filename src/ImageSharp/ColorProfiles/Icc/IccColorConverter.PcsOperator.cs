// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using static SixLabors.ImageSharp.ColorProfiles.ColorProfileConverterExtensionsIcc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Connects encoded ICC PCS values through XYZ while applying the selected black-point adjustments.
    /// </summary>
    internal readonly struct PcsOperator : IIccColorConverterOperator
    {
        // These XYZ coefficients implement the ICC perceptual black-point adjustment.
        // Keep the rounded coefficients unchanged in scalar and SIMD arithmetic.
        private const float SourceBlackPointScaleX = 0.9965153F;
        private const float SourceBlackPointScaleY = 0.9965269F;
        private const float SourceBlackPointScaleZ = 0.9965208F;
        private const float SourceBlackPointOffsetX = 0.00336F;
        private const float SourceBlackPointOffsetY = 0.0034731F;
        private const float SourceBlackPointOffsetZ = 0.00287F;
        private const float TargetBlackPointScaleX = 1.0034969F;
        private const float TargetBlackPointScaleY = 1.0034852F;
        private const float TargetBlackPointScaleZ = 1.0034913F;
        private const float TargetBlackPointOffsetX = 0.0033717495F;
        private const float TargetBlackPointOffsetY = 0.0034852044F;
        private const float TargetBlackPointOffsetZ = 0.0028800198F;

        private readonly ColorConversionOptions options;
        private readonly bool sourceLab;
        private readonly bool targetLab;
        private readonly bool sourceV2;
        private readonly bool targetV2;
        private readonly bool adjustFrom;
        private readonly bool adjustTo;

        /// <summary>
        /// Initializes a new instance of the <see cref="PcsOperator"/> struct.
        /// </summary>
        /// <param name="source">The selected source transform and PCS encoding.</param>
        /// <param name="target">The selected target transform and PCS encoding.</param>
        /// <param name="options">The PCS converter options, with D50 as both white points.</param>
        public PcsOperator(ConversionParams source, ConversionParams target, ColorConversionOptions options)
        {
            this.options = options;
            this.sourceLab = source.PcsType switch
            {
                IccColorSpaceType.CieLab => true,
                IccColorSpaceType.CieXyz => false,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported ICC profile connection space.")
            };

            this.targetLab = target.PcsType switch
            {
                IccColorSpaceType.CieLab => true,
                IccColorSpaceType.CieXyz => false,
                _ => throw new ArgumentOutOfRangeException(nameof(target), "Unsupported ICC profile connection space.")
            };

            this.sourceV2 = source.Is16BitLutEntry;
            this.targetV2 = target.Is16BitLutEntry;
            bool oneV2 = source.HasV2PerceptualHandling ^ target.HasV2PerceptualHandling;
            this.adjustFrom = source.HasNoPerceptualHandling || (oneV2 && source.HasV2PerceptualHandling);
            this.adjustTo = target.HasNoPerceptualHandling || (oneV2 && target.HasV2PerceptualHandling);
        }

        /// <inheritdoc/>
        public bool IsTrc => false;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <summary>
        /// Gets a value indicating whether both PCS encodings are XYZ, so no Lab conversion separates their matrices.
        /// </summary>
        public bool CanCombineXyzMatrices => !this.sourceLab && !this.targetLab;

        /// <summary>
        /// Gets the linear XYZ adjustment for a connection between two matrix/TRC profiles.
        /// </summary>
        /// <param name="clipNegativeXyz">Whether target black-point adjustment clips negative XYZ.</param>
        /// <returns>The XYZ scale and offset, without the temporary PCS encoding.</returns>
        public MatrixOperator GetXyzAdjustment(out bool clipNegativeXyz)
        {
            // Matrix/TRC profiles connect through XYZ. Their PCS encode/decode scales cancel between stages.
            // Combine only the linear black-point adjustments; clipping remains a separate operation.
            Vector3 scale = Vector3.One;
            Vector3 offset = Vector3.Zero;
            if (this.adjustFrom)
            {
                scale = new Vector3(SourceBlackPointScaleX, SourceBlackPointScaleY, SourceBlackPointScaleZ);
                offset = new Vector3(SourceBlackPointOffsetX, SourceBlackPointOffsetY, SourceBlackPointOffsetZ);
            }

            if (this.adjustTo)
            {
                Vector3 targetScale = new(TargetBlackPointScaleX, TargetBlackPointScaleY, TargetBlackPointScaleZ);
                scale *= targetScale;
                offset = (offset * targetScale) - new Vector3(TargetBlackPointOffsetX, TargetBlackPointOffsetY, TargetBlackPointOffsetZ);
            }

            clipNegativeXyz = this.adjustTo;
            return new MatrixOperator(Matrix4x4.CreateScale(scale), offset);
        }

        /// <summary>
        /// Connects one color using the same PCS decoding, clipping and adjustment order as the SIMD paths.
        /// </summary>
        /// <param name="value">The source's encoded PCS color.</param>
        /// <returns>The target's encoded PCS color.</returns>
        public Vector4 Convert(Vector4 value)
        {
            // Equal connection spaces need no color-model round trip. Legacy Lab encoding alone changes its scale.
            if (!this.adjustFrom && !this.adjustTo && this.sourceLab == this.targetLab)
            {
                return this.sourceLab && this.sourceV2 != this.targetV2
                    ? value * (this.sourceV2 ? LegacyLabDecodeScale : LegacyLabEncodeScale)
                    : value;
            }

            CieXyz xyz;
            if (this.sourceLab)
            {
                value = this.sourceV2 ? value * LegacyLabDecodeScale : value;
                CieLab lab = CieLab.FromScaledVector4(value);
                xyz = lab.ToProfileConnectingSpace(this.options);
            }
            else
            {
                xyz = CieXyz.FromScaledVector4(value);
            }

            Vector3 components = xyz.AsVector3Unsafe();
            if (this.adjustFrom)
            {
                components = this.sourceLab ? Vector3.Max(components, Vector3.Zero) : components;
                components = (components * new Vector3(SourceBlackPointScaleX, SourceBlackPointScaleY, SourceBlackPointScaleZ))
                    + new Vector3(SourceBlackPointOffsetX, SourceBlackPointOffsetY, SourceBlackPointOffsetZ);
            }

            if (this.adjustTo)
            {
                components = (components * new Vector3(TargetBlackPointScaleX, TargetBlackPointScaleY, TargetBlackPointScaleZ))
                    - new Vector3(TargetBlackPointOffsetX, TargetBlackPointOffsetY, TargetBlackPointOffsetZ);
                components = this.targetLab ? components : Vector3.Max(components, Vector3.Zero);
            }

            xyz = new CieXyz(components);
            if (this.targetLab)
            {
                CieLab lab = CieLab.FromProfileConnectingSpace(this.options, in xyz);
                value = lab.ToScaledVector4();
                return this.targetV2 ? value * LegacyLabEncodeScale : value;
            }

            return xyz.ToScaledVector4();
        }

        /// <summary>
        /// Connects the PCS components in registers, with one independent color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Matching PCS values are already encoded for the target, except for legacy Lab's scale.
            if (!this.adjustFrom && !this.adjustTo && this.sourceLab == this.targetLab)
            {
                if (this.sourceLab && this.sourceV2 != this.targetV2)
                {
                    Vector128<float> scale = Vector128.Create(this.sourceV2 ? LegacyLabDecodeScale : LegacyLabEncodeScale);
                    x *= scale;
                    y *= scale;
                    z *= scale;
                    w *= scale;
                }

                return;
            }

            if (this.sourceLab)
            {
                if (this.sourceV2)
                {
                    x *= Vector128.Create(LegacyLabDecodeScale);
                    y *= Vector128.Create(LegacyLabDecodeScale);
                    z *= Vector128.Create(LegacyLabDecodeScale);
                }

                x *= Vector128.Create(100F);
                y = (y * Vector128.Create(255F)) - Vector128.Create(128F);
                z = (z * Vector128.Create(255F)) - Vector128.Create(128F);
                CieXyz white = this.options.SourceWhitePoint;
                CieLab.ToProfileConnectionSpace(ref x, ref y, ref z, in white);
            }
            else
            {
                x *= Vector128.Create(PcsXyzDecodeScale);
                y *= Vector128.Create(PcsXyzDecodeScale);
                z *= Vector128.Create(PcsXyzDecodeScale);
            }

            // Keep ICC v2 black-point correction in XYZ. Lab sources clip negative XYZ before correction;
            // XYZ destinations clip after the reverse correction. Neither rule clips ordinary HDR conversion.
            if (this.adjustFrom)
            {
                if (this.sourceLab)
                {
                    x = Vector128.Max(x, Vector128<float>.Zero);
                    y = Vector128.Max(y, Vector128<float>.Zero);
                    z = Vector128.Max(z, Vector128<float>.Zero);
                }

                x = (x * Vector128.Create(SourceBlackPointScaleX)) + Vector128.Create(SourceBlackPointOffsetX);
                y = (y * Vector128.Create(SourceBlackPointScaleY)) + Vector128.Create(SourceBlackPointOffsetY);
                z = (z * Vector128.Create(SourceBlackPointScaleZ)) + Vector128.Create(SourceBlackPointOffsetZ);
            }

            if (this.adjustTo)
            {
                x = (x * Vector128.Create(TargetBlackPointScaleX)) - Vector128.Create(TargetBlackPointOffsetX);
                y = (y * Vector128.Create(TargetBlackPointScaleY)) - Vector128.Create(TargetBlackPointOffsetY);
                z = (z * Vector128.Create(TargetBlackPointScaleZ)) - Vector128.Create(TargetBlackPointOffsetZ);
                if (!this.targetLab)
                {
                    x = Vector128.Max(x, Vector128<float>.Zero);
                    y = Vector128.Max(y, Vector128<float>.Zero);
                    z = Vector128.Max(z, Vector128<float>.Zero);
                }
            }

            if (this.targetLab)
            {
                CieXyz white = this.options.TargetWhitePoint;
                CieLab.FromProfileConnectionSpace(ref x, ref y, ref z, in white);
                x /= Vector128.Create(100F);
                y = (y + Vector128.Create(128F)) / Vector128.Create(255F);
                z = (z + Vector128.Create(128F)) / Vector128.Create(255F);
                if (this.targetV2)
                {
                    x *= Vector128.Create(LegacyLabEncodeScale);
                    y *= Vector128.Create(LegacyLabEncodeScale);
                    z *= Vector128.Create(LegacyLabEncodeScale);
                }
            }
            else
            {
                x *= Vector128.Create(PcsXyzEncodeScale);
                y *= Vector128.Create(PcsXyzEncodeScale);
                z *= Vector128.Create(PcsXyzEncodeScale);
            }

            w = this.targetLab && this.targetV2 ? Vector128.Create(LegacyLabEncodeScale) : Vector128<float>.One;
        }

        /// <summary>
        /// Connects the PCS components in registers, with one independent color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Matching PCS values are already encoded for the target, except for legacy Lab's scale.
            if (!this.adjustFrom && !this.adjustTo && this.sourceLab == this.targetLab)
            {
                if (this.sourceLab && this.sourceV2 != this.targetV2)
                {
                    Vector256<float> scale = Vector256.Create(this.sourceV2 ? LegacyLabDecodeScale : LegacyLabEncodeScale);
                    x *= scale;
                    y *= scale;
                    z *= scale;
                    w *= scale;
                }

                return;
            }

            if (this.sourceLab)
            {
                if (this.sourceV2)
                {
                    x *= Vector256.Create(LegacyLabDecodeScale);
                    y *= Vector256.Create(LegacyLabDecodeScale);
                    z *= Vector256.Create(LegacyLabDecodeScale);
                }

                x *= Vector256.Create(100F);
                y = (y * Vector256.Create(255F)) - Vector256.Create(128F);
                z = (z * Vector256.Create(255F)) - Vector256.Create(128F);
                CieXyz white = this.options.SourceWhitePoint;
                CieLab.ToProfileConnectionSpace(ref x, ref y, ref z, in white);
            }
            else
            {
                x *= Vector256.Create(PcsXyzDecodeScale);
                y *= Vector256.Create(PcsXyzDecodeScale);
                z *= Vector256.Create(PcsXyzDecodeScale);
            }

            // Keep ICC v2 black-point correction in XYZ. Lab sources clip negative XYZ before correction;
            // XYZ destinations clip after the reverse correction. Neither rule clips ordinary HDR conversion.
            if (this.adjustFrom)
            {
                if (this.sourceLab)
                {
                    x = Vector256.Max(x, Vector256<float>.Zero);
                    y = Vector256.Max(y, Vector256<float>.Zero);
                    z = Vector256.Max(z, Vector256<float>.Zero);
                }

                x = (x * Vector256.Create(SourceBlackPointScaleX)) + Vector256.Create(SourceBlackPointOffsetX);
                y = (y * Vector256.Create(SourceBlackPointScaleY)) + Vector256.Create(SourceBlackPointOffsetY);
                z = (z * Vector256.Create(SourceBlackPointScaleZ)) + Vector256.Create(SourceBlackPointOffsetZ);
            }

            if (this.adjustTo)
            {
                x = (x * Vector256.Create(TargetBlackPointScaleX)) - Vector256.Create(TargetBlackPointOffsetX);
                y = (y * Vector256.Create(TargetBlackPointScaleY)) - Vector256.Create(TargetBlackPointOffsetY);
                z = (z * Vector256.Create(TargetBlackPointScaleZ)) - Vector256.Create(TargetBlackPointOffsetZ);
                if (!this.targetLab)
                {
                    x = Vector256.Max(x, Vector256<float>.Zero);
                    y = Vector256.Max(y, Vector256<float>.Zero);
                    z = Vector256.Max(z, Vector256<float>.Zero);
                }
            }

            if (this.targetLab)
            {
                CieXyz white = this.options.TargetWhitePoint;
                CieLab.FromProfileConnectionSpace(ref x, ref y, ref z, in white);
                x /= Vector256.Create(100F);
                y = (y + Vector256.Create(128F)) / Vector256.Create(255F);
                z = (z + Vector256.Create(128F)) / Vector256.Create(255F);
                if (this.targetV2)
                {
                    x *= Vector256.Create(LegacyLabEncodeScale);
                    y *= Vector256.Create(LegacyLabEncodeScale);
                    z *= Vector256.Create(LegacyLabEncodeScale);
                }
            }
            else
            {
                x *= Vector256.Create(PcsXyzEncodeScale);
                y *= Vector256.Create(PcsXyzEncodeScale);
                z *= Vector256.Create(PcsXyzEncodeScale);
            }

            w = this.targetLab && this.targetV2 ? Vector256.Create(LegacyLabEncodeScale) : Vector256<float>.One;
        }

        /// <summary>
        /// Connects the PCS components in registers, with one independent color per lane.
        /// </summary>
        /// <param name="x">The first input and output components.</param>
        /// <param name="y">The second input and output components.</param>
        /// <param name="z">The third input and output components.</param>
        /// <param name="w">The fourth input and output components.</param>
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Matching PCS values are already encoded for the target, except for legacy Lab's scale.
            if (!this.adjustFrom && !this.adjustTo && this.sourceLab == this.targetLab)
            {
                if (this.sourceLab && this.sourceV2 != this.targetV2)
                {
                    Vector512<float> scale = Vector512.Create(this.sourceV2 ? LegacyLabDecodeScale : LegacyLabEncodeScale);
                    x *= scale;
                    y *= scale;
                    z *= scale;
                    w *= scale;
                }

                return;
            }

            if (this.sourceLab)
            {
                if (this.sourceV2)
                {
                    x *= Vector512.Create(LegacyLabDecodeScale);
                    y *= Vector512.Create(LegacyLabDecodeScale);
                    z *= Vector512.Create(LegacyLabDecodeScale);
                }

                x *= Vector512.Create(100F);
                y = (y * Vector512.Create(255F)) - Vector512.Create(128F);
                z = (z * Vector512.Create(255F)) - Vector512.Create(128F);
                CieXyz white = this.options.SourceWhitePoint;
                CieLab.ToProfileConnectionSpace(ref x, ref y, ref z, in white);
            }
            else
            {
                x *= Vector512.Create(PcsXyzDecodeScale);
                y *= Vector512.Create(PcsXyzDecodeScale);
                z *= Vector512.Create(PcsXyzDecodeScale);
            }

            // Keep ICC v2 black-point correction in XYZ. Lab sources clip negative XYZ before correction;
            // XYZ destinations clip after the reverse correction. Neither rule clips ordinary HDR conversion.
            if (this.adjustFrom)
            {
                if (this.sourceLab)
                {
                    x = Vector512.Max(x, Vector512<float>.Zero);
                    y = Vector512.Max(y, Vector512<float>.Zero);
                    z = Vector512.Max(z, Vector512<float>.Zero);
                }

                x = (x * Vector512.Create(SourceBlackPointScaleX)) + Vector512.Create(SourceBlackPointOffsetX);
                y = (y * Vector512.Create(SourceBlackPointScaleY)) + Vector512.Create(SourceBlackPointOffsetY);
                z = (z * Vector512.Create(SourceBlackPointScaleZ)) + Vector512.Create(SourceBlackPointOffsetZ);
            }

            if (this.adjustTo)
            {
                x = (x * Vector512.Create(TargetBlackPointScaleX)) - Vector512.Create(TargetBlackPointOffsetX);
                y = (y * Vector512.Create(TargetBlackPointScaleY)) - Vector512.Create(TargetBlackPointOffsetY);
                z = (z * Vector512.Create(TargetBlackPointScaleZ)) - Vector512.Create(TargetBlackPointOffsetZ);
                if (!this.targetLab)
                {
                    x = Vector512.Max(x, Vector512<float>.Zero);
                    y = Vector512.Max(y, Vector512<float>.Zero);
                    z = Vector512.Max(z, Vector512<float>.Zero);
                }
            }

            if (this.targetLab)
            {
                CieXyz white = this.options.TargetWhitePoint;
                CieLab.FromProfileConnectionSpace(ref x, ref y, ref z, in white);
                x /= Vector512.Create(100F);
                y = (y + Vector512.Create(128F)) / Vector512.Create(255F);
                z = (z + Vector512.Create(128F)) / Vector512.Create(255F);
                if (this.targetV2)
                {
                    x *= Vector512.Create(LegacyLabEncodeScale);
                    y *= Vector512.Create(LegacyLabEncodeScale);
                    z *= Vector512.Create(LegacyLabEncodeScale);
                }
            }
            else
            {
                x *= Vector512.Create(PcsXyzEncodeScale);
                y *= Vector512.Create(PcsXyzEncodeScale);
                z *= Vector512.Create(PcsXyzEncodeScale);
            }

            w = this.targetLab && this.targetV2 ? Vector512.Create(LegacyLabEncodeScale) : Vector512<float>.One;
        }
    }
}

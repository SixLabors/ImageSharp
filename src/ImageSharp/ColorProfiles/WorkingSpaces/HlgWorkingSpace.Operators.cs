// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <content>
/// Applies prepared HLG component and luminance operations to independent RGB lanes.
/// </content>
public sealed partial class HlgWorkingSpace
{
    /// <summary>
    /// Applies a prepared RGB operation without intermediate clamping. Each SIMD lane contains one independent color.
    /// </summary>
    private interface IColorOperator
    {
        /// <summary>
        /// Converts RGB components in place.
        /// </summary>
        /// <param name="red">The red components.</param>
        /// <param name="green">The green components.</param>
        /// <param name="blue">The blue components.</param>
        void Invoke(ref float red, ref float green, ref float blue);

        /// <summary>
        /// Converts RGB components in place.
        /// </summary>
        /// <param name="red">The red components.</param>
        /// <param name="green">The green components.</param>
        /// <param name="blue">The blue components.</param>
        void Invoke(ref Vector128<float> red, ref Vector128<float> green, ref Vector128<float> blue);

        /// <summary>
        /// Converts RGB components in place.
        /// </summary>
        /// <param name="red">The red components.</param>
        /// <param name="green">The green components.</param>
        /// <param name="blue">The blue components.</param>
        void Invoke(ref Vector256<float> red, ref Vector256<float> green, ref Vector256<float> blue);

        /// <summary>
        /// Converts RGB components in place.
        /// </summary>
        /// <param name="red">The red components.</param>
        /// <param name="green">The green components.</param>
        /// <param name="blue">The blue components.</param>
        void Invoke(ref Vector512<float> red, ref Vector512<float> green, ref Vector512<float> blue);
    }

    /// <summary>
    /// Expands HLG scene light before applying display luminance.
    /// </summary>
    private readonly struct ExpandOperator : IColorOperator
    {
        private readonly OotfOperator ootf;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpandOperator"/> struct.
        /// </summary>
        /// <param name="ootf">The prepared luminance adjustment.</param>
        public ExpandOperator(OotfOperator ootf) => this.ootf = ootf;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float red, ref float green, ref float blue)
        {
            HlgCompanding.ExpandOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);

            this.ootf.Invoke(ref red, ref green, ref blue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> red, ref Vector128<float> green, ref Vector128<float> blue)
        {
            HlgCompanding.ExpandOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);

            this.ootf.Invoke(ref red, ref green, ref blue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> red, ref Vector256<float> green, ref Vector256<float> blue)
        {
            HlgCompanding.ExpandOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);

            this.ootf.Invoke(ref red, ref green, ref blue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> red, ref Vector512<float> green, ref Vector512<float> blue)
        {
            HlgCompanding.ExpandOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);

            this.ootf.Invoke(ref red, ref green, ref blue);
        }
    }

    /// <summary>
    /// Restores scene light before applying the HLG component curve.
    /// </summary>
    private readonly struct CompressOperator : IColorOperator
    {
        private readonly OotfOperator ootf;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressOperator"/> struct.
        /// </summary>
        /// <param name="ootf">The prepared luminance adjustment.</param>
        public CompressOperator(OotfOperator ootf) => this.ootf = ootf;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float red, ref float green, ref float blue)
        {
            this.ootf.Invoke(ref red, ref green, ref blue);

            HlgCompanding.CompressOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> red, ref Vector128<float> green, ref Vector128<float> blue)
        {
            this.ootf.Invoke(ref red, ref green, ref blue);

            HlgCompanding.CompressOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> red, ref Vector256<float> green, ref Vector256<float> blue)
        {
            this.ootf.Invoke(ref red, ref green, ref blue);

            HlgCompanding.CompressOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> red, ref Vector512<float> green, ref Vector512<float> blue)
        {
            this.ootf.Invoke(ref red, ref green, ref blue);

            HlgCompanding.CompressOperator curve = default;
            red = curve.Invoke(red);
            green = curve.Invoke(green);
            blue = curve.Invoke(blue);
        }
    }

    /// <summary>
    /// Applies the HLG opto-optical luminance adjustment with prepared display gamma and primary weights.
    /// </summary>
    private readonly struct OotfOperator : IColorOperator
    {
        /// <summary>
        /// The largest gamma distance from one that retains identity luminance adjustment.
        /// </summary>
        private const float IdentityGammaTolerance = 0.01F;

        /// <summary>
        /// The maximum RGB scale used by the luminance adjustment.
        /// </summary>
        private const float MaximumLuminanceRatio = 1E9F;

        private readonly TensorPrimitives_.PowOperator power;
        private readonly Vector3 luminance;
        private readonly bool apply;

        /// <summary>
        /// Initializes a new instance of the <see cref="OotfOperator"/> struct.
        /// </summary>
        /// <param name="gamma">The display or inverse display system gamma.</param>
        /// <param name="luminance">The Y coefficients for the working-space RGB primaries.</param>
        public OotfOperator(float gamma, Vector3 luminance)
        {
            float exponent = gamma - 1F;
            this.power = new(exponent);
            this.luminance = luminance;

            // The shared contract retains the identity tolerance and scale limit used by libjxl HlgOOTF_Base.
            // Prepare this decision once; every RGB layout then applies the same luminance policy.
            this.apply = MathF.Abs(exponent) > IdentityGammaTolerance;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float red, ref float green, ref float blue)
        {
            if (!this.apply)
            {
                return;
            }

            // Accumulate B, G, R in this order. The physical luminance, not its magnitude, controls the shared RGB scale.
            float light = this.luminance.Z * blue;
            light = float.MultiplyAddEstimate(this.luminance.Y, green, light);
            light = float.MultiplyAddEstimate(this.luminance.X, red, light);
            float ratio = MathF.Min(this.power.Invoke(light), MaximumLuminanceRatio);
            red *= ratio;
            green *= ratio;
            blue *= ratio;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> red, ref Vector128<float> green, ref Vector128<float> blue)
        {
            if (!this.apply)
            {
                return;
            }

            // Accumulate B, G, R in this order. The physical luminance, not its magnitude, controls the shared RGB scale.
            Vector128<float> light = Vector128.Multiply(Vector128.Create(this.luminance.Z), blue);
            light = Vector128.MultiplyAddEstimate(Vector128.Create(this.luminance.Y), green, light);
            light = Vector128.MultiplyAddEstimate(Vector128.Create(this.luminance.X), red, light);
            Vector128<float> ratio = Vector128.Min(this.power.Invoke(light), Vector128.Create(MaximumLuminanceRatio));
            red = Vector128.Multiply(red, ratio);
            green = Vector128.Multiply(green, ratio);
            blue = Vector128.Multiply(blue, ratio);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> red, ref Vector256<float> green, ref Vector256<float> blue)
        {
            if (!this.apply)
            {
                return;
            }

            // Accumulate B, G, R in this order. The physical luminance, not its magnitude, controls the shared RGB scale.
            Vector256<float> light = Vector256.Multiply(Vector256.Create(this.luminance.Z), blue);
            light = Vector256.MultiplyAddEstimate(Vector256.Create(this.luminance.Y), green, light);
            light = Vector256.MultiplyAddEstimate(Vector256.Create(this.luminance.X), red, light);
            Vector256<float> ratio = Vector256.Min(this.power.Invoke(light), Vector256.Create(MaximumLuminanceRatio));
            red = Vector256.Multiply(red, ratio);
            green = Vector256.Multiply(green, ratio);
            blue = Vector256.Multiply(blue, ratio);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> red, ref Vector512<float> green, ref Vector512<float> blue)
        {
            if (!this.apply)
            {
                return;
            }

            // Accumulate B, G, R in this order. The physical luminance, not its magnitude, controls the shared RGB scale.
            Vector512<float> light = Vector512.Multiply(Vector512.Create(this.luminance.Z), blue);
            light = Vector512.MultiplyAddEstimate(Vector512.Create(this.luminance.Y), green, light);
            light = Vector512.MultiplyAddEstimate(Vector512.Create(this.luminance.X), red, light);
            Vector512<float> ratio = Vector512.Min(this.power.Invoke(light), Vector512.Create(MaximumLuminanceRatio));
            red = Vector512.Multiply(red, ratio);
            green = Vector512.Multiply(green, ratio);
            blue = Vector512.Multiply(blue, ratio);
        }
    }
}

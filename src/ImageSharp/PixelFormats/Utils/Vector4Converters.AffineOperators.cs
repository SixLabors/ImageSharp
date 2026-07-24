// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

internal static partial class Vector4Converters
{
    /// <summary>
    /// Defines a stateful component transform for each register width used by the shared traversal.
    /// </summary>
    private interface IStatefulVector4Operator
    {
        /// <summary>
        /// Transforms one pixel represented by four components.
        /// </summary>
        /// <param name="source">The source components.</param>
        /// <returns>The transformed components.</returns>
        public Vector4 Invoke(Vector4 source);

        /// <summary>
        /// Transforms one pixel represented by the four single-precision lanes in a 128-bit register.
        /// </summary>
        /// <param name="source">The source components.</param>
        /// <returns>The transformed components.</returns>
        public Vector128<float> Invoke(Vector128<float> source);

        /// <summary>
        /// Transforms two pixels represented by the eight single-precision lanes in a 256-bit register.
        /// </summary>
        /// <param name="source">The source components.</param>
        /// <returns>The transformed components.</returns>
        public Vector256<float> Invoke(Vector256<float> source);

        /// <summary>
        /// Transforms four pixels represented by the sixteen single-precision lanes in a 512-bit register.
        /// </summary>
        /// <param name="source">The source components.</param>
        /// <returns>The transformed components.</returns>
        public Vector512<float> Invoke(Vector512<float> source);
    }

    /// <summary>
    /// Carries the component state for a multiply-then-add transform.
    /// </summary>
    private readonly struct MultiplyThenAddOperator : IStatefulVector4Operator
    {
        private readonly Vector512<float> multiplier;
        private readonly Vector512<float> offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiplyThenAddOperator"/> struct.
        /// </summary>
        /// <param name="multiplier">The component-wise multiplier.</param>
        /// <param name="offset">The component-wise offset applied after multiplication.</param>
        public MultiplyThenAddOperator(Vector4 multiplier, Vector4 offset)
        {
            Vector128<float> multiplier128 = multiplier.AsVector128();
            Vector128<float> offset128 = offset.AsVector128();
            Vector256<float> multiplier256 = Vector256.Create(multiplier128, multiplier128);
            Vector256<float> offset256 = Vector256.Create(offset128, offset128);

            // Expanding the invariant state once prevents the width-specific Invoke methods
            // from rebuilding identical lane groups for every vector processed by the loop.
            this.multiplier = Vector512.Create(multiplier256, multiplier256);
            this.offset = Vector512.Create(offset256, offset256);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Invoke(Vector4 source)
        {
            Vector128<float> result =
                (source.AsVector128() * this.multiplier.GetLower().GetLower())
                + this.offset.GetLower().GetLower();

            return result.AsVector4();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> source)
            => (source * this.multiplier.GetLower().GetLower()) + this.offset.GetLower().GetLower();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> source)
            => (source * this.multiplier.GetLower()) + this.offset.GetLower();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> source)
            => (source * this.multiplier) + this.offset;
    }

    /// <summary>
    /// Carries the component state for an add-then-divide transform.
    /// </summary>
    private readonly struct AddThenDivideOperator : IStatefulVector4Operator
    {
        private readonly Vector512<float> offset;
        private readonly Vector512<float> divisor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddThenDivideOperator"/> struct.
        /// </summary>
        /// <param name="offset">The component-wise offset applied before division.</param>
        /// <param name="divisor">The component-wise divisor.</param>
        public AddThenDivideOperator(Vector4 offset, Vector4 divisor)
        {
            Vector128<float> offset128 = offset.AsVector128();
            Vector128<float> divisor128 = divisor.AsVector128();
            Vector256<float> offset256 = Vector256.Create(offset128, offset128);
            Vector256<float> divisor256 = Vector256.Create(divisor128, divisor128);

            // All register widths consume prefixes of this repeated four-pixel state,
            // so one construction serves the wide loop and every narrower remainder.
            this.offset = Vector512.Create(offset256, offset256);
            this.divisor = Vector512.Create(divisor256, divisor256);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Invoke(Vector4 source)
        {
            Vector128<float> result =
                (source.AsVector128() + this.offset.GetLower().GetLower())
                / this.divisor.GetLower().GetLower();

            return result.AsVector4();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> source)
            => (source + this.offset.GetLower().GetLower()) / this.divisor.GetLower().GetLower();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> source)
            => (source + this.offset.GetLower()) / this.divisor.GetLower();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> source)
            => (source + this.offset) / this.divisor;
    }
}

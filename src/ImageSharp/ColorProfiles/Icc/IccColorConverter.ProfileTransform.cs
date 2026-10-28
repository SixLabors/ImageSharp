// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Binds a shared source stage to the existing connection operator in Native AOT.
    /// </summary>
    internal readonly struct ProfileTransformOperator : IIccColorConverterOperator
    {
        // The reference has the same layout for every source profile. Native AOT therefore needs only one
        // source binding per target traversal. The object owns the selected operator, not pixel storage.
        private readonly ProfileTransform source;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileTransformOperator"/> struct.
        /// </summary>
        /// <param name="source">The configured source profile stage.</param>
        public ProfileTransformOperator(ProfileTransform source) => this.source = source;

        /// <inheritdoc/>
        public bool IsTrc => false;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value) => this.source.Convert(value);

        /// <inheritdoc/>
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
            => this.source.Convert(ref x, ref y, ref z, ref w);

        /// <inheritdoc/>
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
            => this.source.Convert(ref x, ref y, ref z, ref w);

        /// <inheritdoc/>
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
            => this.source.Convert(ref x, ref y, ref z, ref w);
    }

    /// <summary>
    /// Applies the configured arithmetic for one device-to-PCS profile stage.
    /// </summary>
    internal abstract class ProfileTransform
    {
        /// <summary>
        /// Converts one color in the component units defined by the selected profile stage.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public abstract Vector4 Convert(Vector4 value);

        /// <summary>
        /// Converts four colors. Each register contains one component from each color.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        public abstract void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w);

        /// <summary>
        /// Converts eight colors. Each register contains one component from each color.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        public abstract void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w);

        /// <summary>
        /// Converts sixteen colors. Each register contains one component from each color.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        public abstract void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w);
    }

    /// <summary>
    /// Retains one selected profile operator for the shared connection.
    /// </summary>
    /// <typeparam name="TOperator">The configured profile operator.</typeparam>
    internal sealed class ProfileTransform<TOperator> : ProfileTransform
        where TOperator : struct, IIccColorConverterOperator
    {
        // Each profile stage compiles independently of its connection partner. The base class provides fixed
        // virtual slots, so block conversion does not need an interface lookup or an unboxing stub.
        // Keep the field writable: constrained calls must not copy a large operator for each component block.
        private TOperator colorOperator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileTransform{TOperator}"/> class.
        /// </summary>
        /// <param name="colorOperator">The configured profile arithmetic.</param>
        public ProfileTransform(TOperator colorOperator) => this.colorOperator = colorOperator;

        /// <inheritdoc/>
        public override Vector4 Convert(Vector4 value) => this.colorOperator.Convert(value);

        /// <inheritdoc/>
        public override void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
            => this.colorOperator.Convert(ref x, ref y, ref z, ref w);

        /// <inheritdoc/>
        public override void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
            => this.colorOperator.Convert(ref x, ref y, ref z, ref w);

        /// <inheritdoc/>
        public override void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
            => this.colorOperator.Convert(ref x, ref y, ref z, ref w);
    }
}

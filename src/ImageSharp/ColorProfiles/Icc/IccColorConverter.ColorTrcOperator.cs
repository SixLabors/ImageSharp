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
    /// Converts RGB through the profile's channel curves and XYZ matrix.
    /// </summary>
    internal struct ColorTrcOperator : IIccColorConverterOperator
    {
        // Keep the generic operation writable so delegation does not copy its channel curves on each SIMD block.
        private ColorTrcOperator<TrcOperator> operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorTrcOperator"/> struct.
        /// </summary>
        /// <param name="red">The red XYZ matrix column.</param>
        /// <param name="green">The green XYZ matrix column.</param>
        /// <param name="blue">The blue XYZ matrix column.</param>
        /// <param name="curves">The red, green and blue channel curves.</param>
        /// <param name="toPcs">Whether to convert device RGB to encoded PCS XYZ.</param>
        public ColorTrcOperator(Vector3 red, Vector3 green, Vector3 blue, ReadOnlySpan<IccTagDataEntry> curves, bool toPcs)
        {
            TrcOperator trc = new(curves, !toPcs);

            // Each row stores one RGB primary's XYZ coefficients. Row-vector multiplication combines the three primaries.
            Matrix4x4 matrix = new(red.X, red.Y, red.Z, 0F, green.X, green.Y, green.Z, 0F, blue.X, blue.Y, blue.Z, 0F, 0F, 0F, 0F, 1F);

            if (!toPcs)
            {
                Matrix4x4.Invert(matrix, out matrix);
            }

            this.operation = new(new MatrixOperator(matrix, Vector3.Zero), trc, toPcs);
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <summary>
        /// Connects two matrix/TRC profiles and combines adjacent linear stages before traversal.
        /// </summary>
        /// <param name="target">The selected PCS-to-device operator.</param>
        /// <param name="pcs">The connection's XYZ adjustment and clipping rules.</param>
        /// <returns>The complete connection using the shared SIMD traversal.</returns>
        public IIccColorConverter Connect(ColorTrcOperator target, PcsOperator pcs)
        {
            MatrixOperator adjustment = pcs.GetXyzAdjustment(out bool clipNegativeXyz);
            MatrixOperator sourceMatrix = this.operation.Matrix.Then(adjustment);
            TrcOperator curves = this.operation.Curves;

            // Select the curve formulas before traversal. Closed stages avoid the general curve-kind switch in every component block.
            if (curves.TryGetGamma(out GammaTrcOperator sourceGamma))
            {
                return CreateConnection(sourceGamma, target, sourceMatrix, clipNegativeXyz);
            }

            return curves.TryGetSRgb(out SRgbTrcOperator sourceSRgb)
                ? CreateConnection(sourceSRgb, target, sourceMatrix, clipNegativeXyz)
                : CreateConnection(curves, target, sourceMatrix, clipNegativeXyz);
        }

        /// <summary>
        /// Selects the target curve formula before constructing the closed connection.
        /// </summary>
        /// <typeparam name="TSource">The selected source curve operator.</typeparam>
        /// <param name="source">The source curves.</param>
        /// <param name="target">The target matrix and channel curves.</param>
        /// <param name="sourceMatrix">The source matrix with its XYZ adjustment.</param>
        /// <param name="clipNegativeXyz">Whether the PCS rule clips negative XYZ between the matrices.</param>
        /// <returns>The connection with its target curve formula selected.</returns>
        private static IIccColorConverter CreateConnection<TSource>(
            TSource source,
            ColorTrcOperator target,
            MatrixOperator sourceMatrix,
            bool clipNegativeXyz)
            where TSource : struct, IIccColorConverterOperator
        {
            TrcOperator curves = target.operation.Curves;
            if (curves.TryGetGamma(out GammaTrcOperator gamma))
            {
                return CreateConnection(source, gamma, sourceMatrix, target.operation.Matrix, clipNegativeXyz);
            }

            return curves.TryGetSRgb(out SRgbTrcOperator srgb)
                ? CreateConnection(source, srgb, sourceMatrix, target.operation.Matrix, clipNegativeXyz)
                : CreateConnection(source, curves, sourceMatrix, target.operation.Matrix, clipNegativeXyz);
        }

        /// <summary>
        /// Connects an arbitrary source profile to this profile with the target curve formula selected once.
        /// </summary>
        /// <typeparam name="TSource">The source profile operator.</typeparam>
        /// <param name="source">The configured source profile transform.</param>
        /// <param name="pcs">The connection's PCS adjustment.</param>
        /// <returns>The closed source, PCS and target transform.</returns>
        public IIccColorConverter ConnectFrom<TSource>(TSource source, PcsOperator pcs)
            where TSource : struct, IIccColorConverterOperator
        {
            TrcOperator curves = this.operation.Curves;
            if (curves.TryGetGamma(out GammaTrcOperator gamma))
            {
                return this.CreateTargetConnection(source, pcs, gamma);
            }

            return curves.TryGetSRgb(out SRgbTrcOperator srgb)
                ? this.CreateTargetConnection(source, pcs, srgb)
                : this.CreateTargetConnection(source, pcs, curves);
        }

        /// <summary>
        /// Builds the target profile stage with the selected channel curves and the profile matrix and PCS encoding.
        /// </summary>
        /// <typeparam name="TSource">The source profile operator.</typeparam>
        /// <typeparam name="TCurves">The type that implements the target channel curves.</typeparam>
        /// <param name="source">The configured source transform.</param>
        /// <param name="pcs">The PCS adjustment.</param>
        /// <param name="curves">The configured target channel curves.</param>
        /// <returns>The complete connection with a closed target curve type.</returns>
        private IccColorConverter<ConnectionOperator<TSource, ColorTrcOperator<TCurves>>> CreateTargetConnection<TSource, TCurves>(
            TSource source,
            PcsOperator pcs,
            TCurves curves)
            where TSource : struct, IIccColorConverterOperator
            where TCurves : struct, IIccColorConverterOperator
            => new(new(source, pcs, new(this.operation.Matrix, curves, this.operation.ToPcs)));

        /// <summary>
        /// Creates the closed connection after selection of the source and target channel-curve operators.
        /// </summary>
        /// <typeparam name="TSource">The source channel-curve operator.</typeparam>
        /// <typeparam name="TTarget">The target channel-curve operator.</typeparam>
        /// <param name="source">The source curves.</param>
        /// <param name="target">The target curves.</param>
        /// <param name="sourceMatrix">The source matrix with its XYZ adjustment.</param>
        /// <param name="targetMatrix">The target matrix.</param>
        /// <param name="clipNegativeXyz">Whether the PCS adjustment clips negative XYZ before the target matrix.</param>
        /// <returns>The connection using the shared SIMD traversal.</returns>
        private static IccColorConverter<ColorTrcConnectionOperator<TSource, TTarget>> CreateConnection<TSource, TTarget>(
            TSource source,
            TTarget target,
            MatrixOperator sourceMatrix,
            MatrixOperator targetMatrix,
            bool clipNegativeXyz)
            where TSource : struct, IIccColorConverterOperator
            where TTarget : struct, IIccColorConverterOperator
            => new(new(source, sourceMatrix, targetMatrix, target, clipNegativeXyz));

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value) => this.operation.Convert(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
            => this.operation.Convert(ref x, ref y, ref z, ref w);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
            => this.operation.Convert(ref x, ref y, ref z, ref w);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
            => this.operation.Convert(ref x, ref y, ref z, ref w);
    }
}

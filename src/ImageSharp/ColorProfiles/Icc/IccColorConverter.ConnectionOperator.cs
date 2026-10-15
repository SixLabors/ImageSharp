// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Keeps source, PCS and target stages in component registers throughout each color block.
    /// </summary>
    /// <typeparam name="TSource">The source profile operator.</typeparam>
    /// <typeparam name="TTarget">The target profile operator.</typeparam>
    internal struct ConnectionOperator<TSource, TTarget> : IIccColorConverterOperator
        where TSource : struct, IIccColorConverterOperator
        where TTarget : struct, IIccColorConverterOperator
    {
        // Generic interface calls on readonly fields copy the complete operator. Keep these private fields writable
        // so constrained calls use their addresses. Conversion only reads the configured profile state.
        private TSource source;
        private TTarget target;
        private readonly PcsOperator pcs;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionOperator{TSource, TTarget}"/> struct.
        /// </summary>
        /// <param name="source">The configured device-to-PCS operator.</param>
        /// <param name="pcs">The connection's PCS encoding and adjustment rules.</param>
        /// <param name="target">The configured PCS-to-device operator.</param>
        public ConnectionOperator(TSource source, PcsOperator pcs, TTarget target)
        {
            this.source = source;
            this.pcs = pcs;
            this.target = target;
        }

        /// <inheritdoc/>
        public bool IsTrc => false;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value) => this.target.Convert(this.pcs.Convert(this.source.Convert(value)));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            this.source.Convert(ref x, ref y, ref z, ref w);
            this.pcs.Convert(ref x, ref y, ref z, ref w);
            this.target.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            this.source.Convert(ref x, ref y, ref z, ref w);
            this.pcs.Convert(ref x, ref y, ref z, ref w);
            this.target.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            this.source.Convert(ref x, ref y, ref z, ref w);
            this.pcs.Convert(ref x, ref y, ref z, ref w);
            this.target.Convert(ref x, ref y, ref z, ref w);
        }
    }
}

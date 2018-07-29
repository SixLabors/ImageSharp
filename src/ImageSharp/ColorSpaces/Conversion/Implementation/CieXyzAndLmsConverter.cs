// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
{
    /// <summary>
    /// Color converter between CIE XYZ and LMS
    /// </summary>
    internal sealed class CieXyzAndLmsConverter : IColorConversion<CieXyz, Lms>, IColorConversion<Lms, CieXyz>
    {
        /// <summary>
        /// Default transformation matrix used, when no other is set. (Bradford)
        /// <see cref="LmsAdaptationMatrix"/>
        /// </summary>
        public static readonly Matrix4x4 DefaultTransformationMatrix = LmsAdaptationMatrix.Bradford;

        private Matrix4x4 inverseTransformationMatrix;
        private Matrix4x4 transformationMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzAndLmsConverter"/> class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyzAndLmsConverter()
            : this(DefaultTransformationMatrix)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzAndLmsConverter"/> class.
        /// </summary>
        /// <param name="transformationMatrix">
        /// Definition of the cone response domain (see <see cref="LmsAdaptationMatrix"/>),
        /// if not set <see cref="DefaultTransformationMatrix"/> will be used.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyzAndLmsConverter(Matrix4x4 transformationMatrix)
        {
            this.transformationMatrix = transformationMatrix;
            Matrix4x4.Invert(this.transformationMatrix, out this.inverseTransformationMatrix);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Lms Convert(in CieXyz input)
        {
            var vector = Vector3.Transform(input.ToVector3(), this.transformationMatrix);

            return new Lms(vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyz Convert(in Lms input)
        {
            var vector = Vector3.Transform(input.ToVector3(), this.inverseTransformationMatrix);

            return new CieXyz(vector);
        }
    }
}
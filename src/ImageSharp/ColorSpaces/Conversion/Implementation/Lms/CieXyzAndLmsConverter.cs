// <copyright file="CieXyzAndLmsConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Lms
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using ImageSharp.ColorSpaces;

    /// <summary>
    /// Color converter between CIE XYZ and LMS
    /// </summary>
    internal class CieXyzAndLmsConverter : IColorConversion<CieXyz, Lms>, IColorConversion<Lms, CieXyz>
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
            this.TransformationMatrix = transformationMatrix;
        }

        /// <summary>
        /// Gets or sets the transformation matrix used for the conversion (definition of the cone response domain).
        /// <see cref="LmsAdaptationMatrix"/>
        /// </summary>
        public Matrix4x4 TransformationMatrix
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.transformationMatrix;

            set
            {
                this.transformationMatrix = value;
                Matrix4x4.Invert(this.transformationMatrix, out this.inverseTransformationMatrix);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Lms Convert(CieXyz input)
        {
            DebugGuard.NotNull(input, nameof(input));

            Vector3 vector = Vector3.Transform(input.Vector, this.transformationMatrix);
            return new Lms(vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyz Convert(Lms input)
        {
            DebugGuard.NotNull(input, nameof(input));

            Vector3 vector = Vector3.Transform(input.Vector, this.inverseTransformationMatrix);
            return new CieXyz(vector);
        }
    }
}
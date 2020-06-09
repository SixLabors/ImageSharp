// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Color converter between <see cref="CieXyz"/> and <see cref="Lms"/>
    /// </summary>
    internal sealed class CieXyzAndLmsConverter
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
        public CieXyzAndLmsConverter(Matrix4x4 transformationMatrix)
        {
            this.transformationMatrix = transformationMatrix;
            Matrix4x4.Invert(this.transformationMatrix, out this.inverseTransformationMatrix);
        }

        /// <summary>
        /// Performs the conversion from the <see cref="CieXyz"/> input to an instance of <see cref="Lms"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Lms Convert(in CieXyz input)
        {
            var vector = Vector3.Transform(input.ToVector3(), this.transformationMatrix);

            return new Lms(vector);
        }

        /// <summary>
        /// Performs the conversion from the <see cref="Lms"/> input to an instance of <see cref="CieXyz"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieXyz Convert(in Lms input)
        {
            var vector = Vector3.Transform(input.ToVector3(), this.inverseTransformationMatrix);

            return new CieXyz(vector);
        }
    }
}
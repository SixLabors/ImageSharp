// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
{
    /// <summary>
    /// Color converter between <see cref="CieXyz"/> and <see cref="LinearRgb"/>
    /// </summary>
    internal sealed class CieXyzToLinearRgbConverter : LinearRgbAndCieXyzConverterBase
    {
        private readonly Matrix4x4 conversionMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToLinearRgbConverter"/> class.
        /// </summary>
        public CieXyzToLinearRgbConverter()
            : this(Rgb.DefaultWorkingSpace)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToLinearRgbConverter"/> class.
        /// </summary>
        /// <param name="workingSpace">The target working space.</param>
        public CieXyzToLinearRgbConverter(RgbWorkingSpaceBase workingSpace)
        {
            this.TargetWorkingSpace = workingSpace;
            this.conversionMatrix = GetRgbToCieXyzMatrix(workingSpace);
        }

        /// <summary>
        /// Gets the target working space
        /// </summary>
        public RgbWorkingSpaceBase TargetWorkingSpace { get; }

        /// <summary>
        /// Performs the conversion from the <see cref="CieXyz"/> input to an instance of <see cref="LinearRgb"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public LinearRgb Convert(in CieXyz input)
        {
            Matrix4x4.Invert(this.conversionMatrix, out Matrix4x4 inverted);
            var vector = Vector3.Transform(input.ToVector3(), inverted);
            return new LinearRgb(vector, this.TargetWorkingSpace);
        }
    }
}
// <copyright file="LinearRgbToRgbConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    using System.Numerics;

    using Rgb = ImageSharp.ColorSpaces.Rgb;

    /// <summary>
    /// Color converter between CieXyz and LinearRgb
    /// </summary>
    internal class CieXyzToLinearRgbConverter : LinearRgbAndCieXyzConverterBase, IColorConversion<CieXyz, LinearRgb>
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
        public CieXyzToLinearRgbConverter(IRgbWorkingSpace workingSpace)
        {
            this.TargetWorkingSpace = workingSpace;
            this.conversionMatrix = GetRgbToCieXyzMatrix(workingSpace);
        }

        /// <summary>
        /// Gets the target working space
        /// </summary>
        public IRgbWorkingSpace TargetWorkingSpace { get; }

        /// <inheritdoc/>
        public LinearRgb Convert(CieXyz input)
        {
            DebugGuard.NotNull(input, nameof(input));

            Matrix4x4.Invert(this.conversionMatrix, out Matrix4x4 inverted);
            Vector3 vector = Vector3.Transform(input.Vector, inverted);
            return new LinearRgb(vector, this.TargetWorkingSpace);
        }
    }
}
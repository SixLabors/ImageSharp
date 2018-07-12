// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Color converter between CieXyz and LinearRgb
    /// </summary>
    internal sealed class CieXyzToLinearRgbConverter : LinearRgbAndCieXyzConverterBase, IColorConversion<CieXyz, LinearRgb>
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
        public CieXyzToLinearRgbConverter(RgbWorkingSpace workingSpace)
        {
            this.TargetWorkingSpace = workingSpace;
            this.conversionMatrix = GetRgbToCieXyzMatrix(workingSpace);
        }

        /// <summary>
        /// Gets the target working space
        /// </summary>
        public RgbWorkingSpace TargetWorkingSpace { get; }

        /// <inheritdoc/>
        public LinearRgb Convert(in CieXyz input)
        {
            Matrix4x4.Invert(this.conversionMatrix, out Matrix4x4 inverted);
            Vector3 vector = Vector3.Transform(input.Vector, inverted);
            return new LinearRgb(vector, this.TargetWorkingSpace);
        }
    }
}
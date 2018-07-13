// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Color converter between LinearRgb and CieXyz
    /// </summary>
    internal sealed class LinearRgbToCieXyzConverter : LinearRgbAndCieXyzConverterBase, IColorConversion<LinearRgb, CieXyz>
    {
        private readonly Matrix4x4 conversionMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRgbToCieXyzConverter"/> class.
        /// </summary>
        public LinearRgbToCieXyzConverter()
            : this(Rgb.DefaultWorkingSpace)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRgbToCieXyzConverter"/> class.
        /// </summary>
        /// <param name="workingSpace">The target working space.</param>
        public LinearRgbToCieXyzConverter(RgbWorkingSpace workingSpace)
        {
            this.SourceWorkingSpace = workingSpace;
            this.conversionMatrix = GetRgbToCieXyzMatrix(workingSpace);
        }

        /// <summary>
        /// Gets the source working space
        /// </summary>
        public RgbWorkingSpace SourceWorkingSpace { get; }

        /// <inheritdoc/>
        public CieXyz Convert(in LinearRgb input)
        {
            DebugGuard.IsTrue(input.WorkingSpace.Equals(this.SourceWorkingSpace), nameof(input.WorkingSpace), "Input and source working spaces must be equal.");

            Vector3 vector = Vector3.Transform(input.Vector, this.conversionMatrix);
            return new CieXyz(vector);
        }
    }
}
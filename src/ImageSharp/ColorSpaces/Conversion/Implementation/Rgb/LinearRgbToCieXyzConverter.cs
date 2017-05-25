// <copyright file="LinearRgbToCieXyzConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    using System.Numerics;

    using Rgb = ImageSharp.ColorSpaces.Rgb;

    /// <summary>
    /// Color converter between LinearRgb and CieXyz
    /// </summary>
    internal class LinearRgbToCieXyzConverter : LinearRgbAndCieXyzConverterBase, IColorConversion<LinearRgb, CieXyz>
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
        public LinearRgbToCieXyzConverter(IRgbWorkingSpace workingSpace)
        {
            this.SourceWorkingSpace = workingSpace;
            this.conversionMatrix = GetRgbToCieXyzMatrix(workingSpace);
        }

        /// <summary>
        /// Gets the source working space
        /// </summary>
        public IRgbWorkingSpace SourceWorkingSpace { get; }

        /// <inheritdoc/>
        public CieXyz Convert(LinearRgb input)
        {
            DebugGuard.NotNull(input, nameof(input));
            Guard.IsTrue(input.WorkingSpace.Equals(this.SourceWorkingSpace), nameof(input.WorkingSpace), "Input and source working spaces must be equal.");

            Vector3 vector = Vector3.Transform(input.Vector, this.conversionMatrix);
            return new CieXyz(vector);
        }
    }
}
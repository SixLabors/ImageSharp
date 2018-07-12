// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyzToLinearRgbConverter()
            : this(Rgb.DefaultWorkingSpace)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToLinearRgbConverter"/> class.
        /// </summary>
        /// <param name="workingSpace">The target working space.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LinearRgb Convert(in CieXyz input)
        {
            Matrix4x4.Invert(this.conversionMatrix, out Matrix4x4 inverted);
            var vector = Vector3.Transform(input.ToVector3(), inverted);
            return new LinearRgb(vector, this.TargetWorkingSpace);
        }
    }
}
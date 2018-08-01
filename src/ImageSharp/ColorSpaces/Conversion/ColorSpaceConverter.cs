// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Provides methods to allow the conversion of color values between different color spaces.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        // Options.
        private CieXyz whitePoint;
        private CieXyz targetLuvWhitePoint;
        private CieXyz targetLabWhitePoint;
        private CieXyz targetHunterLabWhitePoint;
        private RgbWorkingSpace targetRgbWorkingSpace;
        private IChromaticAdaptation chromaticAdaptation;
        private bool performChromaticAdaptation;
        private bool performLabChromaticAdaptation;
        private Matrix4x4 lmsAdaptationMatrix;

        private CieXyzAndLmsConverter cieXyzAndLmsConverter;
        private CieXyzToCieLabConverter cieXyzToCieLabConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorSpaceConverter"/> class.
        /// </summary>
        public ColorSpaceConverter()
          : this(new ColorSpaceConverterOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorSpaceConverter"/> class.
        /// </summary>
        /// <param name="options">The configuration options.</param>
        public ColorSpaceConverter(ColorSpaceConverterOptions options)
        {
            Guard.NotNull(options, nameof(options));
            this.whitePoint = options.WhitePoint;
            this.targetLuvWhitePoint = options.TargetLuvWhitePoint;
            this.targetLabWhitePoint = options.TargetLabWhitePoint;
            this.targetHunterLabWhitePoint = options.TargetHunterLabWhitePoint;
            this.targetRgbWorkingSpace = options.TargetRgbWorkingSpace;
            this.chromaticAdaptation = options.ChromaticAdaptation;
            this.performChromaticAdaptation = this.chromaticAdaptation != null;
            this.performLabChromaticAdaptation = !this.whitePoint.Equals(this.targetLabWhitePoint) && this.performChromaticAdaptation;
            this.lmsAdaptationMatrix = options.LmsAdaptationMatrix;

            this.cieXyzAndLmsConverter = new CieXyzAndLmsConverter(this.lmsAdaptationMatrix);
            this.cieXyzToCieLabConverter = new CieXyzToCieLabConverter(this.targetLabWhitePoint);
        }
    }
}
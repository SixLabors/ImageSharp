// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Provides methods to allow the conversion of color values between different color spaces.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        // Options.
        private static readonly ColorSpaceConverterOptions DefaultOptions = new ColorSpaceConverterOptions();
        private readonly Matrix4x4 lmsAdaptationMatrix;
        private readonly CieXyz whitePoint;
        private readonly CieXyz targetLuvWhitePoint;
        private readonly CieXyz targetLabWhitePoint;
        private readonly CieXyz targetHunterLabWhitePoint;
        private readonly RgbWorkingSpace targetRgbWorkingSpace;
        private readonly IChromaticAdaptation chromaticAdaptation;
        private readonly bool performChromaticAdaptation;
        private readonly CieXyzAndLmsConverter cieXyzAndLmsConverter;
        private readonly CieXyzToCieLabConverter cieXyzToCieLabConverter;
        private readonly CieXyzToCieLuvConverter cieXyzToCieLuvConverter;
        private readonly CieXyzToHunterLabConverter cieXyzToHunterLabConverter;
        private readonly CieXyzToLinearRgbConverter cieXyzToLinearRgbConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorSpaceConverter"/> class.
        /// </summary>
        public ColorSpaceConverter()
          : this(DefaultOptions)
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
            this.lmsAdaptationMatrix = options.LmsAdaptationMatrix;

            this.cieXyzAndLmsConverter = new CieXyzAndLmsConverter(this.lmsAdaptationMatrix);
            this.cieXyzToCieLabConverter = new CieXyzToCieLabConverter(this.targetLabWhitePoint);
            this.cieXyzToCieLuvConverter = new CieXyzToCieLuvConverter(this.targetLuvWhitePoint);
            this.cieXyzToHunterLabConverter = new CieXyzToHunterLabConverter(this.targetHunterLabWhitePoint);
            this.cieXyzToLinearRgbConverter = new CieXyzToLinearRgbConverter(this.targetRgbWorkingSpace);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Configuration options for the <see cref="ColorSpaceConverter"/> class.
    /// </summary>
    public class ColorSpaceConverterOptions
    {
        /// <summary>
        /// Gets or sets the white point used for chromatic adaptation in conversions from/to XYZ color space.
        /// When <value>default</value>, no adaptation will be performed.
        /// Defaults to: <see cref="CieLuv.DefaultWhitePoint"/>.
        /// </summary>
        public CieXyz WhitePoint { get; set; } = CieLuv.DefaultWhitePoint;

        /// <summary>
        /// Gets or sets the white point used *when creating* Luv/LChuv colors. (Luv/LChuv colors on the input already contain the white point information)
        /// Defaults to: <see cref="CieLuv.DefaultWhitePoint"/>.
        /// </summary>
        public CieXyz TargetLuvWhitePoint { get; set; } = CieLuv.DefaultWhitePoint;

        /// <summary>
        /// Gets or sets the white point used *when creating* Lab/LChab colors. (Lab/LChab colors on the input already contain the white point information)
        /// Defaults to: <see cref="CieLab.DefaultWhitePoint"/>.
        /// </summary>
        public CieXyz TargetLabWhitePoint { get; set; } = CieLab.DefaultWhitePoint;

        /// <summary>
        /// Gets or sets the white point used *when creating* HunterLab colors. (HunterLab colors on the input already contain the white point information)
        /// Defaults to: <see cref="HunterLab.DefaultWhitePoint"/>.
        /// </summary>
        public CieXyz TargetHunterLabWhitePoint { get; set; } = HunterLab.DefaultWhitePoint;

        /// <summary>
        /// Gets or sets the target working space used *when creating* RGB colors. (RGB colors on the input already contain the working space information)
        /// Defaults to: <see cref="Rgb.DefaultWorkingSpace"/>.
        /// </summary>
        public RgbWorkingSpace TargetRgbWorkingSpace { get; set; } = Rgb.DefaultWorkingSpace;

        /// <summary>
        /// Gets or sets the chromatic adaptation method used. When <value>null</value>, no adaptation will be performed.
        /// </summary>
        public IChromaticAdaptation ChromaticAdaptation { get; set; } = new VonKriesChromaticAdaptation();

        /// <summary>
        /// Gets or sets transformation matrix used in conversion to and from <see cref="Lms"/>.
        /// </summary>
        public Matrix4x4 LmsAdaptationMatrix { get; set; } = CieXyzAndLmsConverter.DefaultTransformationMatrix;
    }
}

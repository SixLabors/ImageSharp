// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Modes to repeat a gradient.
    /// </summary>
    public enum GradientRepetitionMode
    {
        /// <summary>
        /// don't repeat, keep the color of start and end beyond those points stable.
        /// </summary>
        None,

        /// <summary>
        /// Repeat the gradient.
        /// If it's a black-white gradient, with Repeat it will be Black->{gray}->White|Black->{gray}->White|...
        /// </summary>
        Repeat,

        /// <summary>
        /// Reflect the gradient.
        /// Similar to <see cref="Repeat"/>, but each other repetition uses inverse order of <see cref="ColorStop{TPixel}"/>s.
        /// Used on a Black-White gradient, Reflect leads to Black->{gray}->White->{gray}->White...
        /// </summary>
        Reflect,

        /// <summary>
        /// With DontFill a gradient does not touch any pixel beyond it's borders.
        /// For the <see cref="LinearGradientBrush{TPixel}" /> this is beyond the orthogonal through start and end,
        /// TODO For the cref="PolygonalGradientBrush" it's outside the polygon,
        /// For <see cref="RadialGradientBrush{TPixel}" /> and <see cref="EllipticGradientBrush{TPixel}" /> it's beyond 1.0.
        /// </summary>
        DontFill
    }
}
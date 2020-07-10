// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Curve Segment Signature
    /// </summary>
    internal enum IccCurveSegmentSignature : uint
    {
        /// <summary>
        /// Curve defined by a formula
        /// </summary>
        FormulaCurve = 0x70617266,      // parf

        /// <summary>
        /// Curve defined by multiple segments
        /// </summary>
        SampledCurve = 0x73616D66,      // samf
    }
}

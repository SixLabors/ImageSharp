// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Curve Measurement Encodings
    /// </summary>
    internal enum IccCurveMeasurementEncodings : uint
    {
        /// <summary>
        /// ISO 5-3 densitometer response. This is the accepted standard for
        /// reflection densitometers for measuring photographic color prints
        /// </summary>
        StatusA = 0x53746141,   // StaA

        /// <summary>
        /// ISO 5-3 densitometer response which is the accepted standard in
        /// Europe for color reflection densitometers
        /// </summary>
        StatusE = 0x53746145,   // StaE

        /// <summary>
        /// ISO 5-3 densitometer response commonly referred to as narrow band
        /// or interference-type response.
        /// </summary>
        StatusI = 0x53746149,   // StaI

        /// <summary>
        /// ISO 5-3 wide band color reflection densitometer response which is
        /// the accepted standard in the United States for color reflection densitometers
        /// </summary>
        StatusT = 0x53746154,   // StaT

        /// <summary>
        /// ISO 5-3 densitometer response for measuring color negatives
        /// </summary>
        StatusM = 0x5374614D,   // StaM

        /// <summary>
        /// DIN 16536-2 densitometer response, with no polarizing filter
        /// </summary>
        DinE = 0x434E2020,      // DN

        /// <summary>
        /// DIN 16536-2 densitometer response, with polarizing filter
        /// </summary>
        DinEPol = 0x434E2050,  // DNP

        /// <summary>
        /// DIN 16536-2 narrow band densitometer response, with no polarizing filter
        /// </summary>
        DinI = 0x434E4E20,      // DNN

        /// <summary>
        /// DIN 16536-2 narrow band densitometer response, with polarizing filter
        /// </summary>
        DinIPol = 0x434E4E50,  // DNNP
    }
}

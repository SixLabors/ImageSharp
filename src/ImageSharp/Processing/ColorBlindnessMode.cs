// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Enumerates the various types of defined color blindness filters.
    /// </summary>
    public enum ColorBlindnessMode
    {
        /// <summary>
        /// Partial color desensitivity.
        /// </summary>
        Achromatomaly,

        /// <summary>
        /// Complete color desensitivity (Monochrome)
        /// </summary>
        Achromatopsia,

        /// <summary>
        /// Green weak
        /// </summary>
        Deuteranomaly,

        /// <summary>
        /// Green blind
        /// </summary>
        Deuteranopia,

        /// <summary>
        /// Red weak
        /// </summary>
        Protanomaly,

        /// <summary>
        /// Red blind
        /// </summary>
        Protanopia,

        /// <summary>
        /// Blue weak
        /// </summary>
        Tritanomaly,

        /// <summary>
        /// Blue blind
        /// </summary>
        Tritanopia
    }
}

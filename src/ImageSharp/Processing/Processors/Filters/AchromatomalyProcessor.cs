// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Achromatomaly (Color desensitivity) color blindness.
    /// </summary>
    public sealed class AchromatomalyProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AchromatomalyProcessor"/> class.
        /// </summary>
        public AchromatomalyProcessor()
            : base(KnownFilterMatrices.AchromatomalyFilter)
        {
        }
    }
}
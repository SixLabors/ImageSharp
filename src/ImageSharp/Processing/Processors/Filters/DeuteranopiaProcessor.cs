// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Deuteranopia (Green-Blind) color blindness.
    /// </summary>
    public sealed class DeuteranopiaProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeuteranopiaProcessor"/> class.
        /// </summary>
        public DeuteranopiaProcessor()
            : base(KnownFilterMatrices.DeuteranopiaFilter)
        {
        }
    }
}
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Deuteranomaly (Green-Weak) color blindness.
    /// </summary>
    public sealed class DeuteranomalyProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeuteranomalyProcessor"/> class.
        /// </summary>
        public DeuteranomalyProcessor()
            : base(KnownFilterMatrices.DeuteranomalyFilter)
        {
        }
    }
}
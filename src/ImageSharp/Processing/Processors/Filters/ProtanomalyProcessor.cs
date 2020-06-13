// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Protanomaly (Red-Weak) color blindness.
    /// </summary>
    public sealed class ProtanomalyProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProtanomalyProcessor"/> class.
        /// </summary>
        public ProtanomalyProcessor()
            : base(KnownFilterMatrices.ProtanomalyFilter)
        {
        }
    }
}
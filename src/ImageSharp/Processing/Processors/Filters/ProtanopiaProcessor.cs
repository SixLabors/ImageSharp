// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Protanopia (Red-Blind) color blindness.
    /// </summary>
    public sealed class ProtanopiaProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProtanopiaProcessor"/> class.
        /// </summary>
        public ProtanopiaProcessor()
            : base(KnownFilterMatrices.ProtanopiaFilter)
        {
        }
    }
}
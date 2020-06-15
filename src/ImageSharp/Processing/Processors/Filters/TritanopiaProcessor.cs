// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Tritanopia (Blue-Blind) color blindness.
    /// </summary>
    public sealed class TritanopiaProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TritanopiaProcessor"/> class.
        /// </summary>
        public TritanopiaProcessor()
            : base(KnownFilterMatrices.TritanopiaFilter)
        {
        }
    }
}
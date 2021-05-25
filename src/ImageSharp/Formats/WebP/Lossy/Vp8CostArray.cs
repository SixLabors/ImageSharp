// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8CostArray
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8CostArray"/> class.
        /// </summary>
        public Vp8CostArray()
        {
            this.Costs = new ushort[WebpConstants.NumCtx * (67 + 1)];
        }

        public ushort[] Costs { get; }
    }
}

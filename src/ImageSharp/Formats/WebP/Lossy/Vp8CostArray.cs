// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal class Vp8CostArray
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8CostArray"/> class.
        /// </summary>
        public Vp8CostArray()
        {
            this.Costs = new ushort[WebPConstants.NumCtx * (67 + 1)];
        }

        public ushort[] Costs { get; }
    }
}

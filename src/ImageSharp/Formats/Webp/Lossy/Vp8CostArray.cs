// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8CostArray
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8CostArray"/> class.
        /// </summary>
        public Vp8CostArray() => this.Costs = new ushort[67 + 1];

        public ushort[] Costs { get; }
    }
}

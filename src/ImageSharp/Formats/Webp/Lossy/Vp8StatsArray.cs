// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8StatsArray
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8StatsArray"/> class.
        /// </summary>
        public Vp8StatsArray() => this.Stats = new uint[WebpConstants.NumProbas];

        public uint[] Stats { get; }
    }
}

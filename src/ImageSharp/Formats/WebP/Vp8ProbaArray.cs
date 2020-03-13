// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Probabilities associated to one of the contexts.
    /// </summary>
    internal class Vp8ProbaArray
    {
        public Vp8ProbaArray()
        {
            this.Probabilities = new byte[WebPConstants.NumProbas];
        }

        public byte[] Probabilities { get; }
    }
}

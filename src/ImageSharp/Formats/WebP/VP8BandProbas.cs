// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// All the probabilities associated to one band.
    /// </summary>
    internal class Vp8BandProbas
    {
        public Vp8BandProbas()
        {
            this.Probabilities = new Vp8ProbaArray[WebPConstants.NumCtx];
            for (int i = 0; i < WebPConstants.NumCtx; i++)
            {
                this.Probabilities[i] = new Vp8ProbaArray();
            }
        }

        public Vp8ProbaArray[] Probabilities { get; }
    }
}

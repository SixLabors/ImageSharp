// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    public class GraphicsOptionsComparer : IEqualityComparer<GraphicsOptions>
    {
        public bool Equals(GraphicsOptions x, GraphicsOptions y)
        {
            return x.AlphaCompositionMode == y.AlphaCompositionMode
                && x.Antialias == y.Antialias
                && x.AntialiasSubpixelDepth == y.AntialiasSubpixelDepth
                && x.BlendPercentage == y.BlendPercentage
                && x.ColorBlendingMode == y.ColorBlendingMode;
        }

        public int GetHashCode(GraphicsOptions obj) => obj.GetHashCode();
    }
}

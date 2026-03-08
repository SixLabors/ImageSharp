// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestUtilities;

public class GraphicsOptionsComparer : IEqualityComparer<GraphicsOptions>
{
    public bool Equals(GraphicsOptions x, GraphicsOptions y)
        => x.AlphaCompositionMode == y.AlphaCompositionMode
        && x.Antialias == y.Antialias
        && x.AntialiasThreshold == y.AntialiasThreshold
        && x.BlendPercentage == y.BlendPercentage
        && x.ColorBlendingMode == y.ColorBlendingMode;

    public int GetHashCode(GraphicsOptions obj) => obj.GetHashCode();
}

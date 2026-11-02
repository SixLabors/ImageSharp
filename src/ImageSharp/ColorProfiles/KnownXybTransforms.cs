// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Provides standard XYB color transforms.
/// </summary>
public static class KnownXybTransforms
{
    /// <summary>
    /// Gets the standard JPEG XL absorbance transform with a reference intensity of 255 nits.
    /// </summary>
    public static XybTransform Default { get; } = new(
        new Matrix4x4(
            0.30F, 0.23F, 0.24342268924547819F, 0F,
            1F - 0.078F - 0.30F, 1F - 0.078F - 0.23F, 0.20476744424496821F, 0F,
            0.078F, 0.078F, 1F - 0.24342268924547819F - 0.20476744424496821F, 0F,
            0F, 0F, 0F, 1F),
        new Matrix4x4(
            11.031566901960783F, -3.254147380392157F, -3.6588512862745097F, 0F,
            -9.866943921568629F, 4.418770392156863F, 2.7129230470588235F, 0F,
            -0.16462299647058826F, -0.16462299647058826F, 1.9459282392156863F, 0F,
            0F, 0F, 0F, 1F),
        new Vector3(-0.0037930732552754493F),
        XybTransform.ReferenceIntensity);
}

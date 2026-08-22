// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

internal sealed class ButteraugliComparator
{
    private int xSize;
    private int ySize;
    private ButteraugliParameters parameters;
    private readonly ButteraugliPsychoImage pi0;
    private readonly JxlImage3F temp;
    private bool tempInUse;
    private readonly ButteraugliBlurTemp blurTemp;

    /// <summary>
    /// Computes the butteraugli map between the original image given in the constructor and the distorted image given here.
    /// </summary>
    public bool Diffmap(JxlImage3F rgb1, JxlImageF result)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Same as Diffmap but OpsinDynamicsImage() was already applied.
    /// </summary>
    public bool DiffmapOpsinDynamicsImage(JxlImage3F xyb1, JxlImageF result)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Same as above but the frequency decomposition was already applied.
    /// </summary>
    public bool DiffmapPsychoImage(ButteraugliPsychoImage pi1, JxlImageF diffmap)
    {
        throw new NotImplementedException();
    }

    public bool Mask(JxlImageF mask)
    {
        throw new NotImplementedException();
    }
}

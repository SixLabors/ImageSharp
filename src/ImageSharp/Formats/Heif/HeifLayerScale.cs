// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Gives the width and height of a layer of a layered image as a fraction of the image size. A smaller layer makes a
/// smaller file, and a viewer shows it enlarged to the image size.
/// </summary>
public enum HeifLayerScale
{
    /// <summary>
    /// The layer has the size of the image.
    /// </summary>
    Full = 0,

    /// <summary>
    /// The layer is four fifths of the image width and height.
    /// </summary>
    FourFifths = 1,

    /// <summary>
    /// The layer is three quarters of the image width and height.
    /// </summary>
    ThreeQuarters = 2,

    /// <summary>
    /// The layer is three fifths of the image width and height.
    /// </summary>
    ThreeFifths = 3,

    /// <summary>
    /// The layer is half of the image width and height.
    /// </summary>
    Half = 4,

    /// <summary>
    /// The layer is a quarter of the image width and height.
    /// </summary>
    Quarter = 5,

    /// <summary>
    /// The layer is an eighth of the image width and height.
    /// </summary>
    Eighth = 6
}

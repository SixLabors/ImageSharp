// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides enumeration of supported Item Property Types for HEIC.
/// </summary>
public enum HeicItemPropertyType : uint
{
    Invalid = 0,
    AcessibilityText = FourCharacterCode.altt,
    Colour = FourCharacterCode.colr,
    HvcConfiguration = FourCharacterCode.hvcC,
    ImageMirror = FourCharacterCode.imir,
    ImageRotation = FourCharacterCode.irot,
    ImageScaling = FourCharacterCode.iscl,
    ImageSpatialExtents = FourCharacterCode.ispe,
    PixelAspectRatio = FourCharacterCode.pasp,
    PixelInformation = FourCharacterCode.pixi,
    RelativeLocation = FourCharacterCode.rloc,
    UserDescription = FourCharacterCode.udes,
}

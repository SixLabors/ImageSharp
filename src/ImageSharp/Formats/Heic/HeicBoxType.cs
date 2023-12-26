// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides enumeration of supported ISO Base Format Box Types for HEIC.
/// </summary>
public enum HeicBoxType : uint
{
    FileType = FourCharacterCode.ftyp,
    Meta = FourCharacterCode.meta,
    MediaData = FourCharacterCode.mdat,
    ItemInfo = FourCharacterCode.infe
    ItemData = FourCharacterCode.idat,
    ItemLocation = FourCharacterCode.iloc,
    Exif = FourCharacterCode.Exif,
    ItemPropertyAssociation = FourCharacterCode.ipma,
    DataReference = FourCharacterCode.dref,
    PrimaryItemReference = FourCharacterCode.pitm,
    ImageSpatialExtentsProperty = FourCharacterCode.ispe,

    // Possible box types outside of HEIC images:
    Movie = FourCharacterCode.moov,
    Track = FourCharacterCode.trak,
}

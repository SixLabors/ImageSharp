// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides enumeration of supported sub type boxes within the 'meta' box for HEIC.
/// </summary>
public enum HeicMetaSubBoxType : uint
{
    Invalid = 0,
    DataInformation = 0, // 'dinf'
    GroupsList = 0, // 'grpl'
    Handler = 0, // 'hdlr'
    ItemData = 0, // 'idat'
    ItemInfo = 0, // 'iinf'
    ItemLocation = 0, // 'iloc'
    ItemProperty = 0, // 'iprp'
    ItemProtection = 0, // 'ipro'
    ItemReference = 0, // 'iref'
    PrimaryItem = 0, // 'pitm'
}

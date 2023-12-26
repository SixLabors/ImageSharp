// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

public enum HeicItemType
{
    Hvc1,
    Grid,
    Exif
}

public class HeicItemLink
{
    public uint Type;
    public HeicItem Source;
    public List<HeicItem> Destinations = new List<HeicItem>();
}

/// <summary>
/// Provides definition for a HEIC Item.
/// </summary>
public class HeicItem
{
    public uint Id;
    public HeicItemType type;
    public string Name;
    public string ContentType;
    public string ContentEncoding;
    public uint ExtensionType;
    public string UriType;
}

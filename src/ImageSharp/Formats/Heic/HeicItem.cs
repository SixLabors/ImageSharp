// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides definition for a HEIC Item.
/// </summary>
public class HeicItem(uint type, uint id)
{
    /// <summary>
    /// Gets the ID of this Item.
    /// </summary>
    public uint Id { get; } = id;

    /// <summary>
    /// Gets the type of this Item.
    /// </summary>
    public uint Type { get; } = type;

    /// <summary>
    /// Gets or sets the name of this item.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the Content Type of this item.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the Content Encoding of this item.
    /// </summary>
    public string? ContentEncoding { get; set; }

    /// <summary>
    /// Gets or sets the type of extension of this item.
    /// </summary>
    public uint ExtensionType { get; set; }

    /// <summary>
    /// Gets or sets the URI of this item.
    /// </summary>
    public string? UriType { get; set; }

    /// <summary>
    /// Sets a property on this item.
    /// </summary>
    public void SetProperty(KeyValuePair<uint, object> pair)
    {
        switch (pair.Key)
        {
            case FourCharacterCode.ispe:
                // Set image extents
                break;
            case FourCharacterCode.pasp:
                // Set pixel aspact ratio
                break;
            case FourCharacterCode.pixi:
                // Set pixel information
                break;
        }
    }
}

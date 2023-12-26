// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides constants for 4 Character codes used in HEIC images.
/// </summary>
public static class FourCharacterCode
{
    // TODO: Create T4 template for this file

    /// <summary>
    /// File Type
    /// </summary>
    public const uint ftyp = 0x66747970U,

    /// <summary>
    /// Metadata container
    /// </summary>
    public const uint meta = 0x6D657461U,

    /// <summary>
    /// Media Data
    /// </summary>
    public const uint mdat = 0x6D646174U,

    /// <summary>
    /// Item Information Entry
    /// </summary>
    public const uint infe = 0x696E6665U,

    /// <summary>
    /// Item Data
    /// </summary>
    public const uint idat = 0x69646174U,

    /// <summary>
    /// Item Location
    /// </summary>
    public const uint iloc = 0x696C6F63U,

    /// <summary>
    /// EXIF metadata
    /// </summary>
    public const uint Exif = 0x45786966U,

    /// <summary>
    /// Data Reference
    /// </summary>
    public const uint dref = 0x64726566U,

    /// <summary>
    /// Primary Item
    /// </summary>
    public const uint pitm = 0x7069746DU,

    /// <summary>
    /// Item Spatial Extent
    /// </summary>
    public const uint ispe = 0x69737064U,

    /// <summary>
    /// Alternative text
    /// </summary>
    public const uint altt = 0, // 'altt'

    /// <summary>
    /// Colour information
    /// </summary>
    public const uint colr = 0, // 'colr'

    /// <summary>
    /// HVC configuration
    /// </summary>
    public const uint hvcC = 0, // 'hvcC'

    /// <summary>
    /// Image Mirror
    /// </summary>
    public const uint imir = 0, // 'imir'

    /// <summary>
    /// Image Rotation
    /// </summary>
    public const uint irot = 0, // 'irot'

    /// <summary>
    /// Image Scaling
    /// </summary>
    public const uint iscl = 0, // 'iscl'

    /// <summary>
    /// Pixel Aspect Ration
    /// </summary>
    public const uint pasp = 0, // 'pasp'

    /// <summary>
    /// Pixel Information
    /// </summary>
    public const uint pixi = 0x70697869U,

    /// <summary>
    /// Reference Location
    /// </summary>
    public const uint rloc = 0, // 'rloc

    /// <summary>
    /// User Description
    /// </summary>
    public const uint udes = 0, // 'udes'

    /// <summary>
    /// Item Property Container
    /// </summary>
    public const uint ipco = 0,

    /// <summary>
    /// Item Property Association
    /// </summary>
    public const uint ipma = 0,

    /// <summary>
    /// High Efficient Image Coding
    /// </summary>
    public const uint heic = 0,

    /// <summary>
    /// High Efficiency Coding tile
    /// </summary>
    public const uint hvc1 = 0,

    /// <summary>
    /// Data Information
    /// </summary>
    public const uint dinf = 0,

    /// <summary>
    /// Group list
    /// </summary>
    public const uint grpl = 0,

    /// <summary>
    /// Handler
    /// </summary>
    public const uint hdlr = 0,

    /// <summary>
    /// Item Data
    /// </summary>
    public const uint idat = 0, // 'idat'

    /// <summary>
    /// Item Information
    /// </summary>
    public const uint iinf = 0, // 'iinf'

    /// <summary>
    /// Item Property
    /// </summary>
    public const uint iprp = 0, // 'iprp'

    /// <summary>
    /// Item Protection
    /// </summary>
    public const uint ipro = 0, // 'ipro'

    /// <summary>
    /// Item Reference
    /// </summary>
    public const uint iref = 0, // 'iref'

    /// <summary>
    /// Grid
    /// </summary>
    public const uint grid = 0, // 'grid'

    /// <summary>
    /// Derived Image
    /// </summary>
    public const uint dimg = 0, // 'dimg'

    /// <summary>
    /// Thumbnail
    /// </summary>
    public const uint thmb = 0, // 'thmb'

    /// <summary>
    /// Content Description
    /// </summary>
    public const uint cdsc = 0, // 'cdsc'

    public static uint Parse(string code)
    {
        if (code.Length != 4)
        {
            throw new ImageFormatException();
        }
        Span<byte> span = Encoding.UTF8.GetBytes(code);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    public static string ToString(uint fourcc)
    {
        Span<byte> span = stackalloc new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, fourcc);
        return Encoding.UTF8.GetString(span);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Container;

/// <summary>
/// A ftyp box payload.
/// </summary>
internal sealed class JxlFileTypeBox(string majorBrand, uint minorVersion)
{
    /// <summary>
    /// Gets or sets the primary format. Has to be "jxl " for JPEG XL.
    /// </summary>
    public string MajorBrand { get; set; } = majorBrand;

    /// <summary>
    /// Gets or sets the revision of the major brand.
    /// </summary>
    public uint MinorVersion { get; set; } = minorVersion;

    /// <summary>
    /// Gets or sets the list of other brands the file is compatible with.
    /// </summary>
    public List<string> CompatibleBrands { get; set; } = [];

    public int GetPayloadSize() => 8 + (this.CompatibleBrands.Count * 4);

    public static JxlFileTypeBox Parse(Stream stream, ulong boxSize)
    {
        string majorBrand = JxlBoxHeader.TypeToString(BinaryUtils.ReadUInt32BigEndian(stream));
        uint minorVersion = BinaryUtils.ReadUInt32BigEndian(stream);
        boxSize -= 8;

        List<string> compatibleBrands = [];
        for (ulong i = 0; i < boxSize; i += 4)
        {
            compatibleBrands.Add(JxlBoxHeader.TypeToString(BinaryUtils.ReadUInt32BigEndian(stream)));
        }

        JxlFileTypeBox ftyp = new(majorBrand, minorVersion)
        {
            CompatibleBrands = compatibleBrands
        };

        return ftyp;
    }

    public void WritePayload(Stream stream)
    {
        BinaryUtils.WriteUInt32BigEndian(stream, JxlBoxHeader.TypeFromString(this.MajorBrand));
        BinaryUtils.WriteUInt32BigEndian(stream, this.MinorVersion);

        foreach (string compatibleBrand in this.CompatibleBrands)
        {
            BinaryUtils.WriteUInt32BigEndian(stream, JxlBoxHeader.TypeFromString(compatibleBrand));
        }
    }
}

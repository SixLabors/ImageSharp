// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Contains HEIF constant values defined in the specification.
/// </summary>
internal static class HeifConstants
{
    public const Heif4CharCode HeicBrand = Heif4CharCode.Heic;

    /// <summary>
    /// The list of mimetypes that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = new[] { "image/heif", "image/heic", "image/avif" };

    /// <summary>
    /// The list of file extensions that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "heic", "heif", "hif", "avif" };

    public static bool IsSupportedFileType(ReadOnlySpan<byte> boxContent)
    {
        if (boxContent.Length < 8 || (boxContent.Length & 3) != 0)
        {
            return false;
        }

        Heif4CharCode majorBrand = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxContent);
        if (IsSequenceBrand(majorBrand))
        {
            return false;
        }

        if (IsSupportedStillImageBrand(majorBrand))
        {
            return true;
        }

        // The minor-version field follows the major brand; compatible brands start at byte eight.
        for (int offset = 8; offset < boxContent.Length; offset += 4)
        {
            Heif4CharCode compatibleBrand = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxContent[offset..]);
            if (IsSupportedStillImageBrand(compatibleBrand))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportedStillImageBrand(Heif4CharCode brand)
        => brand is Heif4CharCode.Heic
            or Heif4CharCode.Heix
            or Heif4CharCode.Mif1
            or Heif4CharCode.Avif
            or Heif4CharCode.Jpeg;

    private static bool IsSequenceBrand(Heif4CharCode brand)
        => brand is Heif4CharCode.Hevc
            or Heif4CharCode.Hevx
            or Heif4CharCode.Hevm
            or Heif4CharCode.Hevs
            or Heif4CharCode.Avis
            or Heif4CharCode.Jpgs;
}

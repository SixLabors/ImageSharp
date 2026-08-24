// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Contains HEIF constant values defined in the specification.
/// </summary>
internal static class HeifConstants
{
    /// <summary>
    /// The HEIC still-image brand written by the encoder.
    /// </summary>
    public const Heif4CharCode HeicBrand = Heif4CharCode.Heic;

    /// <summary>
    /// The list of mimetypes that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = new[] { "image/heif", "image/heic", "image/avif" };

    /// <summary>
    /// The list of file extensions that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "heic", "heif", "hif", "avif" };

    /// <summary>
    /// Determines whether a file-type box describes a supported still-image container.
    /// </summary>
    /// <param name="boxContent">
    /// The file-type box payload, beginning with the major brand and minor version and followed by compatible brands.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the major brand is a supported still-image brand, or when an otherwise unknown
    /// major brand declares a supported compatible still-image brand; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsSupportedFileType(ReadOnlySpan<byte> boxContent)
    {
        // Every brand is a four-character code. The payload must contain the major brand and minor version before
        // any compatible brands, otherwise accepting a partial trailing code could produce a false detection.
        if (boxContent.Length < 8 || (boxContent.Length & 3) != 0)
        {
            return false;
        }

        Heif4CharCode majorBrand = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxContent);
        if (IsSequenceBrand(majorBrand))
        {
            // Sequence major brands describe timed image sequences, which the still-image decoder cannot expose.
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

    /// <summary>
    /// Determines whether <paramref name="brand"/> identifies a still-image container supported by this codec.
    /// </summary>
    /// <param name="brand">The registered file-type brand.</param>
    /// <returns><see langword="true"/> when the brand identifies a supported still-image container.</returns>
    private static bool IsSupportedStillImageBrand(Heif4CharCode brand)
        => brand is Heif4CharCode.Heic
            or Heif4CharCode.Heix
            or Heif4CharCode.Mif1
            or Heif4CharCode.Avif
            or Heif4CharCode.Jpeg;

    /// <summary>
    /// Determines whether <paramref name="brand"/> identifies a timed image sequence.
    /// </summary>
    /// <param name="brand">The registered file-type brand.</param>
    /// <returns><see langword="true"/> when the brand identifies a timed image sequence.</returns>
    private static bool IsSequenceBrand(Heif4CharCode brand)
        => brand is Heif4CharCode.Hevc
            or Heif4CharCode.Hevx
            or Heif4CharCode.Hevm
            or Heif4CharCode.Hevs
            or Heif4CharCode.Avis
            or Heif4CharCode.Jpgs;
}

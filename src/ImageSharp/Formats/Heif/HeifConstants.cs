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
    /// The auxiliary-type URN used by current HEIF alpha image items.
    /// </summary>
    public const string AlphaAuxiliaryType = "urn:mpeg:mpegB:cicp:systems:auxiliary:alpha";

    /// <summary>
    /// The MIME types recognized by this HEIF implementation.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = new[] { "image/heif", "image/avif" };

    /// <summary>
    /// The file extensions recognized by this HEIF implementation.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "heif", "hif", "avif" };

    /// <summary>
    /// Determines the supported image presentation declared by a file-type box.
    /// </summary>
    /// <param name="boxContent">
    /// The file-type box payload, beginning with the major brand and minor version and followed by compatible brands.
    /// </param>
    /// <param name="fileType">Receives the supported still-image or image-sequence presentation.</param>
    /// <returns><see langword="true"/> when the payload declares a supported HEIF image presentation.</returns>
    public static bool TryGetFileType(ReadOnlySpan<byte> boxContent, out HeifFileType fileType)
    {
        fileType = HeifFileType.Unsupported;

        // Every brand is a four-character code. The payload must contain the major brand and minor version before
        // any compatible brands, otherwise accepting a partial trailing code could produce a false detection.
        if (boxContent.Length < 8 || (boxContent.Length & 3) != 0)
        {
            return false;
        }

        Heif4CharCode majorBrand = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxContent);
        if (IsUnsupportedSequenceBrand(majorBrand))
        {
            // A sequence major brand controls the presentation even if a still-image compatible brand is present.
            return false;
        }

        if (IsSupportedSequenceBrand(majorBrand))
        {
            fileType = HeifFileType.ImageSequence;
            return true;
        }

        if (IsSupportedStillImageBrand(majorBrand))
        {
            fileType = HeifFileType.StillImage;
            return true;
        }

        // The minor-version field follows the major brand; compatible brands start at byte eight.
        bool hasStillImageBrand = false;
        for (int offset = 8; offset < boxContent.Length; offset += 4)
        {
            Heif4CharCode compatibleBrand = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxContent[offset..]);
            if (IsSupportedSequenceBrand(compatibleBrand))
            {
                fileType = HeifFileType.ImageSequence;
                return true;
            }

            if (IsSupportedStillImageBrand(compatibleBrand))
            {
                hasStillImageBrand = true;
            }
        }

        fileType = hasStillImageBrand ? HeifFileType.StillImage : HeifFileType.Unsupported;
        return hasStillImageBrand;
    }

    /// <summary>
    /// Determines whether an auxiliary-type property identifies an alpha image plane.
    /// </summary>
    /// <param name="auxiliaryType">The null-terminated auxiliary type decoded from an <c>auxC</c> property.</param>
    /// <returns><see langword="true"/> when the type is the registered HEIF alpha URN.</returns>
    public static bool IsAlphaAuxiliaryType(string? auxiliaryType)
        => auxiliaryType == AlphaAuxiliaryType;

    /// <summary>
    /// Determines whether <paramref name="brand"/> identifies a still-image container supported by this codec.
    /// </summary>
    /// <param name="brand">The registered file-type brand.</param>
    /// <returns><see langword="true"/> when the brand identifies a supported still-image container.</returns>
    private static bool IsSupportedStillImageBrand(Heif4CharCode brand)
        => brand is Heif4CharCode.Mif1
            or Heif4CharCode.Avif
            or Heif4CharCode.Jpeg;

    /// <summary>
    /// Determines whether <paramref name="brand"/> identifies a supported timed image sequence.
    /// </summary>
    /// <param name="brand">The registered file-type brand.</param>
    /// <returns><see langword="true"/> when the brand identifies a supported timed image sequence.</returns>
    private static bool IsSupportedSequenceBrand(Heif4CharCode brand)
        => brand is Heif4CharCode.Avis;

    /// <summary>
    /// Determines whether <paramref name="brand"/> requires an image-sequence profile outside the implemented scope.
    /// </summary>
    /// <param name="brand">The registered file-type brand.</param>
    /// <returns><see langword="true"/> when the major brand requires unsupported JPEG sequence support.</returns>
    private static bool IsUnsupportedSequenceBrand(Heif4CharCode brand)
        => brand is Heif4CharCode.Jpgs;
}

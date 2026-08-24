// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

/// <summary>
/// Creates encoded images with malformed metadata payloads while keeping the surrounding container valid.
/// </summary>
internal static class CorruptedMetadataImageFactory
{
    /// <summary>
    /// Creates an encoded single-pixel image with a malformed ICC profile.
    /// </summary>
    /// <param name="encoder">The encoder used to produce the image bytes.</param>
    /// <returns>The encoded image bytes with a malformed ICC profile payload.</returns>
    public static byte[] CreateImageWithMalformedIccProfile(IImageEncoder encoder)
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(255, 0, 0, 255);
        image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.HeaderInvalidSizeSmallArray);

        using MemoryStream stream = new();
        image.Save(stream, encoder);

        return stream.ToArray();
    }
}

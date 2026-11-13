// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

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
        image.Metadata.IccProfile = CompactSrgbV4Profile.Profile;

        using MemoryStream stream = new();
        image.Save(stream, encoder);

        // The metadata setter rejects malformed profiles. Change the size field only after encoding.
        byte[] encoded = stream.ToArray();
        int profileOffset = encoded.AsSpan().IndexOf(CompactSrgbV4Profile.Profile.ToByteArray());
        Assert.True(profileOffset >= 0, "The encoder did not write the ICC profile.");
        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(profileOffset), 127);

        return encoded;
    }
}

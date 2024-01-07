// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for the <see cref="ImageMetadata"/> type.
/// </summary>
public static partial class MetadataExtensions
{
    /// <summary>
    /// Gets the jpeg format specific metadata for the image.
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The <see cref="JpegMetadata"/>.</returns>
    public static JpegMetadata GetJpegMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(JpegFormat.Instance);

    /// <summary>
    /// Saves the comment into <see cref="JpegMetadata"/>
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <param name="comment">The comment string.</param>
    public static void SaveComment(this JpegMetadata metadata, string comment)
    {
        ASCIIEncoding encoding = new();

        byte[] bytes = encoding.GetBytes(comment);
        metadata.Comments?.Add(encoding.GetChars(bytes));
    }

    /// <summary>
    /// Gets the comments from <see cref="JpegMetadata"/>
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The IEnumerable string of comments.</returns>
    public static IEnumerable<string>? GetComments(this JpegMetadata metadata) => metadata.Comments?.Select(x => x.ToString());
}

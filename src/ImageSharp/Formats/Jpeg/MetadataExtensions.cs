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
    /// Sets the comment in <see cref="JpegMetadata"/>
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <param name="index">The index of comment to be inserted to.</param>
    /// <param name="comment">The comment string.</param>
    public static void SetComment(this JpegMetadata metadata, int index, string comment)
    {
        if (metadata.Comments == null)
        {
            return;
        }

        ASCIIEncoding encoding = new();
        byte[] bytes = encoding.GetBytes(comment);
        List<Memory<char>>? comments = metadata.Comments as List<Memory<char>>;
        comments?.Insert(index, encoding.GetChars(bytes));
    }

    /// <summary>
    /// Gets the comments from <see cref="JpegMetadata"/>
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <param name="index">The index of comment.</param>
    /// <returns>The IEnumerable string of comments.</returns>
    public static string? GetComment(this JpegMetadata metadata, int index) => metadata.Comments?.ElementAtOrDefault(index).ToString();

    /// <summary>
    /// Clears comments
    /// </summary>
    /// <param name="metadata">The <see cref="JpegMetadata"/>.</param>
    public static void ClearComments(this JpegMetadata metadata) => metadata.Comments?.Clear();
}

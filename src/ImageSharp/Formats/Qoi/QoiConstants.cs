// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Formats.Qoi;

internal static class QoiConstants
{
    /// <summary>
    /// Gets the bytes that indicates the image is QOI
    /// </summary>
    public static ReadOnlySpan<byte> Magic => Encoding.UTF8.GetBytes("qoif");

    /// <summary>
    /// The list of mimetypes that equate to a QOI.
    /// See https://github.com/phoboslab/qoi/issues/167
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = new[] { "image/qoi", "image/x-qoi", "image/vnd.qoi" };

    /// <summary>
    /// The list of file extensions that equate to a QOI.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "qoi" };
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Formats.Qoi;

internal static class QoiConstants
{
    private static readonly byte[] SMagic = Encoding.UTF8.GetBytes("qoif");

    /// <summary>
    /// Gets the bytes that indicates the image is QOI
    /// </summary>
    public static ReadOnlySpan<byte> Magic => SMagic;

    /// <summary>
    /// Gets the list of mimetypes that equate to a QOI.
    /// See https://github.com/phoboslab/qoi/issues/167
    /// </summary>
    public static string[] MimeTypes { get; } = ["image/qoi", "image/x-qoi", "image/vnd.qoi"];

    /// <summary>
    /// Gets the list of file extensions that equate to a QOI.
    /// </summary>
    public static string[] FileExtensions { get; } = ["qoi"];
}

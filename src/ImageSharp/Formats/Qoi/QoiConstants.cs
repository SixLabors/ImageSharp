// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Formats.Qoi;

internal static class QoiConstants
{
    /// <summary>
    /// The size of the header in bytes: the magic "qoif", the big-endian uint32 width and height, the channels byte and the color space byte. See The Quite OK
    /// Image Format Specification, Version 1.0, qoi_header.
    /// </summary>
    public const int HeaderSize = 14;

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

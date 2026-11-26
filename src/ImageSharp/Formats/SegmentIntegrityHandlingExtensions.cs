// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Applies a <see cref="SegmentIntegrityHandling"/> mode to the errors that a decoder finds. The check that finds the error calls one of these methods and then
/// returns or stops, so all formats apply the same policy.
/// </summary>
internal static class SegmentIntegrityHandlingExtensions
{
    /// <summary>
    /// Gets a value that tells if the mode ignores recoverable errors in ancillary segments.
    /// </summary>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="SegmentIntegrityHandling.IgnoreAncillary"/> and
    /// <see cref="SegmentIntegrityHandling.IgnoreImageData"/>. Otherwise <see langword="false"/>.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static bool IgnoresAncillaryErrors(this SegmentIntegrityHandling handling)
        => handling != SegmentIntegrityHandling.Strict;

    /// <summary>
    /// Gets a value that tells if the mode ignores recoverable errors in image data.
    /// </summary>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="SegmentIntegrityHandling.IgnoreImageData"/>. Otherwise
    /// <see langword="false"/>.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static bool IgnoresImageDataErrors(this SegmentIntegrityHandling handling)
        => handling == SegmentIntegrityHandling.IgnoreImageData;

    /// <summary>
    /// Reports a damaged or truncated ancillary segment. In the <see cref="SegmentIntegrityHandling.Strict"/> mode, the
    /// method throws. In the other modes, it returns, and the caller continues without the segment data.
    /// </summary>
    /// <remarks>
    /// The mode check can inline into the caller. The throw stays in a separate cold method, so the caller does not grow.
    /// </remarks>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="message">The exception message.</param>
    /// <exception cref="InvalidImageContentException">The mode is <see cref="SegmentIntegrityHandling.Strict"/>.</exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ThrowOrIgnoreAncillaryError(this SegmentIntegrityHandling handling, string message)
    {
        if (!handling.IgnoresAncillaryErrors())
        {
            ThrowInvalidImageContentException(message);
        }
    }

    /// <summary>
    /// Reports a damaged ancillary segment that another exception describes. In the
    /// <see cref="SegmentIntegrityHandling.Strict"/> mode, the method throws. In the other modes, it returns, and the
    /// caller continues without the segment data.
    /// </summary>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that describes the damage.</param>
    /// <exception cref="InvalidImageContentException">The mode is <see cref="SegmentIntegrityHandling.Strict"/>.</exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ThrowOrIgnoreAncillaryError(this SegmentIntegrityHandling handling, string message, Exception innerException)
    {
        if (!handling.IgnoresAncillaryErrors())
        {
            ThrowInvalidImageContentException(message, innerException);
        }
    }

    /// <summary>
    /// Reports damaged or truncated image data. In the <see cref="SegmentIntegrityHandling.Strict"/> and
    /// <see cref="SegmentIntegrityHandling.IgnoreAncillary"/> modes, the method throws. In the
    /// <see cref="SegmentIntegrityHandling.IgnoreImageData"/> mode, it returns, and the caller keeps the pixels that it
    /// decoded.
    /// </summary>
    /// <remarks>
    /// Decoders call this method inside their decode loops. The mode check can inline into those loops. The throw stays
    /// in a separate cold method, so the loops do not grow.
    /// </remarks>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="message">The exception message.</param>
    /// <exception cref="InvalidImageContentException">The mode is not <see cref="SegmentIntegrityHandling.IgnoreImageData"/>.</exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ThrowOrIgnoreImageDataError(this SegmentIntegrityHandling handling, string message)
    {
        if (!handling.IgnoresImageDataErrors())
        {
            ThrowInvalidImageContentException(message);
        }
    }

    /// <summary>
    /// Reports damaged image data that the decoder can find before or after it creates the image, for example a damaged frame header. Before the image exists,
    /// the decoder has no pixels to keep, so the method throws in each mode. After that, the method acts as
    /// <see cref="ThrowOrIgnoreImageDataError(SegmentIntegrityHandling, string)"/>: in the <see cref="SegmentIntegrityHandling.IgnoreImageData"/> mode, it
    /// returns, and the caller keeps the frames or pixels that it decoded.
    /// </summary>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="imageExists">A value that tells if the decoder created the image before the damaged data.</param>
    /// <param name="message">The exception message.</param>
    /// <exception cref="InvalidImageContentException">
    /// The image does not exist, or the mode is not <see cref="SegmentIntegrityHandling.IgnoreImageData"/>.
    /// </exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ThrowOrIgnoreImageDataError(this SegmentIntegrityHandling handling, bool imageExists, string message)
    {
        if (!imageExists || !handling.IgnoresImageDataErrors())
        {
            ThrowInvalidImageContentException(message);
        }
    }

    /// <summary>
    /// Reports damaged image data that another exception describes. In the <see cref="SegmentIntegrityHandling.Strict"/>
    /// and <see cref="SegmentIntegrityHandling.IgnoreAncillary"/> modes, the method throws. In the
    /// <see cref="SegmentIntegrityHandling.IgnoreImageData"/> mode, it returns, and the caller keeps the pixels that it
    /// decoded.
    /// </summary>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that describes the damage.</param>
    /// <exception cref="InvalidImageContentException">The mode is not <see cref="SegmentIntegrityHandling.IgnoreImageData"/>.</exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ThrowOrIgnoreImageDataError(this SegmentIntegrityHandling handling, string message, Exception innerException)
    {
        if (!handling.IgnoresImageDataErrors())
        {
            ThrowInvalidImageContentException(message, innerException);
        }
    }

    /// <summary>
    /// Throws an <see cref="InvalidImageContentException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    private static void ThrowInvalidImageContentException(string message)
        => throw new InvalidImageContentException(message);

    /// <summary>
    /// Throws an <see cref="InvalidImageContentException"/> that wraps another exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that describes the damage.</param>
    [DoesNotReturn]
    [MethodImpl(InliningOptions.ColdPath)]
    private static void ThrowInvalidImageContentException(string message, Exception innerException)
        => throw new InvalidImageContentException(message, innerException);
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Applies a <see cref="SegmentIntegrityHandling"/> mode to the errors that a decoder finds. Each decoder and each
/// part of a decoder uses these methods, so all formats apply the same policy.
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
    /// Gets a value that tells if the mode ignores an exception from the parse of an ancillary segment.
    /// </summary>
    /// <remarks>
    /// The method is a catch filter. An ancillary segment can use a feature that the decoder does not support. The
    /// decoder does not need the segment for the pixels, so a <see cref="NotSupportedException"/> is also recoverable.
    /// </remarks>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="exception">The exception from the parse.</param>
    /// <returns><see langword="true"/> if the caller can continue without the segment data. Otherwise <see langword="false"/>.</returns>
    public static bool ShouldIgnoreAncillaryError(this SegmentIntegrityHandling handling, Exception exception)
        => handling.IgnoresAncillaryErrors()
            && (IsDamagedDataError(exception) || exception is NotSupportedException);

    /// <summary>
    /// Gets a value that tells if the mode ignores an exception from the decode of image data.
    /// </summary>
    /// <remarks>
    /// The method is a catch filter. A <see cref="NotSupportedException"/> is not damage: the data uses a feature that the
    /// decoder does not support. Thus the filter lets it pass in each mode.
    /// </remarks>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="exception">The exception from the decode.</param>
    /// <returns><see langword="true"/> if the caller can keep the pixels that it decoded. Otherwise <see langword="false"/>.</returns>
    public static bool ShouldIgnoreImageDataError(this SegmentIntegrityHandling handling, Exception exception)
        => handling.IgnoresImageDataErrors() && IsDamagedDataError(exception);

    /// <summary>
    /// Runs the parse of an ancillary segment. The mode controls if a recoverable error from the parse throws.
    /// </summary>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="action">The parse of the segment.</param>
    public static void ExecuteAncillarySegmentAction(this SegmentIntegrityHandling handling, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (handling.ShouldIgnoreAncillaryError(ex))
        {
            // The mode ignores this error. The decoder continues without the segment data.
        }
    }

    /// <summary>
    /// Runs the decode of image data. The mode controls if a recoverable error from the decode throws.
    /// </summary>
    /// <param name="handling">The segment integrity mode of the decode.</param>
    /// <param name="action">The decode of the image data.</param>
    public static void ExecuteImageDataSegmentAction(this SegmentIntegrityHandling handling, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (handling.ShouldIgnoreImageDataError(ex))
        {
            // The mode ignores this error. The decoder keeps the pixels that it decoded.
        }
    }

    /// <summary>
    /// Gets a value that tells if an exception reports damaged or truncated data.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> if the exception reports damaged or truncated data. Otherwise <see langword="false"/>.</returns>
    private static bool IsDamagedDataError(Exception exception)
        => exception is ImageFormatException or InvalidIccProfileException or InvalidOperationException;

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

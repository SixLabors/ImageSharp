using System;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// A base interface for GIF extensions.
    /// </summary>
    public interface IGifExtension
    {
        /// <summary>
        /// Gets the label identifying the extensions.
        /// </summary>
        byte Label { get; }

        /// <summary>
        /// Writes the extension data to the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write the extension to.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        int WriteTo(Span<byte> buffer);
    }
}
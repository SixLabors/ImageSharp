namespace SixLabors.ImageSharp.Formats.Apng
{
    /// <summary>
    /// Specifies how the output buffer should be changed at the end of the delay (before rendering the next frame).
    /// </summary>
    public enum ApngDisposeMethod : byte
    {
        /// <summary>
        /// No disposal is done on this frame before rendering the next; the contents of the output buffer are left as is.
        /// </summary>
        None = 0,

        /// <summary>
        /// The frame's region of the output buffer is to be cleared to fully transparent black before rendering the next frame.
        /// </summary>
        Background = 1,

        /// <summary>
        /// The frame's region of the output buffer is to be reverted to the previous contents before rendering the next frame.
        /// </summary>
        Previous = 2
    }
}
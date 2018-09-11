namespace SixLabors.ImageSharp.Formats.Apng
{
    /// <summary>
    /// Specifies whether the frame is to be alpha blended into the current output buffer content, 
    /// or whether it should completely replace its region in the output buffer.
    /// </summary>
    public enum ApngBlendMethod : byte
    {
        /// <summary>
        /// All color components of the frame, including alpha, overwrite the current contents of the frame's output buffer region
        /// </summary>
        Source = 0,

        /// <summary>
        /// The frame should be composited onto the output buffer based on its alpha, 
        /// using a simple OVER operation as described in the "Alpha Channel Processing" section of the PNG specificatio
        /// </summary>
        Over = 1
    }
}
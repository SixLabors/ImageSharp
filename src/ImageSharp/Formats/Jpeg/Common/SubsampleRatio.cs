namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    /// <summary>
    /// Provides enumeration of the various available subsample ratios.
    /// https://en.wikipedia.org/wiki/Chroma_subsampling
    /// </summary>
    internal enum SubsampleRatio
    {
        Undefined,

        /// <summary>
        /// 4:4:4
        /// </summary>
        Ratio444,

        /// <summary>
        /// 4:2:2
        /// </summary>
        Ratio422,

        /// <summary>
        /// 4:2:0
        /// </summary>
        Ratio420,

        /// <summary>
        /// 4:4:0
        /// </summary>
        Ratio440,

        /// <summary>
        /// 4:1:1
        /// </summary>
        Ratio411,

        /// <summary>
        /// 4:1:0
        /// </summary>
        Ratio410,
    }
}
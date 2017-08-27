namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
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

    /// <summary>
    /// Various utilities for <see cref="SubsampleRatio"/>
    /// </summary>
    internal static class Subsampling
    {
        public static SubsampleRatio GetSubsampleRatio(int horizontalRatio, int verticalRatio)
        {
            switch ((horizontalRatio << 4) | verticalRatio)
            {
                case 0x11:
                    return SubsampleRatio.Ratio444;
                case 0x12:
                    return SubsampleRatio.Ratio440;
                case 0x21:
                    return SubsampleRatio.Ratio422;
                case 0x22:
                    return SubsampleRatio.Ratio420;
                case 0x41:
                    return SubsampleRatio.Ratio411;
                case 0x42:
                    return SubsampleRatio.Ratio410;
            }

            return SubsampleRatio.Ratio444;
        }
    }
}
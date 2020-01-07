namespace Pfim
{
    /// <summary>Contains additional info about the image</summary>
    public struct DdsLoadInfo
    {
        public ImageFormat Format { get; }
        public bool Compressed { get; }
        public bool Swap { get; }
        public bool Palette { get; }

        /// <summary>
        /// The length of a block is in pixels.
        /// This mainly affects compressed images as they are
        /// encoded in blocks that are divSize by divSize.
        /// Uncompressed DDS do not need this value.
        /// </summary>
        public uint DivSize { get; }

        /// <summary>
        /// Number of bytes needed to decode block of pixels
        /// that is divSize by divSize.  This takes into account
        /// how many bytes it takes to extract color and alpha information.
        /// Uncompressed DDS do not need this value.
        /// </summary>
        public uint BlockBytes { get; }

        public int Depth { get; }

        /// <summary>Initialize the load info structure</summary>
        public DdsLoadInfo(bool isCompresed, bool isSwap, bool isPalette, uint aDivSize, uint aBlockBytes, int aDepth, ImageFormat format)
        {
            Format = format;
            Compressed = isCompresed;
            Swap = isSwap;
            Palette = isPalette;
            DivSize = aDivSize;
            BlockBytes = aBlockBytes;
            Depth = aDepth;
        }
    }
}

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System.Collections.Generic;

    using ImageSharp.Memory;

    /// <summary>
    /// Defines a pair of huffman tables
    /// </summary>
    internal class HuffmanTables
    {
        /// <summary>
        /// Gets or sets the quantization tables.
        /// </summary>
        public Fast2DArray<HuffmanBranch> Tables { get; set; } = new Fast2DArray<HuffmanBranch>(256, 2);
    }

    internal struct HuffmanBranch
    {
        public HuffmanBranch(short value)
            : this(value, new List<HuffmanBranch>())
        {
        }

        public HuffmanBranch(List<HuffmanBranch> children)
            : this(0, children)
        {
        }

        private HuffmanBranch(short value, List<HuffmanBranch> children)
        {
            this.Index = 0;
            this.Value = value;
            this.Children = children;
        }

        public int Index;

        public short Value;

        public List<HuffmanBranch> Children;
    }
}

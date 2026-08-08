// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlAnsSymbolReader
{
    private const int MaxCheckpointInterval = 512;

    // Use class because the Lz77Window property uses 2KB memory
    private sealed class Checkpoint
    {
        public uint State { get; set; }

        public uint NumToCopy { get; set; }

        public uint CopyPos { get; set; }

        public uint NumDecoded { get; set; }

        public uint[] Lz77Window { get; set; } = new uint[MaxCheckpointInterval];
    }

    private readonly JxlAnsEntry[] aliasTables = [];
    private JxlHuffmanDecodingData huffmanData;
    private bool usePrefixCode;
    private uint state = AnsSignature << 16u;
}

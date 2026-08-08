// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlAnsCode
{
    public List<JxlHuffmanDecodingData> HuffmanData { get; set; } = [];

    public List<JxlAnsHybridUIntConfiguration> UIntConfig { get; set; } = [];

    public List<int> DegenerateSymbols { get; set; } = [];

    public bool UsePrefixCode { get; set; }

    public byte LogAlphaSize { get; set; }

    public JxlAnsLz77Parameters Lz77 { get; set; } = new();

    public int MaxNumBits { get; set; }

    public void UpdateMaxNumBits(int ctx, int symbol)
    {
        Span<JxlAnsHybridUIntConfiguration> configs = CollectionsMarshal.AsSpan(this.UIntConfig);
        ref JxlAnsHybridUIntConfiguration cfg = ref configs[ctx];
        if (this.Lz77.Enabled && this.Lz77.NonserializedDistanceContext != ctx && symbol >= this.Lz77.MinimumSymbol)
        {
            symbol -= (int)this.Lz77.MinimumSymbol;
            cfg = ref this.Lz77.GetLengthUIntConfigReference();
        }

        uint splitToken = cfg.SplitToken;
        uint msbInToken = cfg.MsbInToken;
        uint lsbInToken = cfg.LsbInToken;
        uint splitExponent = cfg.SplitExponent;

        if (symbol < splitToken)
        {
            this.MaxNumBits = Math.Max(this.MaxNumBits, (int)splitExponent);
            return;
        }

        uint nExtra = splitExponent - (msbInToken + lsbInToken) + (((uint)symbol - splitToken) >> (int)(msbInToken + lsbInToken));
        uint total = msbInToken + lsbInToken + nExtra + 1;
        this.MaxNumBits = Math.Max(this.MaxNumBits, (int)total);
    }
}

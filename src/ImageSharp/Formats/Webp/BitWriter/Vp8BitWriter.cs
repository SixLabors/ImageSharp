// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Webp.Lossy;

namespace SixLabors.ImageSharp.Formats.Webp.BitWriter;

/// <summary>
/// A bit writer for writing lossy webp streams.
/// </summary>
internal class Vp8BitWriter : BitWriterBase
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const int DC_PRED = 0;
    private const int TM_PRED = 1;
    private const int V_PRED = 2;
    private const int H_PRED = 3;

    // 4x4 modes
    private const int B_DC_PRED = 0;
    private const int B_TM_PRED = 1;
    private const int B_VE_PRED = 2;
    private const int B_HE_PRED = 3;
    private const int B_RD_PRED = 4;
    private const int B_VR_PRED = 5;
    private const int B_LD_PRED = 6;
    private const int B_VL_PRED = 7;
    private const int B_HD_PRED = 8;
    private const int B_HU_PRED = 9;
#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly Vp8Encoder enc;

    private int range;

    private int value;

    /// <summary>
    /// Number of outstanding bits.
    /// </summary>
    private int run;

    /// <summary>
    /// Number of pending bits.
    /// </summary>
    private int nbBits;

    private uint pos;

    private readonly int maxPos;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8BitWriter"/> class.
    /// </summary>
    /// <param name="expectedSize">The expected size in bytes.</param>
    /// <param name="enc">The Vp8Encoder.</param>
    public Vp8BitWriter(int expectedSize, Vp8Encoder enc)
        : base(expectedSize)
    {
        this.range = 255 - 1;
        this.value = 0;
        this.run = 0;
        this.nbBits = -8;
        this.pos = 0;
        this.maxPos = 0;

        this.enc = enc;
    }

    /// <inheritdoc/>
    public override int NumBytes => (int)this.pos;

    public int PutCoeffs(int ctx, Vp8Residual residual)
    {
        int n = residual.First;
        Vp8ProbaArray p = residual.Prob[n].Probabilities[ctx];
        if (!this.PutBit(residual.Last >= 0, p.Probabilities[0]))
        {
            return 0;
        }

        while (n < 16)
        {
            int c = residual.Coeffs[n++];
            bool sign = c < 0;
            int v = sign ? -c : c;
            if (!this.PutBit(v != 0, p.Probabilities[1]))
            {
                p = residual.Prob[WebpConstants.Vp8EncBands[n]].Probabilities[0];
                continue;
            }

            if (!this.PutBit(v > 1, p.Probabilities[2]))
            {
                p = residual.Prob[WebpConstants.Vp8EncBands[n]].Probabilities[1];
            }
            else
            {
                if (!this.PutBit(v > 4, p.Probabilities[3]))
                {
                    if (this.PutBit(v != 2, p.Probabilities[4]))
                    {
                        this.PutBit(v == 4, p.Probabilities[5]);
                    }
                }
                else if (!this.PutBit(v > 10, p.Probabilities[6]))
                {
                    if (!this.PutBit(v > 6, p.Probabilities[7]))
                    {
                        this.PutBit(v == 6, 159);
                    }
                    else
                    {
                        this.PutBit(v >= 9, 165);
                        this.PutBit((v & 1) == 0, 145);
                    }
                }
                else
                {
                    int mask;
                    byte[] tab;
                    if (v < 3 + (8 << 1))
                    {
                        // VP8Cat3  (3b)
                        this.PutBit(0, p.Probabilities[8]);
                        this.PutBit(0, p.Probabilities[9]);
                        v -= 3 + (8 << 0);
                        mask = 1 << 2;
                        tab = WebpConstants.Cat3;
                    }
                    else if (v < 3 + (8 << 2))
                    {
                        // VP8Cat4  (4b)
                        this.PutBit(0, p.Probabilities[8]);
                        this.PutBit(1, p.Probabilities[9]);
                        v -= 3 + (8 << 1);
                        mask = 1 << 3;
                        tab = WebpConstants.Cat4;
                    }
                    else if (v < 3 + (8 << 3))
                    {
                        // VP8Cat5  (5b)
                        this.PutBit(1, p.Probabilities[8]);
                        this.PutBit(0, p.Probabilities[10]);
                        v -= 3 + (8 << 2);
                        mask = 1 << 4;
                        tab = WebpConstants.Cat5;
                    }
                    else
                    {
                        // VP8Cat6 (11b)
                        this.PutBit(1, p.Probabilities[8]);
                        this.PutBit(1, p.Probabilities[10]);
                        v -= 3 + (8 << 3);
                        mask = 1 << 10;
                        tab = WebpConstants.Cat6;
                    }

                    int tabIdx = 0;
                    while (mask != 0)
                    {
                        this.PutBit(v & mask, tab[tabIdx++]);
                        mask >>= 1;
                    }
                }

                p = residual.Prob[WebpConstants.Vp8EncBands[n]].Probabilities[2];
            }

            this.PutBitUniform(sign ? 1 : 0);
            if (n == 16 || !this.PutBit(n <= residual.Last, p.Probabilities[0]))
            {
                return 1;   // EOB
            }
        }

        return 1;
    }

    /// <summary>
    /// Resizes the buffer to write to.
    /// </summary>
    /// <param name="extraSize">The extra size in bytes needed.</param>
    public override void BitWriterResize(int extraSize)
    {
        long neededSize = this.pos + extraSize;
        if (neededSize <= this.maxPos)
        {
            return;
        }

        this.ResizeBuffer(this.maxPos, (int)neededSize);
    }

    /// <inheritdoc/>
    public override void Finish()
    {
        this.PutBits(0, 9 - this.nbBits);
        this.nbBits = 0;   // pad with zeroes.
        this.Flush();
    }

    public void PutSegment(int s, Span<byte> p)
    {
        if (this.PutBit(s >= 2, p[0]))
        {
            p = p[1..];
        }

        this.PutBit(s & 1, p[1]);
    }

    public void PutI16Mode(int mode)
    {
        if (this.PutBit(mode is TM_PRED or H_PRED, 156))
        {
            this.PutBit(mode == TM_PRED, 128);    // TM or HE
        }
        else
        {
            this.PutBit(mode == V_PRED, 163);     // VE or DC
        }
    }

    public int PutI4Mode(int mode, Span<byte> prob)
    {
        if (this.PutBit(mode != B_DC_PRED, prob[0]))
        {
            if (this.PutBit(mode != B_TM_PRED, prob[1]))
            {
                if (this.PutBit(mode != B_VE_PRED, prob[2]))
                {
                    if (!this.PutBit(mode >= B_LD_PRED, prob[3]))
                    {
                        if (this.PutBit(mode != B_HE_PRED, prob[4]))
                        {
                            this.PutBit(mode != B_RD_PRED, prob[5]);
                        }
                    }
                    else
                    {
                        if (this.PutBit(mode != B_LD_PRED, prob[6]))
                        {
                            if (this.PutBit(mode != B_VL_PRED, prob[7]))
                            {
                                this.PutBit(mode != B_HD_PRED, prob[8]);
                            }
                        }
                    }
                }
            }
        }

        return mode;
    }

    public void PutUvMode(int uvMode)
    {
        // DC_PRED
        if (this.PutBit(uvMode != DC_PRED, 142))
        {
            // V_PRED
            if (this.PutBit(uvMode != V_PRED, 114))
            {
                // H_PRED
                this.PutBit(uvMode != H_PRED, 183);
            }
        }
    }

    private void PutBits(uint value, int nbBits)
    {
        for (uint mask = 1u << (nbBits - 1); mask != 0; mask >>= 1)
        {
            this.PutBitUniform((int)(value & mask));
        }
    }

    private bool PutBit(bool bit, int prob) => this.PutBit(bit ? 1 : 0, prob);

    private bool PutBit(int bit, int prob)
    {
        int split = (this.range * prob) >> 8;
        if (bit != 0)
        {
            this.value += split + 1;
            this.range -= split + 1;
        }
        else
        {
            this.range = split;
        }

        if (this.range < 127)
        {
            // emit 'shift' bits out and renormalize.
            int shift = WebpLookupTables.Norm[this.range];
            this.range = WebpLookupTables.NewRange[this.range];
            this.value <<= shift;
            this.nbBits += shift;
            if (this.nbBits > 0)
            {
                this.Flush();
            }
        }

        return bit != 0;
    }

    private int PutBitUniform(int bit)
    {
        int split = this.range >> 1;
        if (bit != 0)
        {
            this.value += split + 1;
            this.range -= split + 1;
        }
        else
        {
            this.range = split;
        }

        if (this.range < 127)
        {
            this.range = WebpLookupTables.NewRange[this.range];
            this.value <<= 1;
            this.nbBits += 1;
            if (this.nbBits > 0)
            {
                this.Flush();
            }
        }

        return bit;
    }

    private void PutSignedBits(int value, int nbBits)
    {
        if (this.PutBitUniform(value != 0 ? 1 : 0) == 0)
        {
            return;
        }

        if (value < 0)
        {
            int valueToWrite = (-value << 1) | 1;
            this.PutBits((uint)valueToWrite, nbBits + 1);
        }
        else
        {
            this.PutBits((uint)(value << 1), nbBits + 1);
        }
    }

    private void Flush()
    {
        int s = 8 + this.nbBits;
        int bits = this.value >> s;
        this.value -= bits << s;
        this.nbBits -= 8;
        if ((bits & 0xff) != 0xff)
        {
            uint pos = this.pos;
            this.BitWriterResize(this.run + 1);

            if ((bits & 0x100) != 0)
            {
                // overflow -> propagate carry over pending 0xff's
                if (pos > 0)
                {
                    this.Buffer[pos - 1]++;
                }
            }

            if (this.run > 0)
            {
                int value = (bits & 0x100) != 0 ? 0x00 : 0xff;
                for (; this.run > 0; --this.run)
                {
                    this.Buffer[pos++] = (byte)value;
                }
            }

            this.Buffer[pos++] = (byte)(bits & 0xff);
            this.pos = pos;
        }
        else
        {
            this.run++;   // Delay writing of bytes 0xff, pending eventual carry.
        }
    }

    /// <inheritdoc />
    public override void WriteEncodedImageToStream(Stream stream)
    {
        uint numBytes = (uint)this.NumBytes;

        int mbSize = this.enc.Mbw * this.enc.Mbh;
        int expectedSize = (int)((uint)mbSize * 7 / 8);

        Vp8BitWriter bitWriterPartZero = new(expectedSize, this.enc);

        // Partition #0 with header and partition sizes.
        uint size0 = bitWriterPartZero.GeneratePartition0();

        uint vp8Size = WebpConstants.Vp8FrameHeaderSize + size0;
        vp8Size += numBytes;
        uint pad = vp8Size & 1;
        vp8Size += pad;

        // Emit header and partition #0
        this.WriteVp8Header(stream, vp8Size);
        this.WriteFrameHeader(stream, size0);

        bitWriterPartZero.WriteToStream(stream);

        // Write the encoded image to the stream.
        this.WriteToStream(stream);
        if (pad == 1)
        {
            stream.WriteByte(0);
        }
    }

    private uint GeneratePartition0()
    {
        this.PutBitUniform(0); // colorspace
        this.PutBitUniform(0); // clamp type

        this.WriteSegmentHeader();
        this.WriteFilterHeader();

        this.PutBits(0, 2);

        this.WriteQuant();
        this.PutBitUniform(0);
        this.WriteProbas();
        this.CodeIntraModes();

        this.Finish();

        return (uint)this.NumBytes;
    }

    private void WriteSegmentHeader()
    {
        Vp8EncSegmentHeader hdr = this.enc.SegmentHeader;
        Vp8EncProba proba = this.enc.Proba;
        if (this.PutBitUniform(hdr.NumSegments > 1 ? 1 : 0) != 0)
        {
            // We always 'update' the quant and filter strength values.
            int updateData = 1;
            this.PutBitUniform(hdr.UpdateMap ? 1 : 0);
            if (this.PutBitUniform(updateData) != 0)
            {
                // We always use absolute values, not relative ones.
                this.PutBitUniform(1); // (segment_feature_mode = 1. Paragraph 9.3.)
                for (int s = 0; s < WebpConstants.NumMbSegments; ++s)
                {
                    this.PutSignedBits(this.enc.SegmentInfos[s].Quant, 7);
                }

                for (int s = 0; s < WebpConstants.NumMbSegments; ++s)
                {
                    this.PutSignedBits(this.enc.SegmentInfos[s].FStrength, 6);
                }
            }

            if (hdr.UpdateMap)
            {
                for (int s = 0; s < 3; ++s)
                {
                    if (this.PutBitUniform(proba.Segments[s] != 255 ? 1 : 0) != 0)
                    {
                        this.PutBits(proba.Segments[s], 8);
                    }
                }
            }
        }
    }

    private void WriteFilterHeader()
    {
        Vp8FilterHeader hdr = this.enc.FilterHeader;
        bool useLfDelta = hdr.I4x4LfDelta != 0;
        this.PutBitUniform(hdr.Simple ? 1 : 0);
        this.PutBits((uint)hdr.FilterLevel, 6);
        this.PutBits((uint)hdr.Sharpness, 3);
        if (this.PutBitUniform(useLfDelta ? 1 : 0) != 0)
        {
            // '0' is the default value for i4x4LfDelta at frame #0.
            bool needUpdate = hdr.I4x4LfDelta != 0;
            if (this.PutBitUniform(needUpdate ? 1 : 0) != 0)
            {
                // we don't use refLfDelta => emit four 0 bits.
                this.PutBits(0, 4);

                // we use modeLfDelta for i4x4
                this.PutSignedBits(hdr.I4x4LfDelta, 6);
                this.PutBits(0, 3);    // all others unused.
            }
        }
    }

    // Nominal quantization parameters
    private void WriteQuant()
    {
        this.PutBits((uint)this.enc.BaseQuant, 7);
        this.PutSignedBits(this.enc.DqY1Dc, 4);
        this.PutSignedBits(this.enc.DqY2Dc, 4);
        this.PutSignedBits(this.enc.DqY2Ac, 4);
        this.PutSignedBits(this.enc.DqUvDc, 4);
        this.PutSignedBits(this.enc.DqUvAc, 4);
    }

    private void WriteProbas()
    {
        Vp8EncProba probas = this.enc.Proba;
        for (int t = 0; t < WebpConstants.NumTypes; ++t)
        {
            for (int b = 0; b < WebpConstants.NumBands; ++b)
            {
                for (int c = 0; c < WebpConstants.NumCtx; ++c)
                {
                    for (int p = 0; p < WebpConstants.NumProbas; ++p)
                    {
                        byte p0 = probas.Coeffs[t][b].Probabilities[c].Probabilities[p];
                        bool update = p0 != WebpLookupTables.DefaultCoeffsProba[t, b, c, p];
                        if (this.PutBit(update, WebpLookupTables.CoeffsUpdateProba[t, b, c, p]))
                        {
                            this.PutBits(p0, 8);
                        }
                    }
                }
            }
        }

        if (this.PutBitUniform(probas.UseSkipProba ? 1 : 0) != 0)
        {
            this.PutBits(probas.SkipProba, 8);
        }
    }

    // Writes the partition #0 modes (that is: all intra modes)
    private void CodeIntraModes()
    {
        Vp8EncIterator it = new(this.enc);
        int predsWidth = this.enc.PredsWidth;

        do
        {
            Vp8MacroBlockInfo mb = it.CurrentMacroBlockInfo;
            int predIdx = it.PredIdx;
            Span<byte> preds = it.Preds.AsSpan(predIdx);
            if (this.enc.SegmentHeader.UpdateMap)
            {
                this.PutSegment(mb.Segment, this.enc.Proba.Segments);
            }

            if (this.enc.Proba.UseSkipProba)
            {
                this.PutBit(mb.Skip, this.enc.Proba.SkipProba);
            }

            if (this.PutBit(mb.MacroBlockType != 0, 145))
            {
                // i16x16
                this.PutI16Mode(preds[0]);
            }
            else
            {
                Span<byte> topPred = it.Preds.AsSpan(predIdx - predsWidth);
                for (int y = 0; y < 4; y++)
                {
                    int left = it.Preds[predIdx - 1];
                    for (int x = 0; x < 4; x++)
                    {
                        byte[] probas = WebpLookupTables.ModesProba[topPred[x], left];
                        left = this.PutI4Mode(it.Preds[predIdx + x], probas);
                    }

                    topPred = it.Preds.AsSpan(predIdx);
                    predIdx += predsWidth;
                }
            }

            this.PutUvMode(mb.UvMode);
        }
        while (it.Next());
    }

    private void WriteVp8Header(Stream stream, uint size)
    {
        Span<byte> buf = stackalloc byte[WebpConstants.TagSize];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)WebpChunkType.Vp8);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, size);
        stream.Write(buf);
    }

    private void WriteFrameHeader(Stream stream, uint size0)
    {
        uint profile = 0;
        int width = this.enc.Width;
        int height = this.enc.Height;
        Span<byte> vp8FrameHeader = stackalloc byte[WebpConstants.Vp8FrameHeaderSize];

        // Paragraph 9.1.
        uint bits = 0 // keyframe (1b)
                    | (profile << 1) // profile (3b)
                    | (1 << 4) // visible (1b)
                    | (size0 << 5); // partition length (19b)

        vp8FrameHeader[0] = (byte)((bits >> 0) & 0xff);
        vp8FrameHeader[1] = (byte)((bits >> 8) & 0xff);
        vp8FrameHeader[2] = (byte)((bits >> 16) & 0xff);

        // signature
        vp8FrameHeader[3] = WebpConstants.Vp8HeaderMagicBytes[0];
        vp8FrameHeader[4] = WebpConstants.Vp8HeaderMagicBytes[1];
        vp8FrameHeader[5] = WebpConstants.Vp8HeaderMagicBytes[2];

        // dimensions
        vp8FrameHeader[6] = (byte)(width & 0xff);
        vp8FrameHeader[7] = (byte)(width >> 8);
        vp8FrameHeader[8] = (byte)(height & 0xff);
        vp8FrameHeader[9] = (byte)(height >> 8);

        stream.Write(vp8FrameHeader);
    }
}

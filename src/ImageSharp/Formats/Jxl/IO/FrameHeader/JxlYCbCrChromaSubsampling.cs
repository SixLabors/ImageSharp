// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Gets the Y'Cb'Cr chroma subsampling information as part
/// of the JPEG XL Frame Header.
/// </summary>
internal sealed class JxlYCbCrChromaSubsampling : IJxlFields
{
    private readonly int[] channelMode = new int[3];

    private static ReadOnlySpan<byte> HShiftData => [0, 1, 1, 0];

    private static ReadOnlySpan<byte> VShiftData => [0, 1, 0, 1];

    public byte MaxHShift { get; private set; }

    public byte MaxVShift { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this 4:4:4 chroma subsampling.
    /// </summary>
    public bool Is444 =>
            this.HShift(0) == 0 && this.VShift(0) == 0 && // Cb
            this.HShift(2) == 0 && this.VShift(2) == 0 && // Cr
            this.HShift(1) == 0 && this.VShift(1) == 0;   // Y;

    /// <summary>
    /// Gets a value indicating whether this 4:2:0 chroma subsampling.
    /// </summary>
    public bool Is420 =>
            this.HShift(0) == 1 && this.VShift(0) == 1 && // Cb
            this.HShift(2) == 1 && this.VShift(2) == 1 && // Cr
            this.HShift(1) == 0 && this.VShift(1) == 0;   // Y

    /// <summary>
    /// Gets a value indicating whether this 4:2:2 chroma subsampling.
    /// </summary>
    public bool Is422 =>
            this.HShift(0) == 1 && this.VShift(0) == 0 && // Cb
            this.HShift(2) == 1 && this.VShift(2) == 0 && // Cr
            this.HShift(1) == 0 && this.VShift(1) == 0;   // Y

    /// <summary>
    /// Gets a value indicating whether this 4:4:0 chroma subsampling.
    /// </summary>
    public bool Is440 =>
            this.HShift(0) == 0 && this.VShift(0) == 1 && // Cb
            this.HShift(2) == 0 && this.VShift(2) == 1 && // Cr
            this.HShift(1) == 0 && this.VShift(1) == 0;    // Y

    public byte RawHShift(int c) => HShiftData[this.channelMode[c]];

    public byte RawVShift(int c) => VShiftData[this.channelMode[c]];

    public byte HShift(int c) => (byte)(this.MaxHShift - HShiftData[this.channelMode[c]]);

    public byte VShift(int c) => (byte)(this.MaxVShift - VShiftData[this.channelMode[c]]);

    private void Recompute()
    {
        this.MaxHShift = 0;
        this.MaxVShift = 0;

        for (int i = 0; i < 3; i++)
        {
            int ch = this.channelMode[i];

            this.MaxHShift = Math.Max(this.MaxHShift, HShiftData[ch]);
            this.MaxVShift = Math.Max(this.MaxVShift, VShiftData[ch]);
        }
    }

    public bool Set(ReadOnlySpan<byte> hsample, ReadOnlySpan<byte> vsample)
    {
        for (int c = 0; c < 3; c++)
        {
            int cjpeg = c < 2 ? (c ^ 1) : c;
            int i = 0;

            for (; i < 4; i++)
            {
                if (1 << HShiftData[i] == hsample[cjpeg] && 1 << VShiftData[i] == vsample[cjpeg])
                {
                    this.channelMode[c] = i;
                    break;
                }
            }

            if (i == 4)
            {
                return false;
            }
        }

        this.Recompute();
        return true;
    }

    public override string ToString()
    {
        if (this.Is444)
        {
            return "4:4:4";
        }
        else if (this.Is420)
        {
            return "4:2:0";
        }
        else if (this.Is422)
        {
            return "4:2:2";
        }
        else if (this.Is440)
        {
            return "4:4:0";
        }
        else
        {
            return $"[Custom] {this.channelMode[0]}:{this.channelMode[1]}:{this.channelMode[2]}";
        }
    }

    public bool Visit(JxlVisitor visitor)
    {
        for (int i = 0; i < 3; i++)
        {
            int channel = this.channelMode[i];

            uint unsignedChannel = (uint)channel;
            bool wroteSuccessfully = visitor.Bits(2, 0, ref unsignedChannel);

            if (!wroteSuccessfully)
            {
                return false;
            }

            this.channelMode[i] = (int)unsignedChannel;
        }

        return true;
    }
}

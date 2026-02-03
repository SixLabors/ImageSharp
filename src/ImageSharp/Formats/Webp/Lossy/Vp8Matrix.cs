// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal unsafe struct Vp8Matrix
{
    // [luma-ac,luma-dc,chroma][dc,ac]
    private static readonly int[][] BiasMatrices =
    [
        [96, 110],
        [96, 108],
        [110, 115]
    ];

    /// <summary>
    /// Number of descaling bits for sharpening bias.
    /// </summary>
    private const int SharpenBits = 11;

    /// <summary>
    /// The quantizer steps.
    /// </summary>
    public fixed ushort Q[16];

    /// <summary>
    /// The reciprocals, fixed point.
    /// </summary>
    public fixed ushort IQ[16];

    /// <summary>
    /// The rounding bias.
    /// </summary>
    public fixed uint Bias[16];

    /// <summary>
    /// The value below which a coefficient is zeroed.
    /// </summary>
    public fixed uint ZThresh[16];

    /// <summary>
    /// The frequency boosters for slight sharpening.
    /// </summary>
    public fixed short Sharpen[16];

    // Sharpening by (slightly) raising the hi-frequency coeffs.
    // Hack-ish but helpful for mid-bitrate range. Use with care.
    // This uses C#'s optimization to refer to the static data segment of the assembly, no allocation occurs.
    private static ReadOnlySpan<byte> FreqSharpening => [0, 30, 60, 90, 30, 60, 90, 90, 60, 90, 90, 90, 90, 90, 90, 90];

    /// <summary>
    /// Returns the average quantizer.
    /// </summary>
    /// <returns>The average quantizer.</returns>
    public int Expand(int type)
    {
        int sum;
        int i;
        for (i = 0; i < 2; i++)
        {
            int isAcCoeff = i > 0 ? 1 : 0;
            int bias = BiasMatrices[type][isAcCoeff];
            this.IQ[i] = (ushort)((1 << WebpConstants.QFix) / this.Q[i]);
            this.Bias[i] = (uint)BIAS(bias);

            // zthresh is the exact value such that QUANTDIV(coeff, iQ, B) is:
            //   * zero if coeff <= zthresh
            //   * non-zero if coeff > zthresh
            this.ZThresh[i] = ((1 << WebpConstants.QFix) - 1 - this.Bias[i]) / this.IQ[i];
        }

        for (i = 2; i < 16; i++)
        {
            this.Q[i] = this.Q[1];
            this.IQ[i] = this.IQ[1];
            this.Bias[i] = this.Bias[1];
            this.ZThresh[i] = this.ZThresh[1];
        }

        for (sum = 0, i = 0; i < 16; i++)
        {
            if (type == 0)
            {
                // We only use sharpening for AC luma coeffs.
                this.Sharpen[i] = (short)((FreqSharpening[i] * this.Q[i]) >> SharpenBits);
            }
            else
            {
                this.Sharpen[i] = 0;
            }

            sum += this.Q[i];
        }

        return (sum + 8) >> 4;
    }

    private static int BIAS(int b) => b << (WebpConstants.QFix - 8);
}

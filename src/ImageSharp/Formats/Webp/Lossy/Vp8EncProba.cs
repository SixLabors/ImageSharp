// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal class Vp8EncProba
{
    /// <summary>
    /// Last (inclusive) level with variable cost.
    /// </summary>
    private const int MaxVariableLevel = 67;

    /// <summary>
    /// Value below which using skipProba is OK.
    /// </summary>
    private const int SkipProbaThreshold = 250;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8EncProba"/> class.
    /// </summary>
    public Vp8EncProba()
    {
        this.Dirty = true;
        this.UseSkipProba = false;
        this.Segments = new byte[3];
        this.Coeffs = new Vp8BandProbas[WebpConstants.NumTypes][];
        for (int i = 0; i < this.Coeffs.Length; i++)
        {
            this.Coeffs[i] = new Vp8BandProbas[WebpConstants.NumBands];
            for (int j = 0; j < this.Coeffs[i].Length; j++)
            {
                this.Coeffs[i][j] = new();
            }
        }

        this.Stats = new Vp8Stats[WebpConstants.NumTypes][];
        for (int i = 0; i < this.Coeffs.Length; i++)
        {
            this.Stats[i] = new Vp8Stats[WebpConstants.NumBands];
            for (int j = 0; j < this.Stats[i].Length; j++)
            {
                this.Stats[i][j] = new();
            }
        }

        this.LevelCost = new Vp8Costs[WebpConstants.NumTypes][];
        for (int i = 0; i < this.LevelCost.Length; i++)
        {
            this.LevelCost[i] = new Vp8Costs[WebpConstants.NumBands];
            for (int j = 0; j < this.LevelCost[i].Length; j++)
            {
                this.LevelCost[i][j] = new();
            }
        }

        this.RemappedCosts = new Vp8Costs[WebpConstants.NumTypes][];
        for (int i = 0; i < this.RemappedCosts.Length; i++)
        {
            this.RemappedCosts[i] = new Vp8Costs[16];
            for (int j = 0; j < this.RemappedCosts[i].Length; j++)
            {
                this.RemappedCosts[i][j] = new();
            }
        }

        // Initialize with default probabilities.
        this.Segments.AsSpan().Fill(255);
        for (int t = 0; t < WebpConstants.NumTypes; ++t)
        {
            for (int b = 0; b < WebpConstants.NumBands; ++b)
            {
                for (int c = 0; c < WebpConstants.NumCtx; ++c)
                {
                    Vp8ProbaArray dst = this.Coeffs[t][b].Probabilities[c];
                    for (int p = 0; p < WebpConstants.NumProbas; ++p)
                    {
                        dst.Probabilities[p] = WebpLookupTables.DefaultCoeffsProba[t, b, c, p];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the probabilities for segment tree.
    /// </summary>
    public byte[] Segments { get; }

    /// <summary>
    /// Gets or sets the final probability of being skipped.
    /// </summary>
    public byte SkipProba { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the skip probability.
    /// </summary>
    public bool UseSkipProba { get; set; }

    public Vp8BandProbas[][] Coeffs { get; }

    public Vp8Stats[][] Stats { get; }

    public Vp8Costs[][] LevelCost { get; }

    public Vp8Costs[][] RemappedCosts { get; }

    /// <summary>
    /// Gets or sets the number of skipped blocks.
    /// </summary>
    public int NbSkip { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CalculateLevelCosts() needs to be called.
    /// </summary>
    public bool Dirty { get; set; }

    public void CalculateLevelCosts()
    {
        if (!this.Dirty)
        {
            return; // Nothing to do.
        }

        for (int ctype = 0; ctype < WebpConstants.NumTypes; ++ctype)
        {
            for (int band = 0; band < WebpConstants.NumBands; ++band)
            {
                for (int ctx = 0; ctx < WebpConstants.NumCtx; ++ctx)
                {
                    Vp8ProbaArray p = this.Coeffs[ctype][band].Probabilities[ctx];
                    Vp8CostArray table = this.LevelCost[ctype][band].Costs[ctx];
                    int cost0 = ctx > 0 ? LossyUtils.Vp8BitCost(1, p.Probabilities[0]) : 0;
                    int costBase = LossyUtils.Vp8BitCost(1, p.Probabilities[1]) + cost0;
                    int v;
                    table.Costs[0] = (ushort)(LossyUtils.Vp8BitCost(0, p.Probabilities[1]) + cost0);
                    for (v = 1; v <= MaxVariableLevel; ++v)
                    {
                        table.Costs[v] = (ushort)(costBase + VariableLevelCost(v, p.Probabilities));
                    }

                    // Starting at level 67 and up, the variable part of the cost is actually constant
                }
            }

            for (int n = 0; n < 16; ++n)
            {
                for (int ctx = 0; ctx < WebpConstants.NumCtx; ++ctx)
                {
                    Vp8CostArray dst = this.RemappedCosts[ctype][n].Costs[ctx];
                    Vp8CostArray src = this.LevelCost[ctype][WebpConstants.Vp8EncBands[n]].Costs[ctx];
                    src.Costs.CopyTo(dst.Costs.AsSpan());
                }
            }
        }

        this.Dirty = false;
    }

    public int FinalizeTokenProbas()
    {
        bool hasChanged = false;
        int size = 0;
        for (int t = 0; t < WebpConstants.NumTypes; ++t)
        {
            for (int b = 0; b < WebpConstants.NumBands; ++b)
            {
                for (int c = 0; c < WebpConstants.NumCtx; ++c)
                {
                    for (int p = 0; p < WebpConstants.NumProbas; ++p)
                    {
                        uint stats = this.Stats[t][b].Stats[c].Stats[p];
                        int nb = (int)((stats >> 0) & 0xffff);
                        int total = (int)((stats >> 16) & 0xffff);
                        int updateProba = WebpLookupTables.CoeffsUpdateProba[t, b, c, p];
                        int oldP = WebpLookupTables.DefaultCoeffsProba[t, b, c, p];
                        int newP = CalcTokenProba(nb, total);
                        int oldCost = BranchCost(nb, total, oldP) + LossyUtils.Vp8BitCost(0, (byte)updateProba);
                        int newCost = BranchCost(nb, total, newP) + LossyUtils.Vp8BitCost(1, (byte)updateProba) + (8 * 256);
                        bool useNewP = oldCost > newCost;
                        size += LossyUtils.Vp8BitCost(useNewP ? 1 : 0, (byte)updateProba);
                        if (useNewP)
                        {
                            // Only use proba that seem meaningful enough.
                            this.Coeffs[t][b].Probabilities[c].Probabilities[p] = (byte)newP;
                            hasChanged |= newP != oldP;
                            size += 8 * 256;
                        }
                        else
                        {
                            this.Coeffs[t][b].Probabilities[c].Probabilities[p] = (byte)oldP;
                        }
                    }
                }
            }
        }

        this.Dirty = hasChanged;
        return size;
    }

    public int FinalizeSkipProba(int mbw, int mbh)
    {
        int nbMbs = mbw * mbh;
        int nbEvents = this.NbSkip;
        this.SkipProba = (byte)CalcSkipProba(nbEvents, nbMbs);
        this.UseSkipProba = this.SkipProba < SkipProbaThreshold;

        int size = 256;
        if (this.UseSkipProba)
        {
            size += (nbEvents * LossyUtils.Vp8BitCost(1, this.SkipProba)) + ((nbMbs - nbEvents) * LossyUtils.Vp8BitCost(0, this.SkipProba));
            size += 8 * 256;   // cost of signaling the skipProba itself.
        }

        return size;
    }

    public void ResetTokenStats()
    {
        for (int t = 0; t < WebpConstants.NumTypes; ++t)
        {
            for (int b = 0; b < WebpConstants.NumBands; ++b)
            {
                for (int c = 0; c < WebpConstants.NumCtx; ++c)
                {
                    for (int p = 0; p < WebpConstants.NumProbas; ++p)
                    {
                        this.Stats[t][b].Stats[c].Stats[p] = 0;
                    }
                }
            }
        }
    }

    private static int CalcSkipProba(long nb, long total) => (int)(total != 0 ? (total - nb) * 255 / total : 255);

    private static int VariableLevelCost(int level, Span<byte> probas)
    {
        int pattern = WebpLookupTables.Vp8LevelCodes[level - 1][0];
        int bits = WebpLookupTables.Vp8LevelCodes[level - 1][1];
        int cost = 0;
        for (int i = 2; pattern != 0; i++)
        {
            if ((pattern & 1) != 0)
            {
                cost += LossyUtils.Vp8BitCost(bits & 1, probas[i]);
            }

            bits >>= 1;
            pattern >>= 1;
        }

        return cost;
    }

    // Collect statistics and deduce probabilities for next coding pass.
    // Return the total bit-cost for coding the probability updates.
    private static int CalcTokenProba(int nb, int total) => nb != 0 ? (255 - (nb * 255 / total)) : 255;

    // Cost of coding 'nb' 1's and 'total-nb' 0's using 'proba' probability.
    private static int BranchCost(int nb, int total, int proba) => (nb * LossyUtils.Vp8BitCost(1, (byte)proba)) + ((total - nb) * LossyUtils.Vp8BitCost(0, (byte)proba));
}

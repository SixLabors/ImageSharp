// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

/// <summary>
/// ANS histogram.
/// </summary>
internal sealed class JxlHistogram(int length)
{
    public List<int> Counts { get; set; } = new(length);

    public int TotalCount { get; set; }

    public float Entropy { get; set; }

    /// <summary>
    /// Resets all values to their defaults.
    /// </summary>
    public void Clear()
    {
        this.Counts.Clear();
        this.TotalCount = 0;
        this.Entropy = 0f;
    }

    /// <summary>
    /// Adds a new symbol.
    /// </summary>
    /// <param name="symbol">
    /// Index of the symbol to be added or, if it already
    /// exists, incremented.
    /// </param>
    public void Add(int symbol)
    {
        // Just to be careful here. If the symbol is too large,
        // this can allocate a lot of memory.
        DebugGuard.MustBeLessThan(symbol, 1_000_000, nameof(symbol));

        _ = this.Counts.EnsureCapacity(symbol);
        this.Counts[symbol]++;
        this.TotalCount++;
    }

    /// <summary>
    /// Increments the specified symbol. This is equivalent to
    /// <see cref="Add(int)"/> but without any checks, like ensuring capacity.
    /// </summary>
    /// <param name="symbol">Index of the symbol.</param>
    public void FastAdd(int symbol) => this.Counts[symbol]++;

    /// <summary>
    /// Adds the counts of the specified histogram to the current histogram.
    /// </summary>
    /// <param name="other">A specified histogram to add to this histogram.</param>
    public void AddHistogram(JxlHistogram other)
    {
        _ = this.Counts.EnsureCapacity(other.Counts.Count);

        for (int i = 0; i < other.Counts.Count; i++)
        {
            this.Counts[i] += other.Counts[i];
        }

        this.TotalCount += other.TotalCount;
    }

    /// <summary>
    /// Calculates the alphabet size.
    /// </summary>
    /// <returns>The alphabet size.</returns>
    public int GetAlphabetSize()
    {
        for (int i = this.Counts.Count - 1; i >= 0; i--)
        {
            if (this.Counts[i] > 0)
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// Finds the largest symbol.
    /// </summary>
    /// <returns>Largest symbol in the histogram.</returns>
    public int GetMaxSymbol()
    {
        if (this.TotalCount == 0)
        {
            return 0;
        }

        for (int i = this.Counts.Count - 1; i > 0; i--)
        {
            if (this.Counts[i] != 0)
            {
                return i;
            }
        }

        return 0;
    }
}

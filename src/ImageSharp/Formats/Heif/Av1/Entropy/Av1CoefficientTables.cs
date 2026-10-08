// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Holds the rate tables and the scratch storage that the coefficient search reads for every transform block. A
/// search loop reads them once from <see cref="Av1SymbolEncoder.GetCoefficientTables"/> and passes them to each trial,
/// so no trial reads the encoder buffers again.
/// </summary>
internal readonly ref struct Av1CoefficientTables
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CoefficientTables"/> struct.
    /// </summary>
    /// <param name="entropyWorkspace">The rate workspace of the encoder: mode rates, then coefficient rates, then contexts.</param>
    /// <param name="levelStorage">All of the level storage of the encoder.</param>
    public Av1CoefficientTables(Span<int> entropyWorkspace, Span<byte> levelStorage)
    {
        this.ModeCosts = new Av1ModeCosts(entropyWorkspace[..Av1ModeCosts.StorageLength]);
        this.CoefficientCosts = new Av1CoefficientCosts(entropyWorkspace.Slice(Av1ModeCosts.StorageLength, Av1CoefficientCosts.StorageLength));
        this.Contexts = MemoryMarshal.Cast<int, sbyte>(entropyWorkspace[(Av1ModeCosts.StorageLength + Av1CoefficientCosts.StorageLength)..]);
        this.LevelStorage = levelStorage;
    }

    /// <summary>
    /// Gets the mode rates.
    /// </summary>
    public Av1ModeCosts ModeCosts { get; }

    /// <summary>
    /// Gets the coefficient rates.
    /// </summary>
    public Av1CoefficientCosts CoefficientCosts { get; }

    /// <summary>
    /// Gets the scratch storage for the coefficient contexts of one transform block.
    /// </summary>
    public Span<sbyte> Contexts { get; }

    /// <summary>
    /// Gets all of the level storage.
    /// </summary>
    public Span<byte> LevelStorage { get; }
}

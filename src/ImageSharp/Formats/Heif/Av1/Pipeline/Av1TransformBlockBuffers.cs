// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Holds the workspace buffers and the rate tables that every lossy transform encode uses. A caller builds it once
/// before its loop, so no transform block reads the workspace or the writer buffers again.
/// </summary>
internal readonly ref struct Av1TransformBlockBuffers
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformBlockBuffers"/> struct.
    /// </summary>
    /// <param name="workspace">The block workspace, which owns the transform buffers and the quantizer matrices.</param>
    /// <param name="writer">The tile symbol encoder, which owns the rate tables.</param>
    public Av1TransformBlockBuffers(Av1EncoderBlockWorkspace workspace, Av1SymbolEncoder writer)
    {
        this.Workspace = workspace;
        this.Writer = writer;
        this.Tables = writer.GetCoefficientTables();
        this.TransformCoefficients = workspace.TransformCoefficients;
        this.DequantizedCoefficients = workspace.DequantizedCoefficients;
        this.TransformWorkspace = workspace.TransformWorkspace;
        this.Residual = workspace.Residual;
        this.SearchCoefficients = workspace.SearchCoefficients;
        this.SearchDequantizedCoefficients = workspace.SearchDequantizedCoefficients;
        this.SearchReconstructions = workspace.SearchReconstructions;
    }

    /// <summary>
    /// Gets the block workspace, which holds the quantizer matrices and the coding stage.
    /// </summary>
    public Av1EncoderBlockWorkspace Workspace { get; }

    /// <summary>
    /// Gets the tile symbol encoder that prices the coefficients.
    /// </summary>
    public Av1SymbolEncoder Writer { get; }

    /// <summary>
    /// Gets the rate tables and scratch storage of the writer.
    /// </summary>
    public Av1CoefficientTables Tables { get; }

    /// <summary>
    /// Gets the forward transform output buffer.
    /// </summary>
    public Span<int> TransformCoefficients { get; }

    /// <summary>
    /// Gets the dequantized coefficient buffer.
    /// </summary>
    public Span<int> DequantizedCoefficients { get; }

    /// <summary>
    /// Gets the intermediate buffer of the transforms.
    /// </summary>
    public Span<int> TransformWorkspace { get; }

    /// <summary>
    /// Gets the residual buffer of one transform block.
    /// </summary>
    public Span<short> Residual { get; }

    /// <summary>
    /// Gets the quantized coefficients of the best transform type of a type search.
    /// </summary>
    public Span<int> SearchCoefficients { get; }

    /// <summary>
    /// Gets the dequantized coefficients of the best transform type of a type search.
    /// </summary>
    public Span<int> SearchDequantizedCoefficients { get; }

    /// <summary>
    /// Gets the storage of the two reconstructions that a transform type search swaps between its candidate and its
    /// winner.
    /// </summary>
    public Span<int> SearchReconstructions { get; }

    /// <summary>
    /// Gets one of the two transform type search reconstructions.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <param name="index">The reconstruction slot, zero or one.</param>
    /// <returns>The reconstruction storage.</returns>
    public Span<TSample> GetSearchReconstruction<TSample>(int index)
        where TSample : unmanaged
        => Av1EncoderBlockWorkspace.GetSearchReconstruction<TSample>(this.SearchReconstructions, index);
}

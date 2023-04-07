// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text.Json.Serialization;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal class Vp8CostArray
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8CostArray"/> class.
    /// </summary>
    public Vp8CostArray() => this.Costs = new ushort[67 + 1];

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8CostArray"/> class.
    /// Only used for unit tests.
    /// </summary>
    [JsonConstructor]
    public Vp8CostArray(ushort[] costs) => this.Costs = costs;

    public ushort[] Costs { get; }
}

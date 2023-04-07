// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text.Json.Serialization;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal class Vp8Costs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Costs"/> class.
    /// </summary>
    public Vp8Costs()
    {
        this.Costs = new Vp8CostArray[WebpConstants.NumCtx];
        for (int i = 0; i < WebpConstants.NumCtx; i++)
        {
            this.Costs[i] = new Vp8CostArray();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Costs"/> class.
    /// Only used for unit tests.
    /// </summary>
    [JsonConstructor]
    public Vp8Costs(Vp8CostArray[] costs) => this.Costs = costs;

    /// <summary>
    /// Gets the Costs.
    /// </summary>
    public Vp8CostArray[] Costs { get; }
}

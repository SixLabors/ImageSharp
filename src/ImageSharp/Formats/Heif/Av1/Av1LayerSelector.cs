// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Identifies the AV1 spatial layer selected by an AVIF image item.
/// </summary>
internal readonly struct Av1LayerSelector
{
    /// <summary>
    /// The layer identifier that selects progressive exposure or the final layer rather than one specific spatial layer.
    /// </summary>
    public const ushort AllLayers = ushort.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LayerSelector"/> struct.
    /// </summary>
    /// <param name="layerId">The spatial-layer identifier, or <see cref="AllLayers"/> for progressive or final-layer decoding.</param>
    public Av1LayerSelector(ushort layerId)
    {
        this.LayerId = layerId;
    }

    /// <summary>
    /// Gets the spatial-layer identifier, or <see cref="AllLayers"/> when no individual layer is selected.
    /// </summary>
    public ushort LayerId { get; }
}

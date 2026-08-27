// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Describes the explicit payload sizes that delimit the first three layers of a layered AV1 image item.
/// </summary>
/// <param name="firstLayerSize">The first layer size in bytes.</param>
/// <param name="secondLayerSize">The second layer size in bytes.</param>
/// <param name="thirdLayerSize">The third layer size in bytes.</param>
internal readonly struct Av1LayeredImageIndex(uint firstLayerSize, uint secondLayerSize, uint thirdLayerSize)
{
    /// <summary>
    /// Gets the first layer size in bytes.
    /// </summary>
    public uint FirstLayerSize { get; } = firstLayerSize;

    /// <summary>
    /// Gets the second layer size in bytes.
    /// </summary>
    public uint SecondLayerSize { get; } = secondLayerSize;

    /// <summary>
    /// Gets the third layer size in bytes.
    /// </summary>
    public uint ThirdLayerSize { get; } = thirdLayerSize;

    /// <summary>
    /// Gets the number of item bytes needed to decode the selected spatial layer.
    /// </summary>
    /// <param name="itemSize">The complete logical image-item payload size.</param>
    /// <param name="selector">The requested spatial layer, or <see langword="null"/> to decode the final layer.</param>
    /// <returns>The cumulative payload size through the selected layer, or the complete item size for final-layer decoding.</returns>
    public int GetPayloadLength(int itemSize, Av1LayerSelector? selector)
    {
        int selectedLayer = selector is null || selector.Value.LayerId == Av1LayerSelector.AllLayers
            ? -1
            : selector.Value.LayerId;

        uint remainingSize = (uint)itemSize;
        uint selectedPayloadSize = 0;
        int layerCount = 0;
        for (int layer = 0; layer < Av1Constants.MaxSpatialLayerCount - 1; layer++)
        {
            uint layerSize = layer switch
            {
                0 => this.FirstLayerSize,
                1 => this.SecondLayerSize,
                _ => this.ThirdLayerSize
            };

            layerCount++;
            if (layerSize == 0)
            {
                if (selectedLayer < 0 || selectedLayer == layer)
                {
                    selectedPayloadSize += remainingSize;
                }

                remainingSize = 0;
                break;
            }

            if (layerSize >= remainingSize)
            {
                // Every explicit layer must leave at least one byte for the final implicit layer. A zero entry instead
                // identifies the current layer as final and consumes the complete remainder.
                throw new InvalidImageContentException($"AV1 layered-image layer {layer} does not fit within the item payload.");
            }

            if (selectedLayer < 0 || layer <= selectedLayer)
            {
                selectedPayloadSize += layerSize;
            }

            remainingSize -= layerSize;
        }

        if (remainingSize != 0)
        {
            if (selectedLayer < 0 || selectedLayer == layerCount)
            {
                selectedPayloadSize += remainingSize;
            }

            layerCount++;
        }

        if (selectedLayer >= layerCount)
        {
            throw new InvalidImageContentException($"AV1 layer selector requests layer {selectedLayer}, but the item contains {layerCount} layers.");
        }

        return selectedLayer < 0 ? itemSize : (int)selectedPayloadSize;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Expands the owned reconstructed colors and updates their logical dimensions before output conversion.
    /// </summary>
    /// <param name="upsampler">The ready shared component upsampler, also reusable for extra channels.</param>
    /// <param name="factor">The validated color factor.</param>
    /// <param name="cancellationToken">The token checked during row expansion.</param>
    public void ApplyUpsampling(JxlUpsampler upsampler, int factor, CancellationToken cancellationToken)
    {
        for (int component = 0; component < 3; component++)
        {
            upsampler.Apply(this.reconstructedChannels[component], this.reconstructedSizes[component], factor, cancellationToken);
            this.reconstructedSizes[component] = upsampler.TargetSize;
        }
    }
}

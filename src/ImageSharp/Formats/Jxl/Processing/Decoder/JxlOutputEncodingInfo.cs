// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

// Prefer class instead of struct because it's too large for a struct
// Note that this struct wasn't well documented, so it's not that easy to add
// XML documentation here.
internal sealed class JxlOutputEncodingInfo
{
    public JxlColorEncoding? OriginalColorEncoding { get; set; }

    public float OriginalIntensityTarget { get; set; }

    public JxlMatrix3x3F OriginalInverseMatrix { get; set; }

    public bool DefaultTransform { get; set; }

    public bool XybEncoded { get; set; }

    /// <summary>
    /// Gets or sets the requested color encoding.
    /// </summary>
    public JxlColorEncoding ColorEncoding { get; set; } = new();

    public JxlColorEncoding LinearColorEncoding { get; set; } = new();

    public bool ColorEncodingIsOriginal { get; set; }

    public JxlOpsinParameters OpsinParameters { get; set; } = new();

    public bool AllDefaultOpsin { get; set; }

    public float InverseGamma { get; set; }

    public Vector3 Luminances { get; set; }

    public float DesiredIntensityTarget { get; set; }

    public bool CmsSet { get; set; }

    public JxlCmsInterface Cms { get; set; }

    public void SetFromMetadata(JxlCodecMetadata metadata)
    {
        JxlImageMetadata imageMetadata = metadata.ImageMetadata ?? throw new InvalidOperationException("Missing image metadata");

        this.OriginalColorEncoding = imageMetadata.ColorEncoding;
        this.OriginalIntensityTarget = imageMetadata.IntensityTarget;
        this.DesiredIntensityTarget = this.OriginalIntensityTarget;

        JxlOpsinInverseMatrix inverseMatrix = metadata.CustomTransformData?.OpsinInverseMatrix ?? throw new InvalidOperationException("Missing Opsin inverse matrix or transform data");
        this.OriginalInverseMatrix = inverseMatrix.InverseMatrix;
        this.DefaultTransform = inverseMatrix.AllDefault;
        this.XybEncoded = imageMetadata.XybEncoded;

        JxlOpsinParameters parameters = this.OpsinParameters;

        imageMetadata.OpsinBiases.CopyTo(parameters.OpsinBiases);
        parameters.OpsinBiasesCbrt[0] = MathF.Cbrt(parameters.OpsinBiases[0]);
        parameters.OpsinBiasesCbrt[1] = MathF.Cbrt(parameters.OpsinBiases[1]);
        parameters.OpsinBiasesCbrt[2] = MathF.Cbrt(parameters.OpsinBiases[2]);

        parameters.OpsinBiasesCbrt[3] = 1;
        parameters.OpsinBiases[3] = 1;

        inverseMatrix.QuantBiases.AsSpan().CopyTo(parameters.QuantBiases);

        bool origOK = JxlXybDecoder.CanOutputToColorEncoding(this.OriginalColorEncoding ?? throw new InvalidCastException("Missing color encoding"));
        bool origGrey = this.OriginalColorEncoding.IsGray;

        return this.SetColorEncoding(!this.XybEncoded || origOK ? this.OriginalColorEncoding : JxlColorEncoding.LinearSrgb(origGrey));
    }
}

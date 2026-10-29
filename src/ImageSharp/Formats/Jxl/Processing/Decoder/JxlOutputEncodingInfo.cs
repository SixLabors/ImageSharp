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

    /// <summary>Loads the original encoding metadata and prepares its inverse opsin coefficients.</summary>
    /// <param name="metadata">The parsed image and transform headers.</param>
    public void SetFromMetadata(JxlCodecMetadata metadata)
    {
        JxlImageMetadata imageMetadata = metadata.ImageMetadata;

        this.OriginalColorEncoding = imageMetadata.ColorEncoding;
        this.OriginalIntensityTarget = imageMetadata.IntensityTarget;
        this.DesiredIntensityTarget = this.OriginalIntensityTarget;

        JxlOpsinInverseMatrix inverseMatrix = metadata.CustomTransformData.OpsinInverseMatrix
            ?? throw new InvalidOperationException("Missing opsin inverse matrix");

        this.OriginalInverseMatrix = inverseMatrix.InverseMatrix;
        this.DefaultTransform = inverseMatrix.AllDefault;
        this.XybEncoded = imageMetadata.XybEncoded;

        JxlOpsinParameters parameters = this.OpsinParameters;

        parameters.Initialize(this.OriginalInverseMatrix, inverseMatrix.OpsinBiases, this.OriginalIntensityTarget);
        parameters.QuantBiases = inverseMatrix.QuantBiases;

        bool origOK = JxlXybDecoder.CanOutputToColorEncoding(imageMetadata.ColorEncoding);
        bool origGrey = this.OriginalColorEncoding.IsGray;

        return this.SetColorEncoding(!this.XybEncoded || origOK ? this.OriginalColorEncoding : JxlColorEncoding.LinearSrgb(origGrey));
    }
}

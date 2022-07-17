// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Provides PBM specific metadata information for the image.
    /// </summary>
    public class PbmMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PbmMetadata"/> class.
        /// </summary>
        public PbmMetadata() =>
            this.ComponentType = this.ColorType == PbmColorType.BlackAndWhite ? PbmComponentType.Bit : PbmComponentType.Byte;

        /// <summary>
        /// Initializes a new instance of the <see cref="PbmMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private PbmMetadata(PbmMetadata other)
        {
            this.Encoding = other.Encoding;
            this.ColorType = other.ColorType;
            this.ComponentType = other.ComponentType;
        }

        /// <summary>
        /// Gets or sets the encoding of the pixels.
        /// </summary>
        public PbmEncoding Encoding { get; set; } = PbmEncoding.Plain;

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public PbmColorType ColorType { get; set; } = PbmColorType.Grayscale;

        /// <summary>
        /// Gets or sets the data type of the pixel components.
        /// </summary>
        public PbmComponentType ComponentType { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new PbmMetadata(this);
    }
}

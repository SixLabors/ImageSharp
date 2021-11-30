// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Adjusts an image so that its orientation is suitable for viewing. Adjustments are based on EXIF metadata embedded in the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AutoOrientProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutoOrientProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public AutoOrientProcessor(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
        }

        /// <inheritdoc/>
        protected override void BeforeImageApply()
        {
            // Rotate/flip the image according to the EXIF orientation metadata
            OrientationMode orientation = this.Source.Metadata.GetOrientation();
            Size size = this.SourceRectangle.Size;
            switch (orientation)
            {
                case OrientationMode.TopRight:
                    new FlipProcessor(FlipMode.Horizontal).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case OrientationMode.BottomRight:
                    new RotateProcessor((int)RotateMode.Rotate180, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case OrientationMode.BottomLeft:
                    new FlipProcessor(FlipMode.Vertical).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case OrientationMode.LeftTop:
                    new RotateProcessor((int)RotateMode.Rotate90, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    new FlipProcessor(FlipMode.Horizontal).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case OrientationMode.RightTop:
                    new RotateProcessor((int)RotateMode.Rotate90, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case OrientationMode.RightBottom:
                    new FlipProcessor(FlipMode.Vertical).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    new RotateProcessor((int)RotateMode.Rotate270, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case OrientationMode.LeftBottom:
                    new RotateProcessor((int)RotateMode.Rotate270, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case OrientationMode.Unknown:
                case OrientationMode.TopLeft:
                default:
                    break;
            }

            // Reset the orientation to top/left
            if (orientation != OrientationMode.Unknown)
            {
                this.Source.Metadata.ExifProfile ??= new ExifProfile();
                this.Source.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)OrientationMode.TopLeft);
            }

            base.BeforeImageApply();
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> sourceBase)
        {
            // All processing happens at the image level within BeforeImageApply();
        }
    }
}

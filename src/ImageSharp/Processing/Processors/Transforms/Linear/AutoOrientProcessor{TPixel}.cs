// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
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
            ushort orientation = GetExifOrientation(this.Source);
            Size size = this.SourceRectangle.Size;
            switch (orientation)
            {
                case ExifOrientationMode.TopRight:
                    new FlipProcessor(FlipMode.Horizontal).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case ExifOrientationMode.BottomRight:
                    new RotateProcessor((int)RotateMode.Rotate180, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case ExifOrientationMode.BottomLeft:
                    new FlipProcessor(FlipMode.Vertical).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case ExifOrientationMode.LeftTop:
                    new RotateProcessor((int)RotateMode.Rotate90, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    new FlipProcessor(FlipMode.Horizontal).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case ExifOrientationMode.RightTop:
                    new RotateProcessor((int)RotateMode.Rotate90, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case ExifOrientationMode.RightBottom:
                    new FlipProcessor(FlipMode.Vertical).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    new RotateProcessor((int)RotateMode.Rotate270, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case ExifOrientationMode.LeftBottom:
                    new RotateProcessor((int)RotateMode.Rotate270, size).Execute(this.Configuration, this.Source, this.SourceRectangle);
                    break;

                case ExifOrientationMode.Unknown:
                case ExifOrientationMode.TopLeft:
                default:
                    break;
            }

            base.BeforeImageApply();
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> sourceBase)
        {
            // All processing happens at the image level within BeforeImageApply();
        }

        /// <summary>
        /// Returns the current EXIF orientation
        /// </summary>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="ushort"/></returns>
        private static ushort GetExifOrientation(Image<TPixel> source)
        {
            if (source.Metadata.ExifProfile is null)
            {
                return ExifOrientationMode.Unknown;
            }

            IExifValue<ushort> value = source.Metadata.ExifProfile.GetValue(ExifTag.Orientation);
            if (value is null)
            {
                return ExifOrientationMode.Unknown;
            }

            ushort orientation;
            if (value.DataType == ExifDataType.Short)
            {
                orientation = value.Value;
            }
            else
            {
                orientation = Convert.ToUInt16(value.Value);
                source.Metadata.ExifProfile.RemoveValue(ExifTag.Orientation);
            }

            source.Metadata.ExifProfile.SetValue(ExifTag.Orientation, ExifOrientationMode.TopLeft);

            return orientation;
        }
    }
}

namespace ImageProcessor.Filters
{
    using System;
    using System.Numerics;

    public class Hue : ColorMatrixFilter
    {
        /// <summary>
        /// The <see cref="Matrix4x4"/> used to alter the image.
        /// </summary>
        private Matrix4x4 matrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="Hue"/> class.
        /// </summary>
        /// <param name="angle">The new brightness of the image. Must be between -100 and 100.</param>
        public Hue(float angle)
        {
            // Wrap the angle round at 360.
            angle = angle % 360;

            // Make sure it's not negative.
            while (angle < 0)
            {
                angle += 360;
            }

            this.Angle = angle;
        }

        /// <summary>
        /// Gets the rotation value.
        /// </summary>
        public float Angle { get; }

        /// <inheritdoc/>
        public override Matrix4x4 Matrix => this.matrix;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            float degrees = this.Angle;
            double costheta = Math.Cos(degrees);
            double sintheta = Math.Sin(degrees);
            float lumR = .213f;
            float lumG = .715f;
            float lumB = .072f;

            // The matrix is set up to preserve the luminance of the image.
            // See http://graficaobscura.com/matrix/index.html
            // Number are taken from https://msdn.microsoft.com/en-us/library/jj192162(v=vs.85).aspx
            // TODO: This isn't correct. Need to double check MS numbers against maths.
            Matrix4x4 matrix4X4 = new Matrix4x4()
            {
                M11 = (float)(.213 + (costheta * .787) - (sintheta * .213)),
                M12 = (float)(.715 - (costheta * .715) - (sintheta * .715)),
                M13 = (float)(.072 - (costheta * .072) + (sintheta * .928)),
                M21 = (float)(.213 - (costheta * .213) + (sintheta * .143)),
                M22 = (float)(.715 + (costheta * .285) + (sintheta * .140)),
                M23 = (float)(.072 - (costheta * .072) - (sintheta * .283)),
                M31 = (float)(.213 - (costheta * .213) - (sintheta * .787)),
                M32 = (float)(.715 - (costheta * .715) + (sintheta * .715)),
                M33 = (float)(.072 + (costheta * .928) + (sintheta * .072))
            };

            this.matrix = matrix4X4;
        }
    }
}

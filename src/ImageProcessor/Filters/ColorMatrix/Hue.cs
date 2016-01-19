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
        public override bool Compand => false;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            float radians = (float)ImageMaths.DegreesToRadians(this.Angle);
            double cosradians = Math.Cos(radians);
            double sinradians = Math.Sin(radians);

            float lumR = .213f;
            float lumG = .715f;
            float lumB = .072f;

            float oneMinusLumR = 1 - lumR;
            float oneMinusLumG = 1 - lumG;
            float oneMinusLumB = 1 - lumB;

            // The matrix is set up to preserve the luminance of the image.
            // See http://graficaobscura.com/matrix/index.html
            // Number are taken from https://msdn.microsoft.com/en-us/library/jj192162(v=vs.85).aspx
            Matrix4x4 matrix4X4 = new Matrix4x4()
            {
                M11 = (float)(lumR + (cosradians * oneMinusLumR) - (sinradians * lumR)),
                M12 = (float)(lumR - (cosradians * lumR) - (sinradians * 0.143)),
                M13 = (float)(lumR - (cosradians * lumR) - (sinradians * oneMinusLumR)),
                M21 = (float)(lumG - (cosradians * lumG) - (sinradians * lumG)),
                M22 = (float)(lumG + (cosradians * oneMinusLumG) + (sinradians * 0.140)),
                M23 = (float)(lumG - (cosradians * lumG) + (sinradians * lumG)),
                M31 = (float)(lumB - (cosradians * lumB) + (sinradians * oneMinusLumB)),
                M32 = (float)(lumB - (cosradians * lumB) - (sinradians * 0.283)),
                M33 = (float)(lumB + (cosradians * oneMinusLumB) + (sinradians * lumB))
            };

            this.matrix = matrix4X4;
        }
    }
}

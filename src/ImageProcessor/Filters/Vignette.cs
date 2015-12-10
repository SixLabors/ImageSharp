namespace ImageProcessor.Filters
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Creates a vignette
    /// </summary>
    public class Vignette : ParallelImageProcessor
    {
        /// <summary>
        /// Used to hold a copy of the target image.
        /// </summary>
        private readonly Image targetCopy = new Image();

        /// <summary>
        /// Gets or sets the vignette color to apply.
        /// </summary>
        public Color Color { get; set; } = Color.Black;

        /// <summary>
        /// Gets or sets the the x-radius.
        /// </summary>
        public float RadiusX { get; set; }

        /// <summary>
        /// Gets or sets the the y-radius.
        /// </summary>
        public float RadiusY { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            this.targetCopy.SetPixels(target.Width, target.Height, target.Pixels);
            target.SetPixels(target.Width, target.Height, new float[target.Pixels.Length]);
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            Vector2 centre = Rectangle.Center(targetRectangle);
            int centerX = (int)centre.X;
            int centerY = (int)centre.Y;
            float rX = this.RadiusX > 0 ? this.RadiusX : targetRectangle.Width / 2f;
            float rY = this.RadiusY > 0 ? this.RadiusY : targetRectangle.Height / 2f;

            Ellipse ellipse = new Ellipse(new Point(centerX, centerY), rX - 1, rY - 1);

            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            if (!ellipse.Contains(x, y))
                            {
                                target[x, y] = Color.Black;
                            }
                        }
                    });
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            new GuassianBlur(30).Apply(target, target, targetRectangle);
            Image temp = new Image(this.targetCopy);
            new Blend(target, 40).Apply(this.targetCopy, temp, targetRectangle);
            target.SetPixels(temp.Width, temp.Height, this.targetCopy.Pixels);
        }
    }
}


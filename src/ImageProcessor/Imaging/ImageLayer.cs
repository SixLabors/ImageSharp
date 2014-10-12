

namespace ImageProcessor.Imaging
{
    using System.Drawing;

    /// <summary>
    /// Encapsulates the properties required to add an image layer to an image.
    /// </summary>
    public class ImageLayer
    {
        /// <summary>
        /// The opacity at which to render the text.
        /// </summary>
        private int opacity = 100;

        /// <summary>
        /// The position to start creating the text from.
        /// </summary>
        private Point position = Point.Empty;

        /// <summary>
        /// Gets or sets the image.
        /// </summary>
        public Image Image { get; set; }

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets the Opacity of the text layer.
        /// </summary>
        public int Opacity
        {
            get { return this.opacity; }
            set { this.opacity = value; }
        }

        /// <summary>
        /// Gets or sets the Position of the text layer.
        /// </summary>
        public Point Position
        {
            get { return this.position; }
            set { this.position = value; }
        }
    }
}

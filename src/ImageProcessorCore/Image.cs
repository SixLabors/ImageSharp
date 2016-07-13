using System.IO;

namespace ImageProcessorCore
{
    public class Image : Image<Bgra32, uint>
    {
        // TODO Constructors.
        public Image(int width, int height)
          : base(width, height)
        {
        }

        public Image(Stream stream)
            : base(stream)
        {
        }
        public Image(Image other)
            : base(other)
        {
        }
    }
}

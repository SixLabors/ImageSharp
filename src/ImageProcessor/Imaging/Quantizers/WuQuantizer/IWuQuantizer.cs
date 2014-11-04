using System.Drawing;

namespace nQuant
{
    public interface IWuQuantizer
    {
        Image QuantizeImage(Bitmap image, int alphaThreshold, int alphaFader);
    }
}
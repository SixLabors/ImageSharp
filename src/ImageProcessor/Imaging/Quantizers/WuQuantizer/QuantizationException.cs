using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    [Serializable]
    public class QuantizationException : ApplicationException
    {
        public QuantizationException(string message) : base(message)
        {

        }
    }
}

using System;
using System.Linq;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    internal class JpegPostProcessor
    {
        private ComponentPostProcessor[] componentProcessors;

        public JpegPostProcessor(IRawJpegData data)
        {
            this.Data = data;
            this.componentProcessors = data.Components.Select(c => new ComponentPostProcessor(this, c)).ToArray();
        }

        public IRawJpegData Data { get; }
    }

    internal class ComponentPostProcessor : IDisposable
    {
        public ComponentPostProcessor(JpegPostProcessor jpegPostProcessor, IJpegComponent component)
        {
            this.Component = component;
            this.JpegPostProcessor = jpegPostProcessor;
        }

        public JpegPostProcessor JpegPostProcessor { get; }

        public IJpegComponent Component { get; }
        
        public int NumberOfRowGroupSteps { get; }
        
        public Buffer2D<float> ColorBuffer { get; }

        public void Dispose()
        {
        }
    }
}
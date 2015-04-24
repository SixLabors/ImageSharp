namespace ImageProcessor.Imaging.Filters.ObjectDetection
{
    using System.IO;
    using System.Reflection;

    using ImageProcessor.Common.Extensions;

    public static class EmbeddedHaarCascades
    {
        private static HaarCascade frontFaceDefault;

        public static HaarCascade FrontFaceDefault
        {
            get
            {
                return frontFaceDefault ?? (frontFaceDefault = GetCascadeFromResource("haarcascade_frontalface_legacy.xml"));
            }
        }

        private static HaarCascade GetCascadeFromResource(string identifier)
        {
            HaarCascade cascade;
            var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("ImageProcessor.Imaging.Filters.ObjectDetection.Resources." + identifier);

            //using (StringReader stringReader = new StringReader(resource))
            //{
                cascade = HaarCascade.FromXml(resource);
            //}

            return cascade;
        }
    }
}

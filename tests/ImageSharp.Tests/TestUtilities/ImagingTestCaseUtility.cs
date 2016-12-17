namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public class ImagingTestCaseUtility
    {
        public string TestOutputRoot { get; set; } = FileTestBase.TestOutputRoot;

        public string TestGroupName { get; set; } = "";

        public string SourceFileOrDescription { get; set; } = "";

        public string TestName { get; set; } = "";

        public string PixelTypeName { get; set; } = "";

        internal void Init(MethodInfo method)
        {
            this.TestGroupName = method.DeclaringType.Name;
            this.TestName = method.Name;
        }

        public string GetTestOutputDir()
        {
            string testGroupName = Path.GetFileNameWithoutExtension(this.TestGroupName);

            string dir = $@"{this.TestOutputRoot}{testGroupName}";
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetTestOutputFileName(string extension = null)
        {
            string fn = "";

            fn = Path.GetFileNameWithoutExtension(this.SourceFileOrDescription);
            extension = extension ?? Path.GetExtension(this.SourceFileOrDescription);
            extension = extension ?? ".bmp";

            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }

            if (fn != "") fn = '_' + fn;

            string pixName = this.PixelTypeName;
            if (pixName != "")
            {
                pixName = '_' + pixName + ' ';
            }

            return $"{this.GetTestOutputDir()}/{this.TestName}{pixName}{fn}{extension}";
        }

        // TODO: This is messy, need to refactor all the output writing logic out from TestImageFactory

        public void SaveTestOutputFile<TColor, TPacked>(Image<TColor, TPacked> image, string extension = null)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            string path = this.GetTestOutputFileName(extension);

            var format = Bootstrapper.Instance.ImageFormats.First(f => f.Encoder.IsSupportedFileExtension(extension));

            using (var stream = File.OpenWrite(path))
            {
                image.Save(stream, format);
            }
        }
    }
}
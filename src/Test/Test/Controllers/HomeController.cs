using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Test.Controllers
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web.Hosting;

    using ImageProcessor;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Imaging;

    //using ImageProcessor.Web.Caching;

    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "ImageProcessor test website";

            return View();
        }

        public ActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Upload(HttpPostedFileBase file)
        {
            Stream upload = file.InputStream;
            int quality = 70;
            ImageFormat format = ImageFormat.Jpeg;
            Size size460 = new Size(460, 0);
            Size size320 = new Size(320, 0);
            Size size240 = new Size(240, 0);

            // Make sure the directory exists as Image.Save will not work without an existing directory.
            string outputPath = HostingEnvironment.MapPath("~/Resized");
            if (outputPath != null)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(outputPath);

                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                }

                // Make the three file paths
                string outputfile1 = Path.Combine(outputPath, "460px_" + file.FileName);
                string outputfile2 = Path.Combine(outputPath, "320px_" + file.FileName);
                string outputfile3 = Path.Combine(outputPath, "240px_" + file.FileName);

                using (MemoryStream inStream = new MemoryStream())
                {
                    // Copy the stream across.
                    upload.CopyTo(inStream);

                    using (ImageFactory imageFactory = new ImageFactory())
                    {
                        // Load, resize, set the format and quality and save an image.
                        imageFactory.Load(inStream)
                                    .Format(format)
                                    .Quality(quality)
                                    .Resize(size460)
                                    .Save(outputfile1)
                                    .Reset()
                                    .Format(format)
                                    .Quality(quality)
                                    .Resize(size320)
                                    .Save(outputfile2)
                                    .Reset()
                                    .Format(format)
                                    .Quality(quality)
                                    .Resize(size240)
                                    .Save(outputfile3);
                    }
                }
            }

            return View();
        }

        public ActionResult About()
        {
            List<string> images = new List<string>();

            const string Path = "/images/";
            string folder = HostingEnvironment.MapPath(Path);
            if (folder != null)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folder);

                if (directoryInfo.Exists)
                {
                    // Get all the files in the cache ordered by LastAccessTime - oldest first.
                    List<FileInfo> fileInfos = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(x => x.LastAccessTime).ToList();

                    int counter = fileInfos.Count;

                    Parallel.ForEach(
                        fileInfos,
                        fileInfo => images.Add(Path + fileInfo.Name));
                }
            }

            return View(images);
        }

        public ActionResult Responsive()
        {


            return this.View();
        }

        public ActionResult Collisions()
        {
            DateTime start = DateTime.Now;

            List<double> collisions = new List<double>();
            const int Iterations = 1;
            const int Maxitems = 3600000;

            for (int i = 0; i < Iterations; i++)
            {
                List<string> paths = new List<string>();

                for (int j = 0; j < Maxitems; j++)
                {
                    string path = Path.GetRandomFileName().ToMD5Fingerprint();

                    path = string.Format("/{0}/{1}/{2}", path.Substring(0, 1), path.Substring(31, 1), path.Substring(0, 8));

                    paths.Add(path);
                }

                int count = paths.Distinct().Count();

                double collisionRate = ((Maxitems - count) * 100D) / Maxitems;
                collisions.Add(collisionRate);
            }

            double averageCollisionRate = collisions.Average();


            TimeSpan timeSpan = DateTime.Now - start;

            ViewBag.Collision = averageCollisionRate;

            return this.View(timeSpan);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Test.Controllers
{
    using System.IO;
    using System.Threading.Tasks;
    using System.Web.Hosting;

    using ImageProcessor.Helpers.Extensions;
    //using ImageProcessor.Web.Caching;

    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "ImageProcessor test website";

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

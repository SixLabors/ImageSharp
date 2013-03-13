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

    using ImageProcessor.Web.Caching;

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

        public ActionResult Speed()
        {
            DateTime start = DateTime.Now;

            //PersistantDictionary persistantDictionary = PersistantDictionary.Instance;

            //for (int i = 0; i < 1000; i++)
            //{
            //    string random = Path.GetRandomFileName();
            //    random = random + random;

            //    CachedImage cachedImage = new CachedImage(random, DateTime.UtcNow, DateTime.UtcNow);
            //    persistantDictionary.GetOrAdd(random, x => cachedImage);
            //}

            //KeyValuePair<string, CachedImage> pair = persistantDictionary.First();

            //CachedImage image;

            //persistantDictionary.TryRemove(pair.Key, out image);

            TimeSpan timeSpan = DateTime.Now - start;

            //ViewBag.Count = persistantDictionary.Count();

            return this.View(timeSpan);
        }
    }
}

ImageProcessor
===============

ImageProcessor is a library for on the fly processing of image files using Asp.Net

The library architecture is highly extensible and allows for easy extension.

Core plugins at present include:

 - Resize 
 - Crop
 - Quality (The quality to set the output for jpeg files)
 - Filter (Image filters including sepia, greyscale, blackwhite, lomograph, comic)
 - Vignette 
 - Format (Sets the output format)
 - Alpha (Sets opacity)

The library consists of two binaries: ImageProcessor.dll and ImageProcessor.Web.dll.

ImageProcessor.dll contains all the core functionality that allows for image manipulation via the `ImageFactory` class. This has a fluent API which allows you to easily chain methods to deliver the desired output.

e.g.

    // Read a file and resize it.
    var photoBytes = File.ReadAllBytes(file);
    int quality = 90;
    ImageFormat format = ImageFormat.Jpeg;
    int thumbnailSize = 150;
        
    using (var inStream = new MemoryStream(photoBytes))
    {
        using (var outStream = new MemoryStream())
        {
            using (ImageFactory imageFactory = new ImageFactory())
            {
                // Load, resize and save an image.
                imageFactory.Load(inStream).Format(format).Quality(quality).Resize(thumbnailSize, 0).Save(outStream);
            }
        }
    }

ImageProcessor.Web.dll contains a HttpModule which captures internal and external requests automagically processing them based on values captured through querystring parameters.

Using the HttpModule requires no code writing at all. Just reference the binaries and add the relevant sections to the web.config

Image requests suffixed with QueryString parameters will then be processed and cached to the server allowing for easy and efficient parsing of following requests.

e.g.

    <img src="/images.yourimage.jpg?width=200" alt="your resized image"/>

Will resize your image to 200px wide whilst keeping the correct aspect ratio.

Remote files can also be requested and cached by prefixing the src with a value set in the web.config 

e.g.

    <img src="remote.axd/http://url/images.yourimage.jpg?width=200" alt="your resized image"/>

Will resize your remote image to 200px wide whilst keeping the correct aspect ratio.
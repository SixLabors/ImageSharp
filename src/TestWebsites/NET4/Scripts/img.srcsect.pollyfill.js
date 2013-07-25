(function ($) {
    // http: //www.whatwg.org/specs/web-apps/current-work/multipage/embedded-content-1.html#attr-img-srcset

    // Regexes for matching queries.
    var rSrc = /[^\s]+/,
        rWidth = /(\d+)w/,
        rRatio = /(\d+)x/;

    // Detect retina display
    // http: //www.quirksmode.org/blog/archives/2012/06/devicepixelrati.html
    var pixelRatio = (window.devicePixelRatio || 1);

    // Cache the images as theres no point querying them twice.
    var imageList = [];

    // http://lodash.com/docs/#debounce
    var debounce = function (func, wait, immediate) {
        var args,
            result,
            thisArg,
            timeoutId;

        function delayed() {
            timeoutId = null;
            if (!immediate) {
                func.apply(thisArg, args);
            }
        }

        return function () {
            var isImmediate = immediate && !timeoutId;
            args = arguments;
            thisArg = this;

            clearTimeout(timeoutId);
            timeoutId = setTimeout(delayed, wait);

            if (isImmediate) {
                result = func.apply(thisArg, args);
            }
            return result;
        };
    };

    var getImgSrc = function (image) {
        var imgSrc = null, imgWidth = 0, i,
            imgSrcParts = image.attributes["srcset"].nodeValue.split(","),
            len = imgSrcParts.length,
            width = $(window).width();

        for (i = 0; i < len; i += 1) {

            // This is just a rough play on the algorithm.
            var newImgSrc = imgSrcParts[i].match(rSrc)[0],
                newImgWidth = rWidth.test(imgSrcParts[i]) ? parseInt(imgSrcParts[i].match(rWidth)[1], 10) : 1, // Use 1 for truthy
                newPixelRatio = rRatio.test(imgSrcParts[i]) ? parseInt(imgSrcParts[i].match(rRatio)[1], 10) : 1;

            if ((newImgWidth > imgWidth && width > newImgWidth && newPixelRatio === pixelRatio)) {

                imgWidth = newImgWidth || imgWidth;
                imgSrc = newImgSrc;
            }
        }

        // Return null  
        return imgSrc;
    };

    $(window).resize(function () {

        $.each(imageList, function () {
            var self = this,
                checkImage = function () {
                    var src = getImgSrc(self);

                    if (src) {
                        self.src = src;
                    }

                },
                lazyCheck = debounce(checkImage, 100);

            // Run debounced
            lazyCheck();

        });

    });

    $(window).load(function () {
        $("img[srcset]").each(function () {

            var src = getImgSrc(this);

            if (src) {
                this.src = src;
            }

            imageList.push(this);
        });
    });

} (jQuery));
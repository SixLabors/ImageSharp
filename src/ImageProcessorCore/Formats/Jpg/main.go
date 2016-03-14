package main
import (
	"fmt"
	"os"
	"image"
	"./jpeg2"
	_ "image/png"
)

func main() {
	f, err := os.Open("/Users/mweber/dev/ImageProcessor/src/ImageProcessorCore/a.png");
	m, _, err := image.Decode(f)

	f2, err := os.Create("/Users/mweber/dev/ImageProcessor/src/ImageProcessorCore/go.jpg");

	if err == nil {
		jpeg2.Encode(f2, m, nil);
		fmt.Println("done")
	}
}

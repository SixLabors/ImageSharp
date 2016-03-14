package main
import "fmt"
import "os"
import "./jpeg2"

func main() {
	f, err := os.Open("/Users/mweber/dev/fprint/print.jpg");
	if err == nil {
		jpeg2.Decode(f);
		fmt.Println("done")
	}
}

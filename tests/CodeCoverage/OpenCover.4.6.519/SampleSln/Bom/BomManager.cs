using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bom
{
    public class BomManager
    {
        public int MethodToTest(IEnumerable<int> collection)
        {
            return (collection ?? new int[0]).Sum();
        }
    }
}

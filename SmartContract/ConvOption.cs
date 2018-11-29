using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Compiler
{
    public class ConvOption
    {
        public bool useNep8 = false;//將call 升級為callI
        public static ConvOption Default
        {
            get
            {
                return new ConvOption();
            }
        }
    }
}

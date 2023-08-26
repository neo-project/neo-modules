using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Plugins.RestServer.Models.Utils
{
    internal class UtilsAddressIsValidModel : UtilsAddressModel
    {
        /// <summary>
        /// Indicates if address can be converted to ScriptHash or Neo Address.
        /// </summary>
        /// <example>true</example>
        public bool IsValid { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StewartTitle.Argano.Plugins.Models
{
    internal class AccountUpdatePayload
    {
        public string name {  get; set; }
        public string address1_line1 { get; set; }
        public string address1_city { get; set; }
        public string address1_stateorprovince { get; set; }
        public string address1_country { get; set; }
        public string address1_postalcode { get; set; }
    }
}

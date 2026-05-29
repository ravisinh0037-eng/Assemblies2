using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StewartTitle.Argano.APIs.Models
{
    public class InboundTPSTransactionPayload
    {
        [JsonProperty("transactions")]
        public List<InboundTPSTransactionItem> transactions { get; set; }
    }
}
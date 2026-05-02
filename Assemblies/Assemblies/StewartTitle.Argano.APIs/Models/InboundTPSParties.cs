using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StewartTitle.Argano.APIs.Models
{
    public class InboundTPSParties
    {
        [JsonPropertyName("parties")]
        public List<InboundTPSParty> Parties {  get; set; }
    }
}

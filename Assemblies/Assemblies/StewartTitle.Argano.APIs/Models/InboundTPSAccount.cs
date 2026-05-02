using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StewartTitle.Argano.APIs.Models
{
    public class InboundTPSAccount
    {
        [JsonPropertyName("accountID")]
        public string accountID {  get; set; }
        [JsonPropertyName("accountName")]
        public string accountName { get; set; }
        [JsonPropertyName("accountAddress1")]
        public string accountAddress1 { get; set; }
        [JsonPropertyName("accountAddress2")]
        public string accountAddress2 { get; set; }
        [JsonPropertyName("accountCity")]
        public string accountCity { get; set; }
        [JsonPropertyName("accountState")]
        public string accountState { get; set; }
        [JsonPropertyName("accountZip")]
        public string accountZip { get; set; }
        [JsonPropertyName("accountPhoneNumber")]
        public string accountPhoneNumber { get; set; }
        [JsonPropertyName("accountCounty")]
        public string accountCounty { get; set; }
    }
}

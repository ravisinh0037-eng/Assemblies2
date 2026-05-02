using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StewartTitle.Argano.APIs.Models
{
    public class InboundTPSParty
    {
        [JsonPropertyName("enterpriseID")]
        public string enterpriseID {  get; set; }
        [JsonPropertyName("UUID")]
        public string UUID { get; set; }
        [JsonPropertyName("partySystem")]
        public string partySystem {  get; set; }
        [JsonPropertyName("partyID")]
        public string partyID { get; set; }
        [JsonPropertyName("firstName")]
        public string firstName { get; set; }
        [JsonPropertyName("lastName")]
        public string lastName { get; set; }
        [JsonPropertyName("businessTitle")]
        public string businessTitle { get; set; }
        [JsonPropertyName("contactAddress1")]
        public string contactAddress1 { get; set; }
        [JsonPropertyName("contactAddress2")]
        public string contactAddress2 { get; set; }
        [JsonPropertyName("contactCity")]
        public string contactCity { get; set; }
        [JsonPropertyName("contactCounty")]
        public string contactCounty { get; set; }
        [JsonPropertyName("contactState")]
        public string contactState { get; set; }
        [JsonPropertyName("contactZip")]
        public string contactZip { get; set; }
        [JsonPropertyName("contactPhoneNumber")]
        public string contactPhoneNumber { get; set; }
        [JsonPropertyName("primaryEmail")]
        public string primaryEmail { get; set; }
        [JsonPropertyName("relatedEmail")]
        public string relatedEmail { get; set; }
        [JsonPropertyName("businessAddress1")]
        public string businessAddress1 { get; set; }
        [JsonPropertyName("businessAddress2")]
        public string businessAddress2 { get; set; }
        [JsonPropertyName("businessCity")]
        public string businessCity { get; set; }
        [JsonPropertyName("businessCounty")]
        public string businessCounty { get; set; }
        [JsonPropertyName("businessState")]
        public string businessState { get; set; }
        [JsonPropertyName("businessZip")]
        public string businessZip { get; set; }
        [JsonPropertyName("businessPhoneNumber")]
        public string businessPhoneNumber { get; set; }
        [JsonPropertyName("multiMatch")]
        public string multiMatch { get; set; }
        [JsonPropertyName("matchedEnterpriseID")]
        public string[] matchedEnterpriseID { get; set; }
        [JsonPropertyName("account")]
        public InboundTPSAccount Account { get; set; }
        [JsonPropertyName("transactions")]
        public InboundTPSTransaction[] Transactions { get; set; }

    }
}

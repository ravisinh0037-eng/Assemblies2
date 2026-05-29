using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StewartTitle.Argano.APIs.Models
{
    public class InboundTPSTransactionParty
    {
        [JsonProperty("enterpriseID")]
        public string enterpriseID { get; set; }

        [JsonProperty("UUID")]
        public string UUID { get; set; }

        [JsonProperty("partySystem")]
        public string partySystem { get; set; }

        [JsonProperty("partyID")]
        public string partyID { get; set; }

        [JsonProperty("firstName")]
        public string firstName { get; set; }

        [JsonProperty("lastName")]
        public string lastName { get; set; }

        [JsonProperty("businessTitle")]
        public string businessTitle { get; set; }

        [JsonProperty("contactAddress1")]
        public string contactAddress1 { get; set; }

        [JsonProperty("contactAddress2")]
        public string contactAddress2 { get; set; }

        [JsonProperty("contactCity")]
        public string contactCity { get; set; }

        [JsonProperty("contactCounty")]
        public string contactCounty { get; set; }

        [JsonProperty("contactState")]
        public string contactState { get; set; }

        [JsonProperty("contactZip")]
        public string contactZip { get; set; }

        [JsonProperty("contactPhoneNumber")]
        public string contactPhoneNumber { get; set; }

        [JsonProperty("primaryEmail")]
        public string primaryEmail { get; set; }

        [JsonProperty("relatedEmail")]
        public string relatedEmail { get; set; }

        [JsonProperty("businessAddress1")]
        public string businessAddress1 { get; set; }

        [JsonProperty("businessAddress2")]
        public string businessAddress2 { get; set; }

        [JsonProperty("businessCity")]
        public string businessCity { get; set; }

        [JsonProperty("businessCounty")]
        public string businessCounty { get; set; }

        [JsonProperty("businessState")]
        public string businessState { get; set; }

        [JsonProperty("businessZip")]
        public string businessZip { get; set; }

        [JsonProperty("businessPhoneNumber")]
        public string businessPhoneNumber { get; set; }

        [JsonProperty("multiMatch")]
        public string multiMatch { get; set; }

        [JsonProperty("matchedEnterpriseID")]
        public string[] matchedEnterpriseID { get; set; }

        [JsonProperty("account")]
        public InboundTPSAccount Account { get; set; }
    }
}
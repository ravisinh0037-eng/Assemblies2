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
        [JsonProperty("partyID")]
        public string partyID { get; set; }

        [JsonProperty("partySystem")]
        public string partySystem { get; set; }

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

        [JsonProperty("contactFinanceIDText")]
        public string contactFinanceIDText { get; set; }

        [JsonProperty("roleInFile")]
        public string roleInFile { get; set; }

        [JsonProperty("directingPartyFlag")]
        public bool directingPartyFlag { get; set; }

        [JsonProperty("accountID")]
        public string accountID { get; set; }

        [JsonProperty("accountSystem")]
        public string accountSystem { get; set; }

        [JsonProperty("accountName")]
        public string accountName { get; set; }

        [JsonProperty("accountAddress1")]
        public string accountAddress1 { get; set; }

        [JsonProperty("accountAddress2")]
        public string accountAddress2 { get; set; }

        [JsonProperty("accountCity")]
        public string accountCity { get; set; }

        [JsonProperty("accountCounty")]
        public string accountCounty { get; set; }

        [JsonProperty("accountState")]
        public string accountState { get; set; }

        [JsonProperty("accountZip")]
        public string accountZip { get; set; }

        [JsonProperty("accountPhoneNumber")]
        public string accountPhoneNumber { get; set; }

        [JsonProperty("accountCountry")]
        public string accountCountry { get; set; }

        [JsonProperty("accountFinanceIDText")]
        public string accountFinanceIDText { get; set; }
    }
}
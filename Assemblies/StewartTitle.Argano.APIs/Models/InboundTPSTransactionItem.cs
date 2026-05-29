using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StewartTitle.Argano.APIs.Models
{
    public class InboundTPSTransactionItem
    {
        [JsonProperty("transactionID")]
        public string transactionID { get; set; }

        [JsonProperty("fileNumber")]
        public string fileNumber { get; set; }

        [JsonProperty("transactionSystem")]
        public string transactionSystem { get; set; }

        [JsonProperty("fileStatus")]
        public string fileStatus { get; set; }

        [JsonProperty("propertyStreet1")]
        public string propertyStreet1 { get; set; }

        [JsonProperty("propertyStreet2")]
        public string propertyStreet2 { get; set; }

        [JsonProperty("propertyCity")]
        public string propertyCity { get; set; }

        [JsonProperty("propertyCounty")]
        public string propertyCounty { get; set; }

        [JsonProperty("propertyState")]
        public string propertyState { get; set; }

        [JsonProperty("propertyZip")]
        public string propertyZip { get; set; }

        [JsonProperty("loanAmount")]
        public string loanAmount { get; set; }

        [JsonProperty("salesPrice")]
        public string salesPrice { get; set; }

        [JsonProperty("transactionType")]
        public string transactionType { get; set; }

        [JsonProperty("BDOBranchInfo")]
        public string BDOBranchInfo { get; set; }

        [JsonProperty("fileStartDate")]
        public string fileStartDate { get; set; }

        [JsonProperty("estSettlementDate")]
        public string estSettlementDate { get; set; }

        [JsonProperty("finalClosingDate")]
        public string finalClosingDate { get; set; }

        [JsonProperty("titleFee")]
        public string titleFee { get; set; }

        [JsonProperty("escrowFee")]
        public string escrowFee { get; set; }

        [JsonProperty("endorsement")]
        public string endorsement { get; set; }

        [JsonProperty("abstractFee")]
        public string abstractFee { get; set; }

        [JsonProperty("parties")]
        public List<InboundTPSTransactionParty> parties { get; set; }
    }
}
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

        [JsonProperty("propertyAddress1")]
        public string propertyAddress1 { get; set; }

        [JsonProperty("propertyAddress2")]
        public string propertyAddress2 { get; set; }

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

        [JsonProperty("roleinFile")]
        public string roleinFile { get; set; }

        [JsonProperty("directingPartyFlag")]
        public string directingPartyFlag { get; set; }

        [JsonProperty("titleFee")]
        public string titleFee { get; set; }

        [JsonProperty("escrowFee")]
        public string escrowFee { get; set; }

        [JsonProperty("endorsement")]
        public string endorsement { get; set; }

        [JsonProperty("abstractFee")]
        public string abstractFee { get; set; }

        [JsonProperty("division")]
        public string division { get; set; }

        [JsonProperty("transactionCRMBrand")]
        public string transactionCRMBrand { get; set; }

        [JsonProperty("estClosingDate")]
        public string estClosingDate { get; set; }

        [JsonProperty("CRMBrandContact")]
        public string CRMBrandContact { get; set; }

        [JsonProperty("transactionAmount")]
        public string transactionAmount { get; set; }

        [JsonProperty("fileNumber")]
        public string fileNumber { get; set; }

        [JsonProperty("mls_id")]
        public string mls_id { get; set; }

        [JsonProperty("fileStatus")]
        public string fileStatus { get; set; }

        [JsonProperty("Country")]
        public string country { get; set; }

        [JsonProperty("MSA")]
        public InboundMSA[] msa { get; set; }

        [JsonProperty("parties")]
        public List<InboundTPSTransactionParty> parties { get; set; }
    }
}
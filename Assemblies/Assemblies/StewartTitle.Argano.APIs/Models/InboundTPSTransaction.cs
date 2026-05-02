using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StewartTitle.Argano.APIs.Models
{
    public class InboundTPSTransaction
    {
        [JsonPropertyName("transactionID")]
        public string transactionID { get; set; }
        [JsonPropertyName("propertyAddress1")]

        public string propertyAddress1 { get; set; }
        [JsonPropertyName("propertyAddress2")]
        public string propertyAddress2 { get; set; }

        [JsonPropertyName("propertyCity")]
        public string propertyCity { get; set; }

        [JsonPropertyName("propertyCounty")]
        public string propertyCounty { get; set; }

        [JsonPropertyName("propertyState")]
        public string propertyState { get; set; }

        [JsonPropertyName("propertyZip")]
        public string propertyZip { get; set; }

        [JsonPropertyName("loanAmount")]
        public string loanAmount { get; set; }

        [JsonPropertyName("salesPrice")]
        public string salesPrice { get; set; }

        [JsonPropertyName("transactionType")]
        public string transactionType { get; set; }

        [JsonPropertyName("BDOBranchInfo")]
        public string BDOBranchInfo { get; set; }

        [JsonPropertyName("fileStartDate")]
        public string fileStartDate { get; set; }

        [JsonPropertyName("estSettlementDate")]
        public string estSettlementDate { get; set; }

        [JsonPropertyName("finalClosingDate")]
        public string finalClosingDate { get; set; }

        [JsonPropertyName("roleinFile")]
        public string roleinFile { get; set; }

        [JsonPropertyName("directingPartyFlag")]
        public string directingPartyFlag { get; set; }

        [JsonPropertyName("titleFee")]
        public string titleFee { get; set; }

        [JsonPropertyName("escrowFee")]
        public string escrowFee { get; set; }

        [JsonPropertyName("endorsement")]
        public string endorsement { get; set; }

        [JsonPropertyName("abstractFee")]
        public string abstractFee { get; set; }

        [JsonPropertyName("division")]
        public string division { get; set; }

        [JsonPropertyName("transactionCRMBrand")]
        public string transactionCRMBrand { get; set; }

        [JsonPropertyName("estClosingDate")]
        public string estClosingDate { get; set; }

        [JsonPropertyName("CRMBrandContact")]
        public string CRMBrandContact { get; set; }

        [JsonPropertyName("transactionAmount")]
        public string transactionAmount { get; set; }
        [JsonPropertyName("fileNumber")]
        public string fileNumber { get; set; }
        [JsonPropertyName("mls_id")]
        public string mls_id { get; set; }
        [JsonPropertyName("fileStatus")]
        public string fileStatus { get; set; }
        [JsonPropertyName("Country")]
        public string country { get; set; }
        [JsonPropertyName("MSA")]
        public InboundMSA[] msa { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace StewartTitle.Argano.Plugins.Models
{
    public class ContactUpdatePayload
    {
        public class KnownEmailAddress
        {
            public string stt_emailaddress { get; set; }
        }

        public class AccountPayload
        {
            public string name { get; set; }
            public string address1_line1 { get; set; }
            public string address1_city { get; set; }
            public string address1_stateorprovince { get; set; }
            public string address1_country { get; set; }
            public string address1_postalcode { get; set; }
        }

        public class ContactPayload
        {
            public string stt_enterpriseid { get; set; }
            public string stt_uuid { get; set; }
            public string firstname { get; set; }
            public string lastname { get; set; }
            public string emailaddress1 { get; set; }
            public string telephone1 { get; set; }
            public string address1_line1 { get; set; }
            public string address1_line2 { get; set; }
            public string address1_city { get; set; }
            public string address1_stateorprovince { get; set; }
            public string address1_postalcode { get; set; }
            public string jobtitle { get; set; }
            public string address1_county { get; set; }
            public List<KnownEmailAddress> KnownEmailAddresses { get; set; }
            public AccountPayload Account { get; set; }
        }


        public List<ContactPayload> contacts { get; set; }
    }

    public class KnownEmailAddress
    {
        public string EmailAddress { get; set; }
    }

    public class ContactPayload
    {
        public string stt_enterpriseid { get; set; }

        public string stt_uuid { get; set; }

        public List<KnownEmailAddress> KnownEmailAddresses { get; set; }
    }

    public class ContactUpdateEnterpriseId
    {
        public List<ContactPayload> contacts { get; set; }
    }
}

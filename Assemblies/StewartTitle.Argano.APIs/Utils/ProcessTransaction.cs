using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using StewartTitle.Argano.APIs.Models;

namespace StewartTitle.Argano.APIs.Utils
{
    public class ProductionProcessor
    {
        private Guid accountId;
        private Guid contactId;
        private Guid transactionId;
        private Entity marketEntity;
        private Entity currentOfficeLocationBranch = null;
        private Entity currentTransaction = null;
        private EntityReference owner;
        private bool primaryEmailSet = false;
        private bool primaryPhoneSet = false;
        private string fileNumber = null;
        private string partiesStringJson = null;
        private IPluginExecutionContext context;

        public void ProcessTransaction(IOrganizationService service, ITracingService tracingService, InboundTPSTransactionItem transaction, bool isTransaction, string payloadString, IPluginExecutionContext contextExecution)
        {
            partiesStringJson = payloadString;
            context = contextExecution;
            tracingService.Trace($"ProductionProcessor.ProcessTransaction started. TransactionID: {transaction.transactionID}");

            try
            {
                #region Process
                UpsertTransaction(service, tracingService, transaction);

                foreach (InboundTPSTransactionParty party in transaction.parties)
                {
                    tracingService.Trace($"Processing party: {party.firstName} {party.lastName}, Role: {party.roleInFile}");
                    ResetPartyVariables();
                    tracingService.Trace($"{party.accountID}");
                    if (!string.IsNullOrEmpty(party.accountID) && !string.IsNullOrEmpty(party.accountName))
                    {
                        tracingService.Trace($"Inside Create Update Accoun");
                        UpsertAccount(service, tracingService, party);
                    }

                }
                #endregion

                tracingService.Trace("ProductionTransactionProcessor completed.");
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException($"ProductionTransactionProcessor.ProcessParty failed: {ex.Message}", ex);
            }
        }

        #region CREATE / UPDATE ACCOUNT - 459987
        private void UpsertAccount(IOrganizationService service, ITracingService tracingService, InboundTPSTransactionParty party)
        {
            tracingService.Trace($"Searching account: {party.accountID}");
            QueryExpression query = new QueryExpression("account");
            query.ColumnSet = new ColumnSet("accountid", "statecode");
            query.Criteria.AddCondition("stt_accountfinanceidtext", ConditionOperator.Equal, party.accountID);

            EntityCollection results = service.RetrieveMultiple(query);

            if (results.Entities.Count == 0)
            {
                tracingService.Trace("Account not found. Creating.");
                Entity account = new Entity("account");
                account["stt_accountfinanceidtext"] = party.accountID;
                account["name"] = party.accountName;
                if (!string.IsNullOrEmpty(party.accountAddress1))
                {
                    account["address1_line1"] = party.accountAddress1;
                }
                if (!string.IsNullOrEmpty(party.accountAddress2))
                {
                    account["address1_line2"] = party.accountAddress2;
                }
                if (!string.IsNullOrEmpty(party.accountCity))
                {
                    account["address1_city"] = party.accountCity;
                }
                if (!string.IsNullOrEmpty(party.accountPhoneNumber))
                {
                    account["telephone1"] = party.accountPhoneNumber;
                }
                account["stt_countyid"] = GetLookupByName(service, party.accountCounty + " County", "stt_county", "stt_countyid", "stt_name");
                account["stt_stateid"] = GetLookupByAbbr(service, party.accountState, "stt_state", "stt_stateid", "stt_abbreviation");
                account["stt_zipcodeid"] = GetLookupByName(service, party.accountZip, "stt_zipcode", "stt_zipcodeid", "stt_name");

                accountId = service.Create(account);
                tracingService.Trace($"Account created: {accountId}");
            }
            else
            {
                accountId = results.Entities[0].Id;
                tracingService.Trace($"Account found: {accountId}");
                if (results.Entities[0].GetAttributeValue<OptionSetValue>("statecode")?.Value != 0)
                {
                    Entity reactivate = new Entity("account", accountId);
                    reactivate["statecode"] = new OptionSetValue(0);
                    reactivate["statuscode"] = new OptionSetValue(1);
                    service.Update(reactivate);
                    tracingService.Trace("Account reactivated.");
                }

            }

        }

        #endregion

        #region CREATE / UPDATE CONTACT

        #endregion

        #region CREATE / UPDATE TRANSACTION - 459993
        private void UpsertTransaction(IOrganizationService service, ITracingService tracingService, InboundTPSTransactionItem transaction)
        {
            tracingService.Trace($"Searching transaction: {transaction.transactionID}");
            QueryExpression query = new QueryExpression("stt_transaction");
            query.ColumnSet = new ColumnSet("stt_transactionid", "statecode", "stt_filenumber", "stt_transactionfinanceidtext", "stt_officelocationbranchid", "stt_marketid", "ownerid");
            query.Criteria.AddCondition("stt_transactionfinanceidtext", ConditionOperator.Equal, transaction.transactionID);

            EntityCollection results = service.RetrieveMultiple(query);

            Entity createUpdateTransaction = new Entity("stt_transaction");
            bool TrsancationExists = results.Entities.Count > 0;

            if (TrsancationExists)
            {
                tracingService.Trace("Transaction found. Will update.");
                createUpdateTransaction.Id = results.Entities[0].Id;
                transactionId = createUpdateTransaction.Id;
            }
            if (!string.IsNullOrEmpty(transaction.propertyStreet1))
            {
                createUpdateTransaction["stt_address1"] = transaction.propertyStreet1;
            }
            if (!string.IsNullOrEmpty(transaction.propertyStreet2))
            {
                createUpdateTransaction["stt_address2"] = transaction.propertyStreet2;
            }
            if (!string.IsNullOrEmpty(transaction.propertyCity))
            {
                createUpdateTransaction["stt_city"] = transaction.propertyCity;
            }
            if (!string.IsNullOrEmpty(transaction.propertyCounty))
            {
                createUpdateTransaction["stt_countyid"] = GetLookupByName(service, transaction.propertyCounty + " County", "stt_county", "stt_countyid", "stt_name");
            }
            if (!string.IsNullOrEmpty(transaction.propertyState))
            {
                createUpdateTransaction["stt_stateid"] = GetLookupByAbbr(service, transaction.propertyState, "stt_state", "stt_stateid", "stt_abbreviation");
            }
            if (!string.IsNullOrEmpty(transaction.propertyZip))
            {
                createUpdateTransaction["stt_zipcodeid"] = GetLookupByName(service, transaction.propertyZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
            }
            if (!string.IsNullOrEmpty(transaction.salesPrice))
            {
                createUpdateTransaction["stt_salesprice"] = new Money(Convert.ToDecimal(transaction.salesPrice));
            }
            if (!string.IsNullOrEmpty(transaction.loanAmount))
            {
                createUpdateTransaction["stt_loanamount"] = new Money(Convert.ToDecimal(transaction.loanAmount));
            }
            if (!string.IsNullOrEmpty(transaction.titleFee))
            {
                createUpdateTransaction["stt_titlefee"] = new Money(Convert.ToDecimal(transaction.titleFee));
            }
            if (!string.IsNullOrEmpty(transaction.escrowFee))
            {
                createUpdateTransaction["stt_escrowfee"] = new Money(Convert.ToDecimal(transaction.escrowFee));
            }
            if (!string.IsNullOrEmpty(transaction.endorsement))
            {
                createUpdateTransaction["stt_endorsementfee"] = new Money(Convert.ToDecimal(transaction.endorsement));
            }
            if (!string.IsNullOrEmpty(transaction.abstractFee))
            {
                createUpdateTransaction["stt_abstractfee"] = new Money(Convert.ToDecimal(transaction.abstractFee));
            }
            if (!string.IsNullOrEmpty(transaction.BDOBranchInfo))
            {
                createUpdateTransaction["stt_titlecompany"] = transaction.BDOBranchInfo;
            }
            if (!string.IsNullOrEmpty(transaction.fileStartDate))
            {
                createUpdateTransaction["stt_filestarton"] = DateTime.Parse(transaction.fileStartDate);
            }
            if (!string.IsNullOrEmpty(transaction.finalClosingDate))
            {
                createUpdateTransaction["stt_finalcloseon"] = DateTime.Parse(transaction.finalClosingDate);
            }
            if (!string.IsNullOrEmpty(transaction.estSettlementDate))
            {
                createUpdateTransaction["stt_estimatedclosedon"] = DateTime.Parse(transaction.estSettlementDate);
            }
            if (!string.IsNullOrEmpty(transaction.transactionType))
            {
                switch (transaction.transactionType.ToLower())
                {
                    case "new sales":
                        createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(924510000); break;
                    case "resale":
                        createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(924510001); break;
                    case "refi":
                        createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(924510002); break;
                    default:
                        createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(198730001); break;
                }
            }
            if (!string.IsNullOrEmpty(transaction.fileStatus))
            {
                switch (transaction.fileStatus.ToLower())
                {
                    case "open":
                        createUpdateTransaction["stt_transactionstatuscode"] = new OptionSetValue(924510000); break;
                    case "closed":
                        createUpdateTransaction["stt_transactionstatuscode"] = new OptionSetValue(924510001); break;
                    case "cancelled":
                        createUpdateTransaction["stt_transactionstatuscode"] = new OptionSetValue(924510002); break;
                }
            }
            createUpdateTransaction["stt_istransaction"] = false; // Production
            createUpdateTransaction["stt_filenumber"] = transaction.fileNumber;
            if (!TrsancationExists)
            {
                createUpdateTransaction["stt_transactionfinanceidtext"] = transaction.transactionID;
                UpsertRequest upsertRequest = new UpsertRequest { Target = createUpdateTransaction };
                UpsertResponse upsertResponse = (UpsertResponse)service.Execute(upsertRequest);
                transactionId = upsertResponse.Target.Id;
                tracingService.Trace($"Transaction created/upserted: {transactionId}");
            }
            else
            {
                service.Update(createUpdateTransaction);
                tracingService.Trace($"Transaction updated: {transactionId}");
            }

            fileNumber = transaction.fileNumber;

        }
        #endregion

        #region CREATE / UPDATE TRANSACTION ROLE

        #endregion

        #region Helpers
        private void ResetPartyVariables()
        {
            accountId = Guid.Empty;
            contactId = Guid.Empty;

            primaryEmailSet = false;
            primaryPhoneSet = false;
        }
        private EntityReference GetLookupByName(IOrganizationService service, string name, string entityName, string keyColumn, string nameColumn)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            QueryExpression query = new QueryExpression(entityName);
            query.ColumnSet = new ColumnSet(keyColumn);
            query.Criteria.AddCondition(nameColumn, ConditionOperator.Equal, name);

            EntityCollection results = service.RetrieveMultiple(query);

            return results.Entities.Count > 0
                ? new EntityReference(entityName, results.Entities[0].GetAttributeValue<Guid>(keyColumn))
                : null;
        }
        private EntityReference GetLookupByAbbr(IOrganizationService service, string abbr, string entityName, string keyColumn, string abbrColumn)
        {
            if (string.IsNullOrEmpty(abbr))
                return null;

            QueryExpression query = new QueryExpression(entityName);
            query.ColumnSet = new ColumnSet(keyColumn);
            query.Criteria.AddCondition(abbrColumn, ConditionOperator.Equal, abbr);

            EntityCollection results = service.RetrieveMultiple(query);

            return results.Entities.Count > 0
                ? new EntityReference(entityName, results.Entities[0].GetAttributeValue<Guid>(keyColumn))
                : null;
        }
        #endregion
    }
}
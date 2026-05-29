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

        public void ProcessTransaction(IOrganizationService service, ITracingService tracingService, InboundTPSTransactionParty party, InboundTPSTransactionItem transaction, bool isTransaction, string payloadString, IPluginExecutionContext contextExecution)
        {
            ResetVariables();
            partiesStringJson = payloadString;
            context = contextExecution;
            tracingService.Trace($"ProductionTransactionProcessor started.");

            if (isTransaction)
            {
                tracingService.Trace("isTransaction = true passed to ProductionTransactionProcessor. This processor only handles Production (false). Exiting.");
                return;
            }

            try
            {
                #region Process
                if (party.Account != null && !string.IsNullOrEmpty(party.Account.accountID) && !string.IsNullOrEmpty(party.Account.accountName))
                {
                    UpsertAccount(service, tracingService, party);
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
            tracingService.Trace($"Searching account: {party.Account.accountID}");

            QueryExpression accountQuery = new QueryExpression("account");
            accountQuery.ColumnSet = new ColumnSet("accountid", "statecode", "name", "address1_line1", "address1_line2", "address1_city", "telephone1", "stt_countyid", "stt_zipcodeid", "stt_stateid");
            accountQuery.Criteria.AddCondition("stt_accountfinanceidtext", ConditionOperator.Equal, party.Account.accountID);

            EntityCollection existing = service.RetrieveMultiple(accountQuery);

            if (existing.Entities.Count == 0)
            {
                tracingService.Trace("Account not found - creating new account");
                Entity account = new Entity("account");

                account["stt_accountfinanceidtext"] = party.Account.accountID;
                account["name"] = party.Account.accountName;

                if (!string.IsNullOrEmpty(party.Account.accountAddress1))
                {
                    account["address1_line1"] = party.Account.accountAddress1;
                }
                if (!string.IsNullOrEmpty(party.Account.accountAddress2))
                {
                    account["address1_line2"] = party.Account.accountAddress2;
                }
                if (!string.IsNullOrEmpty(party.Account.accountCity))
                {
                    account["address1_city"] = party.Account.accountCity;
                }
                if (!string.IsNullOrEmpty(party.Account.accountPhoneNumber))
                {
                    account["telephone1"] = party.Account.accountPhoneNumber;
                }
                account["stt_countyid"] = GetLookupByName(service, party.Account.accountCounty + " County", "stt_county", "stt_countyid", "stt_name");
                account["stt_zipcodeid"] = GetLookupByName(service, party.Account.accountZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
                account["stt_stateid"] = GetLookupByAbbr(service, party.Account.accountState, "stt_state", "stt_stateid", "stt_abbreviation");
                accountId = service.Create(account);
                tracingService.Trace($"Account created: {accountId}");
            }
            else
            {
                Entity existingAccount = existing.Entities[0];
                accountId = existingAccount.Id;
                tracingService.Trace($"Account found - {accountId}. Updating.");
                Entity accountToUpdate = new Entity("account") { Id = accountId };

                bool hasChanges = false;

                if (existingAccount.GetAttributeValue<OptionSetValue>("statecode")?.Value != 0)
                {
                    accountToUpdate["statecode"] = new OptionSetValue(0);
                    accountToUpdate["statuscode"] = new OptionSetValue(1);
                    hasChanges = true;
                    tracingService.Trace("Account marked for reactivation.");
                }

                if (!string.IsNullOrEmpty(party.Account.accountName) && existingAccount.GetAttributeValue<string>("name") != party.Account.accountName)
                {
                    accountToUpdate["name"] = party.Account.accountName;
                    hasChanges = true;
                }
                if (!string.IsNullOrEmpty(party.Account.accountAddress1) && existingAccount.GetAttributeValue<string>("address1_line1") != party.Account.accountAddress1)
                {
                    accountToUpdate["address1_line1"] = party.Account.accountAddress1;
                    hasChanges = true;
                }
                if (!string.IsNullOrEmpty(party.Account.accountAddress2) && existingAccount.GetAttributeValue<string>("address1_line2") != party.Account.accountAddress2)
                {
                    accountToUpdate["address1_line2"] = party.Account.accountAddress2;
                    hasChanges = true;
                }
                if (!string.IsNullOrEmpty(party.Account.accountCity) && existingAccount.GetAttributeValue<string>("address1_city") != party.Account.accountCity)
                {
                    accountToUpdate["address1_city"] = party.Account.accountCity;
                    hasChanges = true;
                }
                if (!string.IsNullOrEmpty(party.Account.accountPhoneNumber) && existingAccount.GetAttributeValue<string>("telephone1") != party.Account.accountPhoneNumber)
                {
                    accountToUpdate["telephone1"] = party.Account.accountPhoneNumber;
                    hasChanges = true;
                }
                accountToUpdate["stt_countyid"] = GetLookupByName(service, party.Account.accountCounty + " County", "stt_county", "stt_countyid", "stt_name");
                accountToUpdate["stt_zipcodeid"] = GetLookupByName(service, party.Account.accountZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
                accountToUpdate["stt_stateid"] = GetLookupByAbbr(service, party.Account.accountState, "stt_state", "stt_stateid", "stt_abbreviation");
                hasChanges = true;

                if (hasChanges)
                {
                    service.Update(accountToUpdate);
                    tracingService.Trace($"Account updated: {accountId}");
                }
                else
                {
                    tracingService.Trace($"Account already up to date: {accountId}");
                }
            }
        }
        #endregion

        #region CREATE / UPDATE CONTACT

        #endregion

        #region CREATE / UPDATE TRANSACTION - 459993
        public void ProcessTransactionRecord(IOrganizationService service, ITracingService tracingService, InboundTPSTransactionItem transaction, Guid directingContactId)
        {
            Entity transactionEntity = currentTransaction != null ? new Entity("stt_transaction") { Id = currentTransaction.Id } : new Entity("stt_transaction");
            bool transactionExists = currentTransaction != null;
            //bool txExists = currentTransaction != null && !currentTransaction.Id.Equals(Guid.Empty);

            if (transactionExists)
            {
                transactionEntity.Id = currentTransaction.Id;
                tracingService.Trace($"Transaction exists: {transactionEntity.Id}");
            }

            if (!string.IsNullOrEmpty(transaction.propertyAddress1))
            {
                transactionEntity["stt_address1"] = transaction.propertyAddress1;
            }

            if (!string.IsNullOrEmpty(transaction.propertyAddress2))
            {
                transactionEntity["stt_address2"] = transaction.propertyAddress2;
            }

            if (!string.IsNullOrEmpty(transaction.propertyCity))
            {
                transactionEntity["stt_city"] = transaction.propertyCity;
            }
            if (!string.IsNullOrEmpty(transaction.propertyCounty))
            {
                transactionEntity["stt_countyid"] = GetLookupByName(service, transaction.propertyCounty + " County", "stt_county", "stt_countyid", "stt_name");
            }

            if (!string.IsNullOrEmpty(transaction.propertyState))
            {
                transactionEntity["stt_stateid"] = GetLookupByAbbr(service, transaction.propertyState, "stt_state", "stt_stateid", "stt_abbreviation");
            }
            if (!string.IsNullOrEmpty(transaction.propertyZip))
            {
                transactionEntity["stt_zipcodeid"] = GetLookupByName(service, transaction.propertyZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
            }
            if (!string.IsNullOrEmpty(transaction.transactionType))
            {
                switch (transaction.transactionType.ToLower())
                {
                    case "new sales":
                        transactionEntity["stt_transactiontypecode"] = new OptionSetValue(924510000);
                        break;
                    case "resale":
                        transactionEntity["stt_transactiontypecode"] = new OptionSetValue(924510001);
                        break;
                    case "refi":
                        transactionEntity["stt_transactiontypecode"] = new OptionSetValue(924510002);
                        break;
                    default:
                        transactionEntity["stt_transactiontypecode"] = new OptionSetValue(198730001);
                        break;
                }
            }
            if (!string.IsNullOrEmpty(transaction.fileStatus))
            {
                switch (transaction.fileStatus.ToLower())
                {
                    case "open":
                        transactionEntity["stt_transactionstatuscode"] = new OptionSetValue(924510000);
                        break;
                    case "closed":
                        transactionEntity["stt_transactionstatuscode"] = new OptionSetValue(924510000);
                        break;
                    case "cancelled":
                        transactionEntity["stt_transactionstatuscode"] = new OptionSetValue(924510000);
                        break;
                }
            }
            if (!string.IsNullOrEmpty(transaction.loanAmount))
            {
                transactionEntity["stt_loanamount"] = new Money(Convert.ToDecimal(transaction.loanAmount));
            }
            if (!string.IsNullOrEmpty(transaction.salesPrice))
            {
                transactionEntity["stt_salesprice"] = new Money(Convert.ToDecimal(transaction.salesPrice));
            }
            if (!string.IsNullOrEmpty(transaction.titleFee))
            {
                transactionEntity["stt_titlefee"] = new Money(Convert.ToDecimal(transaction.titleFee));
            }
            if (!string.IsNullOrEmpty(transaction.escrowFee))
            {
                transactionEntity["stt_escrowfee"] = new Money(Convert.ToDecimal(transaction.escrowFee));
            }
            transactionEntity["stt_istransaction"] = false;

            transactionEntity["stt_filenumber"] = transaction.fileNumber;

            if (!transactionExists)
            {
                transactionEntity["stt_transactionfinanceidtext"] = transaction.transactionID;
                UpsertRequest upsertReq = new UpsertRequest { Target = transactionEntity };
                UpsertResponse upsertResp = (UpsertResponse)service.Execute(upsertReq);
                transactionId = upsertResp.Target.Id;
                tracingService.Trace($"Transaction created/upserted: {transactionId}");
            }
            else
            {
                service.Update(transactionEntity);
                tracingService.Trace($"Transaction updated: {transactionId}");
            }
        }

        #endregion

        #region CREATE / UPDATE TRANSACTION ROLE

        #endregion

        #region Helpers
        private void ResetVariables()
        {
            accountId = Guid.Empty;
            contactId = Guid.Empty;
            transactionId = Guid.Empty;

            marketEntity = null;
            currentOfficeLocationBranch = null;
            currentTransaction = null;
            owner = null;
            fileNumber = null;

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
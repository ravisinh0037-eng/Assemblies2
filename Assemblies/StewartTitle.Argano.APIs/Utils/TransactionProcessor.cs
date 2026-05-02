using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using StewartTitle.Argano.APIs.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using static System.Data.Entity.Infrastructure.Design.Executor;

namespace StewartTitle.Argano.APIs.Utils
{
    public class TransactionProcessor
    {
        private Guid accountId;
        private Guid contactId;
        private Guid transactionId;
        private Entity marketEntity;
        private Entity currentOfficeLocationBranch = null;
        private Entity currentTransaction = null;
        private EntityReference stt_directingclientid = null;
        private EntityReference owner;
        private bool primaryAddressSet = false;
        private bool primaryEmailSet = false;
        private bool primaryPhoneSet = false;
        private string fileNumber = null;
        private bool isTPS = false;
        private string partiesStringJson = null;
        private IPluginExecutionContext context;
        // Stores the BDO user
        private Entity bdoUser = null;
        bool isHouseBdoAssigned = false;
        private EntityReference houseAccountUser = null;
        bool isHousePayload = false;
        // Check if the current party contains transactions with BDO or Contact roles
        bool isBDO = false;
        bool isContact = false;
        bool isBDOuser = false;
        private EntityReference GetLookupByAbbr(IOrganizationService service, String abbr, String entity, String keyColumn, String abbrColumn)
        {
            if (!String.IsNullOrEmpty(abbr))
            {
                QueryExpression sequenceQuery = new QueryExpression(entity);
                sequenceQuery.ColumnSet = new ColumnSet(keyColumn);
                sequenceQuery.Criteria.AddCondition(abbrColumn, ConditionOperator.Equal, abbr);
                EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);
                if (sequenceEntities.Entities.Count == 0)
                {
                    return null;
                }
                else
                {
                    return new EntityReference(entity, sequenceEntities[0].GetAttributeValue<Guid>(keyColumn));
                }
            }
            else
            {
                return null;
            }

        }
        public int CountRecordsToProcess(ITracingService tracingService, InboundTPSParties parties)
        {
            int totalRecords = 0;
            foreach (InboundTPSParty party in parties.Parties)
            {
                tracingService.Trace("Calculating amount of records...");
                if (party.Account != null && !String.IsNullOrEmpty(party.Account.accountName) && !String.IsNullOrEmpty(party.Account.accountID))  // If there's an account to process
                {
                    totalRecords += 1;  // Total records + 1 account
                }
                if (!String.IsNullOrEmpty(party.UUID) && (!String.IsNullOrEmpty(party.enterpriseID) || (!String.IsNullOrEmpty(party.firstName) && !String.IsNullOrEmpty(party.lastName))))  // If there's a contact (and possibly an address, phone number and email) to process
                {
                    totalRecords += 4;  // Total records + 1 Contact + 1 Address + 1 Phone Number + 1 Email
                }
                if (party.Transactions != null)
                {
                    totalRecords += party.Transactions.Length * 2;    // Total records + every transaction of the party + every transaction role of the party
                }
            }

            return totalRecords;
        }
        private void FindUser(IOrganizationService service, ITracingService tracingService, InboundTPSParty party)
        {
            // Retrieve User
            tracingService.Trace("Searching BDO user...");
            if (!String.IsNullOrEmpty(party.primaryEmail))
            {
                QueryExpression sequenceQuery = new QueryExpression("systemuser");
                sequenceQuery.ColumnSet = new ColumnSet("systemuserid");
                FilterExpression filter = new FilterExpression(LogicalOperator.Or);
                filter.AddCondition("internalemailaddress", ConditionOperator.Equal, party.primaryEmail);
                filter.AddCondition("domainname", ConditionOperator.Equal, party.primaryEmail);
                sequenceQuery.Criteria.AddFilter(filter);
                EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);
                if (sequenceEntities.Entities.Count > 0)
                {
                    tracingService.Trace("BDO user found.");
                    bdoUser = sequenceEntities[0];
                    isBDOuser = true; 
                }
                else
                {
                    tracingService.Trace("BDO user not found.");
                    isContact = true;
                }
            }
            else
            {
                tracingService.Trace("BDO requires primary Email to search for user. No user will be assigned.");
            }

        }
        private void SearchTeamForOwner(IOrganizationService service, ITracingService tracingService)
        {
            QueryExpression ownerQuery = new QueryExpression("team");
            ownerQuery.ColumnSet = new ColumnSet("teamid");
            ownerQuery.Criteria.AddCondition("name", ConditionOperator.Equal, "MLS Visibility Team");
            EntityCollection ownerEntities = service.RetrieveMultiple(ownerQuery);
            if (ownerEntities.Entities.Count > 0)
            {
                owner = new EntityReference("team", ownerEntities[0].GetAttributeValue<Guid>("teamid"));
                tracingService.Trace($"Team 'MLS Visibility Team' found.");
            }
            else
            {
                tracingService.Trace("Team 'MLS Visibility Team' not found.");
            }
        }
        private void SearchOfficeLocationBranch(IOrganizationService service, ITracingService tracingService, InboundTPSTransaction transaction)
        {
            tracingService.Trace("inside SearchOfficeLocationBranch");
            // Set market, owner and office location branch 
            marketEntity = null;
            owner = null;
            currentOfficeLocationBranch = null;
            string bdoBranchInfo = transaction.BDOBranchInfo;
            if (!String.IsNullOrEmpty(bdoBranchInfo))
            {
                QueryExpression olbQuery = new QueryExpression("stt_officelocationbranch");
                olbQuery.ColumnSet = new ColumnSet("stt_officelocationbranchid", "stt_brandid", "stt_marketid");
                olbQuery.Criteria.AddCondition("stt_name", ConditionOperator.Equal, bdoBranchInfo);
                EntityCollection olbEntities = service.RetrieveMultiple(olbQuery);
                tracingService.Trace("olbEntities.Entities.Count =" + olbEntities.Entities.Count);
                if (olbEntities.Entities.Count > 0)
                {
                    tracingService.Trace($"Office Location Branch: {olbEntities.Entities[0].Id}");
                    currentOfficeLocationBranch = olbEntities.Entities[0];
                    if (isTPS)
                    {
                        EntityReference marketRef = olbEntities.Entities[0].GetAttributeValue<EntityReference>("stt_marketid");
                        if (marketRef != null)
                        {
                            tracingService.Trace($"Market Id: {marketRef.Id}");
                            marketEntity = service.Retrieve("stt_market", marketRef.Id, new ColumnSet("stt_marketid", "ownerid"));
                            if (marketEntity != null)
                            {
                                tracingService.Trace($"Market found: {marketRef.Id}");
                                owner = marketEntity.GetAttributeValue<EntityReference>("ownerid");
                            }
                            else
                            {
                                tracingService.Trace("Market not found.");
                            }
                        }
                    }
                }
                else
                {
                    tracingService.Trace("BDOBranchInfo is not valid. No Office Location Branch found.");
                }
            }
            else
            {
                tracingService.Trace("BDOBranchInfo is required.");
            }
        }
        private void SetTransactionVariables(IOrganizationService service, ITracingService tracingService, InboundTPSParty party, InboundTPSTransaction transaction)
        {
            //Check if transaction changed
            if (currentTransaction == null || currentTransaction.GetAttributeValue<string>("stt_transactionfinanceidtext") != transaction.transactionID)
            {
                //Search for new transaction if exists
                tracingService.Trace("Searching transaction...");
                QueryExpression sequenceQuery = new QueryExpression("stt_transaction");
                sequenceQuery.ColumnSet = new ColumnSet("stt_transactionid", "statecode", "stt_filenumber", "stt_transactionfinanceidtext", "stt_officelocationbranchid", "stt_marketid", "ownerid", "stt_directingclientid");
                sequenceQuery.Criteria.AddCondition("stt_transactionfinanceidtext", ConditionOperator.Equal, transaction.transactionID);
                EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);

                if (sequenceEntities.Entities.Count > 0)
                {
                    // Transaction found
                    tracingService.Trace("Transaction found.");
                    currentTransaction = sequenceEntities.Entities[0];

                    if (String.IsNullOrEmpty(transaction.fileNumber))
                    {
                        fileNumber = currentTransaction.GetAttributeValue<string>("stt_filenumber");
                    }
                    else
                    {
                        fileNumber = transaction.fileNumber;
                    }
                    tracingService.Trace($"File number: {fileNumber}.");

                    if (String.IsNullOrEmpty(transaction.BDOBranchInfo))
                    {
                        if (currentTransaction.GetAttributeValue<EntityReference>("stt_officelocationbranchid") != null)
                        {
                            currentOfficeLocationBranch = new Entity("stt_officelocationbranch", currentTransaction.GetAttributeValue<EntityReference>("stt_officelocationbranchid").Id);
                        }

                        if (isTPS)
                        {
                            if (currentTransaction.GetAttributeValue<EntityReference>("stt_marketid") != null)
                            {
                                marketEntity = new Entity("stt_market", currentTransaction.GetAttributeValue<EntityReference>("stt_marketid").Id);
                            }

                            owner = currentTransaction.GetAttributeValue<EntityReference>("ownerid");
                        }
                    }
                    else
                    {
                        currentTransaction["stt_zzzbranch"] = transaction.BDOBranchInfo;
                        SearchOfficeLocationBranch(service, tracingService, transaction);
                    }
                    Guid transactioID = currentTransaction.Id;
                    currentTransaction = new Entity(currentTransaction.LogicalName);
                    currentTransaction.Id = transactioID;
                    transactionId = transactioID;
                    tracingService.Trace($"transactionId inside SetTransactionVariables: {transactionId}.");
                }
                else
                {
                    // Transaction wasn't found. It will be created later.
                    tracingService.Trace("Transaction not found. It'll be created later.");
                    // currentTransaction = null;
                    currentTransaction = new Entity("stt_transaction");
                    tracingService.Trace("transaction.fileNumber=" + transaction.fileNumber);
                    if (String.IsNullOrEmpty(transaction.fileNumber))
                    {
                        fileNumber = null;
                        tracingService.Trace("File number is required to create a transaction.");
                    }
                    else
                    {
                        tracingService.Trace("isTPS=" + isTPS);
                        if (isTPS)
                        {
                            fileNumber = transaction.fileNumber;
                        }
                        else
                        {
                            tracingService.Trace("transaction.mls_id=" + transaction.mls_id);
                            fileNumber = transaction.mls_id;
                        }
                    }
                    if (String.IsNullOrEmpty(transaction.BDOBranchInfo))
                    {
                        if (isTPS)
                        {
                            marketEntity = null;
                            owner = null;
                            currentOfficeLocationBranch = null;

                            tracingService.Trace("BDOBranchInfo is required to create a transaction.");
                        }
                    }
                    else
                    {
                        tracingService.Trace("transaction.BDOBranchInfo=" + transaction.BDOBranchInfo);
                        tracingService.Trace("before .currentTransaction['stt_zzzbranch'] = transaction.BDOBranchInfo");
                        currentTransaction["stt_zzzbranch"] = transaction.BDOBranchInfo;
                        tracingService.Trace("after .currentTransaction['stt_zzzbranch'] = transaction.BDOBranchInfo");
                        SearchOfficeLocationBranch(service, tracingService, transaction);
                    }
                }
            }
            tracingService.Trace("before //Set MLS Owner=");
            //Set MLS Owner
            if (!isTPS)
            {
                SearchTeamForOwner(service, tracingService);
            }
        }
        private void UpsertAccount(IOrganizationService service, ITracingService tracingService, InboundTPSParty party)
        {
            // Retrieve Account
            tracingService.Trace("Searching account...");
            QueryExpression sequenceQuery = new QueryExpression("account");
            sequenceQuery.ColumnSet = new ColumnSet("accountid", "statecode");
            sequenceQuery.Criteria.AddCondition("stt_accountfinanceidtext", ConditionOperator.Equal, party.Account.accountID);
            EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);
            bool isNewAccount = false;
            // If account doesn't exist in D365, it will be created
            if (sequenceEntities.Entities.Count == 0)
            {
                tracingService.Trace("Account doesn't exist. It will be created.");
                Entity accountEntity = new Entity("account");
                accountEntity["stt_accountfinanceidtext"] = party.Account.accountID;
                accountEntity["name"] = party.Account.accountName;
                if (!String.IsNullOrEmpty(party.Account.accountAddress1))
                {
                    accountEntity["address1_line1"] = party.Account.accountAddress1;
                }
                if (!String.IsNullOrEmpty(party.Account.accountAddress2))
                {
                    accountEntity["address1_line2"] = party.Account.accountAddress2;
                }
                if (!String.IsNullOrEmpty(party.Account.accountCity))
                {
                    accountEntity["address1_city"] = party.Account.accountCity;
                }
                accountEntity["stt_countyid"] = GetLookupByAbbr(service, party.Account.accountCounty + " County", "stt_county", "stt_countyid", "stt_name");
                accountEntity["stt_stateid"] = GetLookupByAbbr(service, party.Account.accountState, "stt_state", "stt_stateid", "stt_abbreviation");
                accountEntity["stt_zipcodeid"] = GetLookupByAbbr(service, party.Account.accountZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
                if (!String.IsNullOrEmpty(party.Account.accountPhoneNumber))
                {
                    accountEntity["telephone1"] = party.Account.accountPhoneNumber;
                }
                accountId = service.Create(accountEntity);
                isNewAccount = true;



                if (isHousePayload && isNewAccount && string.IsNullOrEmpty(party.enterpriseID) && string.IsNullOrEmpty(party.UUID))
                {
                    tracingService.Trace("House payload and Null EnterpriseID/UUID detected. Attaching Global Dummy Contact.");
                    QueryExpression dummyContact = new QueryExpression("contact");
                    dummyContact.ColumnSet = new ColumnSet("contactid", "parentcustomerid", "fullname", "emailaddress1");
                    dummyContact.Criteria.AddCondition("fullname", ConditionOperator.Equal, "Dummy Contact House Account");
                    dummyContact.Criteria.AddCondition("emailaddress1", ConditionOperator.Equal, "dummycontact@stewart.com");
                    EntityCollection HouseContacts = service.RetrieveMultiple(dummyContact);
                    if (HouseContacts.Entities.Count == 0)
                    {
                        throw new InvalidPluginExecutionException("House Account Global Contact not found. Configuration error.");
                    }

                    Entity houseContact = HouseContacts.Entities[0];
                    //Entity accountUpdate = new Entity("account", accountId);
                    //accountUpdate["primarycontactid"] = new EntityReference("contact", houseContact.Id);
                    Entity contactUpdate = new Entity("contact", houseContact.Id);
                    contactUpdate["stt_accountid"] = new EntityReference("account", accountId);

                    service.Update(contactUpdate);
                    tracingService.Trace($"House Account: Global Dummy Contact attached as Primary Contact. AccountId={accountId}, ContactId={houseContact.Id}");
                }
                tracingService.Trace($"Account created with accountID {accountId}");
            }
            else
            {
                accountId = sequenceEntities[0].GetAttributeValue<Guid>("accountid");
                if (sequenceEntities[0].GetAttributeValue<OptionSetValue>("statecode").Value != 0)
                {
                    sequenceEntities[0]["statecode"] = new OptionSetValue(0);
                    service.Update(sequenceEntities[0]);
                    tracingService.Trace("Account reactivated.");
                }
                else
                {
                    tracingService.Trace("Account already active.");
                }

                if (isHousePayload)
                {
                    EntityReference primaryContactRef = sequenceEntities[0].GetAttributeValue<EntityReference>("stt_accountid");

                    if (primaryContactRef == null)
                    {
                        tracingService.Trace("House payload: Account primary contact is NULL. Attaching dummy contact.");

                        QueryExpression dummyContact = new QueryExpression("contact");
                        dummyContact.ColumnSet = new ColumnSet("contactid");
                        dummyContact.Criteria.AddCondition("fullname", ConditionOperator.Equal, "Dummy Contact House Account");
                        dummyContact.Criteria.AddCondition("emailaddress1", ConditionOperator.Equal, "dummycontact@stewart.com");

                        EntityCollection houseContacts = service.RetrieveMultiple(dummyContact);

                        if (houseContacts.Entities.Count == 0)
                        {
                            throw new InvalidPluginExecutionException("House Account Global Contact not found. Configuration error.");
                        }

                        Entity houseContact = houseContacts.Entities[0];

                        //Entity accountUpdate = new Entity("account", accountId);
                        //accountUpdate["primarycontactid"] = new EntityReference("contact", houseContact.Id);
                        Entity contactUpdate = new Entity("contact", houseContact.Id);
                        contactUpdate["stt_accountid"] = new EntityReference("account", accountId);

                        service.Update(contactUpdate);

                        tracingService.Trace($"House Account: Dummy contact attached. " + $"AccountId={accountId}, ContactId={houseContact.Id}");
                    }
                    else
                    {
                        tracingService.Trace($"House payload: Primary contact already exists: {primaryContactRef.Id}");
                    }
                }
            }

        }

        private Entity checkIfContactExists(IOrganizationService service, ITracingService tracingService, InboundTPSParty party, out bool contactExists)
        {
            EntityCollection contactEntities = new EntityCollection();
            if (!String.IsNullOrEmpty(party.enterpriseID))
            {
                tracingService.Trace("Searching contact by enterprise ID...");
                QueryExpression contactByEntIDQuery = new QueryExpression("contact");
                contactByEntIDQuery.ColumnSet = new ColumnSet("contactid", "stt_contactfinanceidtext", "stt_enterpriseid", "stt_sourceid", "firstname", "lastname",
                    "statecode", "accountid", "mobilephone", "telephone1", "emailaddress1", "address1_line1", "address1_line2",
                    "address1_city", "stt_countyid", "stt_stateid", "stt_zipcodeid", "stt_fieldchangesreswareside");
                contactByEntIDQuery.Criteria.AddCondition("stt_enterpriseid", ConditionOperator.Equal, party.enterpriseID);
                contactEntities = service.RetrieveMultiple(contactByEntIDQuery);
            }

            if (contactEntities.Entities.Count == 0)
            {
                if (!String.IsNullOrEmpty(party.multiMatch) && party.multiMatch.ToLower() == "true" && party.matchedEnterpriseID != null
                    && party.matchedEnterpriseID.Count() > 0 && !String.IsNullOrEmpty(party.matchedEnterpriseID[0]))
                {
                    tracingService.Trace("Searching contact by first matched enterprise ID...");
                    int lowerID = -1;
                    string stlowerID = "";
                    try
                    {
                        foreach (string entIDString in party.matchedEnterpriseID)
                        {
                            int enID = int.Parse(entIDString);
                            if (lowerID == -1 || enID < lowerID)
                            {
                                lowerID = enID;
                                stlowerID = entIDString;
                            }
                        }

                        if (lowerID != -1)
                        {
                            tracingService.Trace($"Comparing Enterprise ID with {lowerID.ToString()}");
                            QueryExpression contactByMatchEntIDQuery = new QueryExpression("contact");
                            contactByMatchEntIDQuery.ColumnSet = new ColumnSet("contactid", "stt_contactfinanceidtext", "stt_enterpriseid", "stt_sourceid", "firstname", "lastname",
                    "statecode", "accountid", "mobilephone", "telephone1", "emailaddress1", "address1_line1", "address1_line2",
                    "address1_city", "stt_countyid", "stt_stateid", "stt_zipcodeid", "stt_fieldchangesreswareside");
                            contactByMatchEntIDQuery.Criteria.AddCondition("stt_enterpriseid", ConditionOperator.Equal, stlowerID);
                            contactByMatchEntIDQuery.Criteria.AddCondition("stt_sourceid", ConditionOperator.Equal, party.partyID);
                            contactEntities = service.RetrieveMultiple(contactByMatchEntIDQuery);
                        }
                        else
                        {
                            tracingService.Trace("Related Enterprise ID list is empty. Not a match with that field.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Possibly a fail trying to convert an Enterprise ID to integer.
                        throw new InvalidPluginExecutionException($"{ex.Message}", ex);
                    }
                }

                if (contactEntities.Entities.Count == 0)
                {
                    if (!String.IsNullOrEmpty(party.UUID))
                    {
                        tracingService.Trace("Searching contact by contact finance ID...");
                        QueryExpression contactByFinIDQuery = new QueryExpression("contact");
                        contactByFinIDQuery.ColumnSet = new ColumnSet("contactid", "stt_contactfinanceidtext", "stt_enterpriseid", "stt_sourceid", "firstname", "lastname",
                    "statecode", "accountid", "mobilephone", "telephone1", "emailaddress1", "address1_line1", "address1_line2",
                    "address1_city", "stt_countyid", "stt_stateid", "stt_zipcodeid", "stt_fieldchangesreswareside");
                        contactByFinIDQuery.Criteria.AddCondition("stt_contactfinanceidtext", ConditionOperator.Equal, party.UUID);
                        contactEntities = service.RetrieveMultiple(contactByFinIDQuery);
                    }

                    if (contactEntities.Entities.Count == 0)
                    {
                        tracingService.Trace("Contact not found");
                        contactExists = false;
                        return new Entity("contact");
                    }
                    else
                    {
                        tracingService.Trace("Contact found by Contact Finance ID.");
                        contactExists = true;
                        return contactEntities[0];
                    }
                }
                else
                {
                    tracingService.Trace("Contact found by first Matched Enterprise ID.");
                    contactExists = true;
                    return contactEntities[0];
                }
            }
            else
            {
                tracingService.Trace("Contact found by Enterprise ID.");
                contactExists = true;
                return contactEntities[0];
            }
        }
        private void UpsertContact(IOrganizationService service, ITracingService tracingService, InboundTPSParty party)
        {
            int stateCodeValue = 0;
            int oldStateCodeValue = 0;
            bool flagActiveContactTrasnform = false;
            // Retrieve contact
            bool contactExists;
            Entity contactEntity = checkIfContactExists(service, tracingService, party, out contactExists);

            if (contactExists)
            {
                stateCodeValue = contactEntity.GetAttributeValue<OptionSetValue>("statecode").Value;
                oldStateCodeValue = stateCodeValue;
                tracingService.Trace("stateCodeValue=" + stateCodeValue);
                if (stateCodeValue == 1)//stateCodeValue == 1, mean existing contact is inactive
                {

                    Guid vOldContactId = contactEntity.Id; ;
                    tracingService.Trace("vOldContactId=" + vOldContactId);
                    QueryExpression transformQuery = new QueryExpression("stt_contacttransform");
                    transformQuery.ColumnSet = new ColumnSet("stt_mergetocontactid");
                    transformQuery.Criteria.AddCondition("stt_mergefromcontactid", ConditionOperator.Equal, vOldContactId.ToString());
                   // transformQuery.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
                    EntityCollection transformResults = service.RetrieveMultiple(transformQuery);

                    tracingService.Trace("transformResults.Entities.Count=" + transformResults.Entities.Count);

                    if (transformResults.Entities.Count > 0)
                    {
                        flagActiveContactTrasnform = true;
                        tracingService.Trace("flagActiveContactTrasnform=" + flagActiveContactTrasnform);

                        Entity transform = transformResults[0];
                        string vNewContactId = string.Empty;
                        if (transform.Contains("stt_mergetocontactid") && transform["stt_mergetocontactid"] != null)
                        {
                            vNewContactId = transformResults[0].GetAttributeValue<string>("stt_mergetocontactid");
                        }
                        tracingService.Trace("vNewContactId=" + vNewContactId);

                        if (vNewContactId != string.Empty)
                        {
                            QueryExpression newContactQuery = new QueryExpression("contact");
                            newContactQuery.ColumnSet = new ColumnSet("contactid", "stt_contactfinanceidtext", "stt_enterpriseid", "stt_sourceid", "firstname", "lastname",
                    "statecode", "accountid", "mobilephone", "telephone1", "emailaddress1", "address1_line1", "address1_line2",
                    "address1_city", "stt_countyid", "stt_stateid", "stt_zipcodeid", "stt_fieldchangesreswareside");
                            newContactQuery.Criteria.AddCondition("contactid", ConditionOperator.Equal, Guid.Parse(vNewContactId));
                            EntityCollection newContactResults = service.RetrieveMultiple(newContactQuery);
                            tracingService.Trace("newContactResults.Entities.Count=" + newContactResults.Entities.Count);
                            if (newContactResults.Entities.Count > 0)
                            {
                                contactEntity = newContactResults.Entities[0];
                                stateCodeValue = contactEntity.GetAttributeValue<OptionSetValue>("statecode").Value;
                            }
                        }
                    }
                }
            }

            if (accountId != null && accountId != Guid.Empty)
            {
                contactEntity["parentcustomerid"] = new EntityReference("account", accountId);
            }
            tracingService.Trace("inside UpsertContact party.partyID = " + party.partyID);
            if (contactExists)
            {
                tracingService.Trace("contactExists = " + contactExists);

                string fieldChanges = contactEntity.Contains("stt_fieldchangesreswareside") && contactEntity["stt_fieldchangesreswareside"] != null ?
                    contactEntity.GetAttributeValue<string>("stt_fieldchangesreswareside") : string.Empty;
                tracingService.Trace("fieldChanges = " + fieldChanges);
                string oldfieldChanges = fieldChanges;
                fieldChanges = string.Empty;

                if (!String.IsNullOrEmpty(party.partyID) && contactEntity.GetAttributeValue<string>("stt_sourceid") == null && oldStateCodeValue != 1)
                {
                    contactEntity["stt_sourceid"] = party.partyID;
                }
                if (!String.IsNullOrEmpty(party.UUID) && contactEntity.GetAttributeValue<string>("stt_contactfinanceidtext") == null && oldStateCodeValue != 1)
                {
                    contactEntity["stt_contactfinanceidtext"] = party.UUID;
                }
                if ((String.IsNullOrEmpty(party.multiMatch) || party.multiMatch.ToLower() == "false") && contactEntity.GetAttributeValue<string>("stt_contactfinanceidtext") == null
                    && oldStateCodeValue != 1)
                {
                    contactEntity["stt_contactfinanceidtext"] = party.UUID;
                }
                if (!String.IsNullOrEmpty(party.firstName) && contactEntity.GetAttributeValue<string>("firstname") == null)
                {
                    contactEntity["firstname"] = party.firstName;
                }
                else if (!String.IsNullOrEmpty(party.firstName) && contactEntity.GetAttributeValue<string>("firstname") != null
                    && contactEntity.GetAttributeValue<string>("firstname") != party.firstName && !(oldfieldChanges.Contains(party.firstName)))
                {
                    fieldChanges += "firstname : " + party.firstName + Environment.NewLine;
                }

                if (!String.IsNullOrEmpty(party.lastName) && contactEntity.GetAttributeValue<string>("lastname") == null)
                {
                    contactEntity["lastname"] = party.lastName;
                }
                else if (!String.IsNullOrEmpty(party.lastName) && contactEntity.GetAttributeValue<string>("lastname") != null
                    && contactEntity.GetAttributeValue<string>("lastname") != party.lastName && !(oldfieldChanges.Contains(party.lastName)))
                {
                    fieldChanges += "lastName : " + party.lastName + Environment.NewLine;
                }

                tracingService.Trace("inside UpsertContact party.primaryEmail = " + party.primaryEmail);
                if (!String.IsNullOrEmpty(party.primaryEmail) && contactEntity.GetAttributeValue<string>("emailaddress1") == null)
                {
                    primaryEmailSet = true;
                    contactEntity["emailaddress1"] = party.primaryEmail;
                }
                else if (!String.IsNullOrEmpty(party.primaryEmail) && contactEntity.GetAttributeValue<string>("emailaddress1") != null
                    && contactEntity.GetAttributeValue<string>("emailaddress1") != party.primaryEmail && !(oldfieldChanges.Contains(party.primaryEmail)))
                {
                    fieldChanges += "emailaddress1 : " + party.primaryEmail + Environment.NewLine;
                }

                tracingService.Trace("inside UpsertContact party.contactPhoneNumber = " + party.contactPhoneNumber);
                if (!String.IsNullOrEmpty(party.contactPhoneNumber) && contactEntity.GetAttributeValue<string>("mobilephone") == null)
                {
                    primaryPhoneSet = true;
                    contactEntity["mobilephone"] = party.contactPhoneNumber;
                }
                else if (!String.IsNullOrEmpty(party.contactPhoneNumber) && contactEntity.GetAttributeValue<string>("mobilephone") != null
                    && contactEntity.GetAttributeValue<string>("mobilephone") != party.contactPhoneNumber && !(oldfieldChanges.Contains(party.contactPhoneNumber)))
                {
                    fieldChanges += "mobilephone : " + party.contactPhoneNumber + Environment.NewLine;
                }

                tracingService.Trace("inside UpsertContact party.businessPhoneNumber = " + party.businessPhoneNumber);
                if (!String.IsNullOrEmpty(party.businessPhoneNumber) && contactEntity.GetAttributeValue<string>("telephone1") == null)
                {
                    contactEntity["telephone1"] = party.businessPhoneNumber;
                }
                else if (!String.IsNullOrEmpty(party.businessPhoneNumber) && contactEntity.GetAttributeValue<string>("telephone1") != null
                    && contactEntity.GetAttributeValue<string>("telephone1") != party.businessPhoneNumber && !(oldfieldChanges.Contains(party.businessPhoneNumber)))
                {
                    fieldChanges += "telephone1 : " + party.contactPhoneNumber + Environment.NewLine;
                }

                tracingService.Trace("inside UpsertContact party.contactAddress1 = " + party.contactAddress1);
                if (!String.IsNullOrEmpty(party.contactAddress1) && contactEntity.GetAttributeValue<string>("address1_line1") == null)
                {
                    contactEntity["address1_line1"] = party.contactAddress1;
                }
                else if (!String.IsNullOrEmpty(party.contactAddress1) && contactEntity.GetAttributeValue<string>("address1_line1") != null
                    && contactEntity.GetAttributeValue<string>("address1_line1") != party.contactAddress1 && !(oldfieldChanges.Contains(party.contactAddress1)))
                {
                    fieldChanges += "address1_line1 : " + party.contactAddress1 + Environment.NewLine;
                }

                tracingService.Trace("inside UpsertContact party.contactAddress2 = " + party.contactAddress2);
                if (!String.IsNullOrEmpty(party.contactAddress2) && contactEntity.GetAttributeValue<string>("address1_line2") == null)
                {
                    contactEntity["address1_line2"] = party.contactAddress2;
                }
                else if (!String.IsNullOrEmpty(party.contactAddress2) && contactEntity.GetAttributeValue<string>("address1_line2") != null
                    && contactEntity.GetAttributeValue<string>("address1_line2") != party.contactAddress2 && !(oldfieldChanges.Contains(party.contactAddress2)))
                {
                    fieldChanges += "address1_line2 : " + party.contactAddress2 + Environment.NewLine;
                }

                tracingService.Trace("inside UpsertContact party.contactCity = " + party.contactCity);
                if (!String.IsNullOrEmpty(party.contactCity) && contactEntity.GetAttributeValue<string>("address1_city") == null)
                {
                    contactEntity["address1_city"] = party.contactCity;
                }
                else if (!String.IsNullOrEmpty(party.contactCity) && contactEntity.GetAttributeValue<string>("address1_city") != null
                    && contactEntity.GetAttributeValue<string>("address1_city") != party.contactCity && !(oldfieldChanges.Contains(party.contactCity)))
                {
                    fieldChanges += "address1_city : " + party.contactCity + Environment.NewLine;
                }

                EntityReference countyRef = GetLookupByAbbr(service, party.contactCounty + "County", "stt_county", "stt_countyid", "stt_name");
                if (countyRef != null && contactEntity.GetAttributeValue<EntityReference>("stt_countyid") == null)
                {
                    contactEntity["stt_countyid"] = countyRef;
                }
                else if (countyRef != null && !String.IsNullOrEmpty(party.contactCounty)
                    && contactEntity.GetAttributeValue<EntityReference>("stt_countyid") != null && contactEntity.GetAttributeValue<EntityReference>("stt_countyid").Id != countyRef.Id
                    && !(oldfieldChanges.Contains(party.contactCounty)))
                {
                    fieldChanges += "stt_countyid : " + party.contactCounty + Environment.NewLine;
                }

                EntityReference stateRef = GetLookupByAbbr(service, party.contactState, "stt_state", "stt_stateid", "stt_abbreviation");
                if (stateRef != null && contactEntity.GetAttributeValue<EntityReference>("stt_stateid") == null)
                {
                    contactEntity["stt_stateid"] = stateRef;
                }
                else if (stateRef != null && !String.IsNullOrEmpty(party.contactState)
                    && contactEntity.GetAttributeValue<EntityReference>("stt_stateid") != null && contactEntity.GetAttributeValue<EntityReference>("stt_stateid").Id != stateRef.Id
                    && !(oldfieldChanges.Contains(party.contactState)))
                {
                    fieldChanges += "stt_stateid : " + party.contactState + Environment.NewLine;
                }

                EntityReference zipRef = GetLookupByAbbr(service, party.contactZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
                if (zipRef != null && contactEntity.GetAttributeValue<EntityReference>("stt_zipcodeid") == null)
                {
                    contactEntity["stt_zipcodeid"] = zipRef;
                }
                else if (zipRef != null && !String.IsNullOrEmpty(party.contactZip)
                    && contactEntity.GetAttributeValue<EntityReference>("stt_zipcodeid") != null && contactEntity.GetAttributeValue<EntityReference>("stt_zipcodeid").Id != zipRef.Id
                    && !(oldfieldChanges.Contains(party.contactZip)))
                {
                    fieldChanges += "stt_zipcodeid : " + party.contactZip + Environment.NewLine;
                }

                if (!String.IsNullOrEmpty(fieldChanges) && oldfieldChanges != fieldChanges)
                {
                    tracingService.Trace("oldfieldChanges = " + oldfieldChanges);
                    fieldChanges = "Changed Date : " + DateTime.Now.Date.ToShortDateString() + Environment.NewLine + fieldChanges + Environment.NewLine + oldfieldChanges;
                    tracingService.Trace("fieldChanges = " + fieldChanges);
                    contactEntity["stt_fieldchangesreswareside"] = fieldChanges;
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(party.partyID))
                {
                    contactEntity["stt_sourceid"] = party.partyID;
                }
                if (!String.IsNullOrEmpty(party.UUID))
                {
                    contactEntity["stt_contactfinanceidtext"] = party.UUID;
                }
                if (String.IsNullOrEmpty(party.multiMatch) || party.multiMatch.ToLower() == "false")
                {
                    contactEntity["stt_contactfinanceidtext"] = party.UUID;
                }
                tracingService.Trace("inside UpsertContact party.firstName = " + party.firstName);

                contactEntity["firstname"] = party.firstName;

                tracingService.Trace("inside UpsertContact party.lastName = " + party.lastName);

                contactEntity["lastname"] = party.lastName;
                tracingService.Trace("inside UpsertContact party.primaryEmail = " + party.primaryEmail);

                if (!String.IsNullOrEmpty(party.primaryEmail))
                {
                    primaryEmailSet = true;
                    contactEntity["emailaddress1"] = party.primaryEmail;
                }
                tracingService.Trace("inside UpsertContact party.contactPhoneNumber = " + party.contactPhoneNumber);
                if (!String.IsNullOrEmpty(party.contactPhoneNumber))
                {
                    primaryPhoneSet = true;
                    contactEntity["mobilephone"] = party.contactPhoneNumber;
                }
                tracingService.Trace("inside UpsertContact party.businessPhoneNumber = " + party.businessPhoneNumber);
                if (!String.IsNullOrEmpty(party.businessPhoneNumber))
                {
                    contactEntity["telephone1"] = party.businessPhoneNumber;
                }

                tracingService.Trace("inside UpsertContact party.contactAddress1 = " + party.contactAddress1);
                if (!String.IsNullOrEmpty(party.contactAddress1))
                {
                    contactEntity["address1_line1"] = party.contactAddress1;
                }
                tracingService.Trace("inside UpsertContact party.contactAddress2 = " + party.contactAddress2);
                if (!String.IsNullOrEmpty(party.contactAddress2))
                {
                    contactEntity["address1_line2"] = party.contactAddress2;
                }
                tracingService.Trace("inside UpsertContact party.contactCity = " + party.contactCity);
                if (!String.IsNullOrEmpty(party.contactCity))
                {
                    contactEntity["address1_city"] = party.contactCity;
                }
                EntityReference countyRef = GetLookupByAbbr(service, party.contactCounty + " County", "stt_county", "stt_countyid", "stt_name");
                if (countyRef != null)
                {
                    contactEntity["stt_countyid"] = countyRef;
                }
                EntityReference stateRef = GetLookupByAbbr(service, party.contactState, "stt_state", "stt_stateid", "stt_abbreviation");
                if (stateRef != null)
                {
                    contactEntity["stt_stateid"] = stateRef;
                }
                EntityReference zipRef = GetLookupByAbbr(service, party.contactZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
                if (zipRef != null)
                {
                    contactEntity["stt_zipcodeid"] = zipRef;
                }
            }

            if (!String.IsNullOrEmpty(party.businessTitle))
            {
                contactEntity["jobtitle"] = party.businessTitle;
            }

            if (!String.IsNullOrEmpty(party.multiMatch) && isTPS)
            {
                contactEntity["stt_ismultimatch"] = party.multiMatch.ToLower() == "true";
            }
            if (party.matchedEnterpriseID != null && isTPS)
            {
                string meIDString = "";
                foreach (string meID in party.matchedEnterpriseID)
                {
                    if (meIDString == "")
                    {
                        meIDString = meID;
                    }
                    else
                    {
                        meIDString += "," + meID;
                    }
                }
                contactEntity["stt_matchedenterpriseids"] = meIDString;
            }

            try
            {
                // If contact doesn't exist in D365, it will be created
                if (!contactExists)
                {
                    tracingService.Trace("Contact doesn't exist. It will be created. MethodName UpsertContact");

                    contactEntity["stt_setenterpriseowner"] = true;
                    contactId = service.Create(contactEntity);
                    tracingService.Trace("Contact created.");
                }
                else
                {
                    if (contactExists)
                    {
                        tracingService.Trace("Contact already exists. It will be updated");
                        // Set contact to active in case it is not
                        if (stateCodeValue == 1)
                        {
                            contactEntity["statecode"] = new OptionSetValue(0);
                            contactEntity["statuscode"] = new OptionSetValue(1);
                        }
                        contactId = (Guid)contactEntity["contactid"];
                        service.Update(contactEntity);

                        tracingService.Trace("Contact updated.");
                    }
                }
            }
            catch (Exception exCreate)
            {
                tracingService.Trace($"Error creating/updating contact: {exCreate.Message}");
                if (exCreate.Message.Contains("Entity Key Contact Finance ID violated"))
                {
                    // Prepare the HTTP request
                    var url = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_ReprocessInboundApiURL");
                    tracingService.Trace($"Power Automate URL: {url}");
                    //var url = "https://0958d15cf44ee2eb8d03e7206f9a71.1d.environment.api.powerplatform.com:443/powerautomate/automations/direct/workflows/d9f689b9f46044c4a1d135590669219d/triggers/manual/paths/invoke/?api-version=1&tenantId=tId&environmentName=0958d15c-f44e-e2eb-8d03-e7206f9a711d&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=Vb6eR60CQzvR_4jTOtIDzUQmPlEb22aS2Xt2VGPCs-s";
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.ContentType = "application/json";

                    string serializedComplexJson = JsonConvert.SerializeObject(partiesStringJson);
                    tracingService.Trace($"Serialized complex JSON: {serializedComplexJson}");
                    // Step 2: Create the final payload object expected by Power Automate
                    var finalPayload = new
                    {
                        payload = partiesStringJson // This will be a string containing your original JSON
                    };

                    // Step 3: Serialize the final payload object to send as the HTTP request body
                    string jsonBody = JsonConvert.SerializeObject(finalPayload);
                    tracingService.Trace($"JSON body to send: {jsonBody}");
                    using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                    {
                        streamWriter.Write(jsonBody);
                        streamWriter.Flush();
                    }
                    // Get the response (optional, but good for error handling)
                    try
                    {
                        using (var response = (HttpWebResponse)request.GetResponse())
                        {
                            tracingService.Trace($"Power Automate webhook called successfully. Status: {response.StatusCode}");
                            context.OutputParameters["ResultCount"] = "Execution ended successfully.";
                            context.OutputParameters["response"] = "Execution ended successfully.";
                            throw new InvalidPluginExecutionException("Contact already exists. Power Automate webhook called to handle the conflict.");
                        }
                    }
                    catch (WebException webEx)
                    {
                        // Optionally log or handle HTTP errors
                        throw new InvalidPluginExecutionException("Failed to call Power Automate webhook: " + webEx.Message, webEx);
                    }
                }
                else
                {
                    throw new InvalidPluginExecutionException($"Error creating/updating contact: {exCreate.Message}", exCreate);
                }
            }

        }
        private void UpsertAddress(IOrganizationService service, ITracingService tracingService, string address1, string address2, string city, string county, string state, string zip, bool isPrimary)
        {
            // Retrieve Address
            tracingService.Trace("Searching address...");
            QueryExpression sequenceQuery = new QueryExpression("stt_address");
            EntityReference countyRef = GetLookupByAbbr(service, county + " County", "stt_county", "stt_countyid", "stt_name");
            EntityReference stateRef = GetLookupByAbbr(service, state, "stt_state", "stt_stateid", "stt_abbreviation");
            EntityReference zipCodeRef = GetLookupByAbbr(service, zip, "stt_zipcode", "stt_zipcodeid", "stt_name");
            sequenceQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
            if (countyRef != null)
            {
                sequenceQuery.Criteria.AddCondition("stt_countyid", ConditionOperator.Equal, countyRef.Id);
            }
            if (stateRef != null)
            {
                sequenceQuery.Criteria.AddCondition("stt_stateid", ConditionOperator.Equal, stateRef.Id);
            }
            if (zipCodeRef != null)
            {
                sequenceQuery.Criteria.AddCondition("stt_zipcodeid", ConditionOperator.Equal, zipCodeRef.Id);
            }
            sequenceQuery.Criteria.AddCondition("stt_address1", ConditionOperator.Equal, address1);
            if (!String.IsNullOrEmpty(address2))
            {
                sequenceQuery.Criteria.AddCondition("stt_address2", ConditionOperator.Equal, address2);
            }
            if (!String.IsNullOrEmpty(city))
            {
                sequenceQuery.Criteria.AddCondition("stt_city", ConditionOperator.Equal, city);
            }
            sequenceQuery.Criteria.AddCondition("stt_addresstypecode", ConditionOperator.Equal, 924510001);

            sequenceQuery.ColumnSet = new ColumnSet("stt_address", "statecode");

            EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);

            // If address doesn't exist in D365, it will be created
            if (sequenceEntities.Entities.Count == 0)
            {
                tracingService.Trace("Address doesn't exist. It will be created.");
                Entity addressEntity = new Entity("stt_address");

                //Assemble name
                List<string> addressNameParts = new List<string>();
                string addressName = "";
                if (!String.IsNullOrEmpty(address1)) { addressNameParts.Add(address1); }
                if (!String.IsNullOrEmpty(address2)) { addressNameParts.Add(address2); }
                if (!String.IsNullOrEmpty(city)) { addressNameParts.Add(city); }
                if (!String.IsNullOrEmpty(state)) { addressNameParts.Add(state); }
                if (!String.IsNullOrEmpty(county)) { addressNameParts.Add(county); }
                if (!String.IsNullOrEmpty(zip)) { addressNameParts.Add(zip); }
                int pos = 0;
                while (pos < addressNameParts.Count - 1)
                {
                    addressName += addressNameParts[pos] + ", ";
                    pos++;
                }
                addressName += addressNameParts[pos];

                //Set fields
                addressEntity["stt_address"] = addressName;
                addressEntity["stt_address1"] = address1;
                addressEntity["stt_address2"] = address2;
                addressEntity["stt_city"] = city;
                addressEntity["stt_countyid"] = countyRef;
                addressEntity["stt_stateid"] = stateRef;
                addressEntity["stt_zipcodeid"] = zipCodeRef;
                addressEntity["stt_contactid"] = new EntityReference("contact", contactId);
                addressEntity["stt_addresstypecode"] = new OptionSetValue(924510001);   // Address
                addressEntity["stt_isprimary"] = isPrimary;

                service.Create(addressEntity);

                tracingService.Trace("Address created.");
            }
            else
            {
                if (sequenceEntities[0].GetAttributeValue<OptionSetValue>("statecode").Value != 0)
                {
                    sequenceEntities[0]["statecode"] = new OptionSetValue(0);
                    service.Update(sequenceEntities[0]);
                    tracingService.Trace("Address reactivated.");
                }
                else
                {
                    tracingService.Trace("Address already exists.");
                }
            }
        }
        private void UpsertPhoneNumber(IOrganizationService service, ITracingService tracingService, string Phone, bool isPrimary)
        {
            // Retrieve Phone Number
            tracingService.Trace("Searching Phone Number...");
            QueryExpression sequenceQuery = new QueryExpression("stt_address");
            sequenceQuery.ColumnSet = new ColumnSet("stt_address", "statecode");
            sequenceQuery.Criteria.AddCondition("stt_telephone1", ConditionOperator.Equal, Phone);
            sequenceQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
            sequenceQuery.Criteria.AddCondition("stt_addresstypecode", ConditionOperator.Equal, 924510002);
            EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);

            // If Phone Number doesn't exist in D365, it will be created
            if (sequenceEntities.Entities.Count == 0)
            {
                tracingService.Trace("Phone Number doesn't exist. It will be created.");
                Entity phoneNumberEntity = new Entity("stt_address");
                phoneNumberEntity["stt_address"] = $"Phone - {Phone}";
                phoneNumberEntity["stt_telephone1"] = Phone;
                phoneNumberEntity["stt_contactid"] = new EntityReference("contact", contactId);
                phoneNumberEntity["stt_addresstypecode"] = new OptionSetValue(924510002);   // Phone
                phoneNumberEntity["stt_isprimary"] = isPrimary;


                service.Create(phoneNumberEntity);

                tracingService.Trace("Phone Number created.");
            }
            else
            {
                if (sequenceEntities[0].GetAttributeValue<OptionSetValue>("statecode").Value != 0)
                {
                    sequenceEntities[0]["statecode"] = new OptionSetValue(0);
                    service.Update(sequenceEntities[0]);
                    tracingService.Trace("Phone Number reactivated.");
                }
                else
                {
                    tracingService.Trace("Phone Number already exists.");
                }
            }
        }
        private void UpsertEmail(IOrganizationService service, ITracingService tracingService, InboundTPSParty party, string email, Boolean isPrimary)
        {
            // Retrieve Email
            tracingService.Trace("Searching Email...");
            QueryExpression sequenceQuery = new QueryExpression("stt_address");
            sequenceQuery.ColumnSet = new ColumnSet("stt_address", "statecode");
            sequenceQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
            sequenceQuery.Criteria.AddCondition("stt_addresstypecode", ConditionOperator.Equal, 924510000);
            sequenceQuery.Criteria.AddCondition("stt_emailaddress", ConditionOperator.Equal, email);
            EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);

            // If Email doesn't exist in D365, it will be created
            if (sequenceEntities.Entities.Count == 0)
            {
                tracingService.Trace("Email doesn't exist. It will be created.");
                Entity emailEntity = new Entity("stt_address");
                emailEntity["stt_address"] = $"Email - {email}";
                emailEntity["stt_emailaddress"] = email;
                emailEntity["stt_contactid"] = new EntityReference("contact", contactId);
                emailEntity["stt_addresstypecode"] = new OptionSetValue(924510000);   // Email
                emailEntity["stt_isprimary"] = isPrimary;

                service.Create(emailEntity);

                tracingService.Trace("Email created.");
            }
            else
            {
                if (sequenceEntities[0].GetAttributeValue<OptionSetValue>("statecode").Value != 0)
                {
                    sequenceEntities[0]["statecode"] = new OptionSetValue(0);
                    service.Update(sequenceEntities[0]);
                    tracingService.Trace("Email reactivated.");
                }
                else
                {
                    tracingService.Trace("Email already exists.");
                }
            }
        }
        private void UpsertAddresses(IOrganizationService service, ITracingService tracingService, InboundTPSParty party)
        {
            // Check if primary address was set on Contact
            if (primaryAddressSet && !String.IsNullOrEmpty(party.contactAddress1))
            {
                UpsertAddress(service, tracingService, party.contactAddress1, party.contactAddress2, party.contactCity, party.contactCounty, party.contactState, party.contactZip, true);
            }
            // Adds secondary address with data from primary address if primary address was already added to the contact in another request
            if (!primaryAddressSet && !String.IsNullOrEmpty(party.contactAddress1))
            {
                UpsertAddress(service, tracingService, party.contactAddress1, party.contactAddress2, party.contactCity, party.contactCounty, party.contactState, party.contactZip, false);
            }
            // Check if secondary address exists
            if (!String.IsNullOrEmpty(party.businessAddress1))
            {
                UpsertAddress(service, tracingService, party.businessAddress1, party.businessAddress2, party.businessCity, party.businessCounty, party.businessState, party.businessZip, false);
            }
            // Check if primary phone was set on Contact
            if (!String.IsNullOrEmpty(party.contactPhoneNumber) && primaryPhoneSet)
            {
                UpsertPhoneNumber(service, tracingService, party.contactPhoneNumber, true);
            }
            // Adds secondary phone with data from primary phone if primary phone was already added to the contact in another request
            if (!String.IsNullOrEmpty(party.contactPhoneNumber) && !primaryPhoneSet)
            {
                UpsertPhoneNumber(service, tracingService, party.contactPhoneNumber, false);
            }
            // Check if secondary phone exists
            if (!String.IsNullOrEmpty(party.businessPhoneNumber))
            {
                UpsertPhoneNumber(service, tracingService, party.businessPhoneNumber, false);
            }
            // Check if primary email was set on Contact
            if (!String.IsNullOrEmpty(party.primaryEmail) && primaryEmailSet)
            {
                UpsertEmail(service, tracingService, party, party.primaryEmail, true);
            }
            // Adds secondary email with data from primary email if primary email was already added to the contact in another request
            if (!String.IsNullOrEmpty(party.primaryEmail) && !primaryEmailSet)
            {
                UpsertEmail(service, tracingService, party, party.primaryEmail, false);
            }
            // Check if secondary email exists
            if (!String.IsNullOrEmpty(party.relatedEmail))
            {
                UpsertEmail(service, tracingService, party, party.relatedEmail, false);
            }
        }
        private void setMSA(IOrganizationService service, ITracingService tracingService, InboundTPSParty party, InboundTPSTransaction transaction, InboundMSA msa)
        {
            // Retrieve MSA
            tracingService.Trace("Searching MSA...");
            QueryExpression msaQuery = new QueryExpression("stt_metropolitanstatisticalarea");
            msaQuery.ColumnSet = new ColumnSet("stt_metropolitanstatisticalareaid", "stt_name", "statecode");
            msaQuery.Criteria.AddCondition("stt_name", ConditionOperator.Equal, msa.msa);
            EntityCollection msaEntities = service.RetrieveMultiple(msaQuery);
            if (msaEntities.Entities.Count > 0 && msaEntities[0].GetAttributeValue<OptionSetValue>("statecode").Value == 0)
            {
                // Retrieve Transaction MSA
                tracingService.Trace("Searching Transaction MSA...");
                QueryExpression tMSAQuery = new QueryExpression("stt_transactionmsa");
                tMSAQuery.ColumnSet = new ColumnSet("stt_transactionmsaid", "stt_msaid", "stt_transactionid", "stt_name", "statecode");
                tMSAQuery.Criteria.AddCondition("stt_msaid", ConditionOperator.Equal, msaEntities[0].GetAttributeValue<Guid>("stt_metropolitanstatisticalareaid"));
                tMSAQuery.Criteria.AddCondition("stt_transactionid", ConditionOperator.Equal, transactionId);
                EntityCollection tMSAEntities = service.RetrieveMultiple(tMSAQuery);
                if (tMSAEntities.Entities.Count > 0 && tMSAEntities[0].GetAttributeValue<OptionSetValue>("statecode").Value == 0)
                {
                    tracingService.Trace($"Transaction MSA {tMSAEntities[0].GetAttributeValue<string>("stt_name")} already exists.");
                }
                else
                {
                    Entity tMSAEntity = new Entity("stt_transactionmsa");
                    tMSAEntity["stt_msaid"] = new EntityReference("stt_metropolitanstatisticalarea", msaEntities[0].GetAttributeValue<Guid>("stt_metropolitanstatisticalareaid"));
                    tMSAEntity["stt_transactionid"] = new EntityReference("stt_transaction", transactionId);
                    tMSAEntity["stt_name"] = $"{msa.msa} - {transaction.fileNumber}";
                    Guid tMSAid = service.Create(tMSAEntity);
                    tracingService.Trace($"Transaction MSA {tMSAid} created");
                }
            }
            else
            {
                tracingService.Trace($"MSA {msa.msa} not found or inactive.");
            }
        }
        private void UpsertTransactionRole(IOrganizationService service, ITracingService tracingService, InboundTPSParty party, InboundTPSTransaction transaction)
        {
            bool transactionRoleExists = false;
            bool existingIsdirectingcient = false;
            bool currentIsdirectingcient = false;
            int existingRole = 0;
            int currentRole = 0;
            Entity transactionRoleEntity = null;
            QueryExpression sequenceQuery;
            EntityCollection sequenceEntities;
            int existingTransactionRoleStateCode = 0;
            Guid transactionRoleEntityId = Guid.Empty;

            tracingService.Trace($"Setting role: {transaction.roleinFile}");

            tracingService.Trace($"transactionId: {transactionId}");
            OptionSetValue roleInFile = GetRoleOptionByName(transaction.roleinFile);

            if (roleInFile != null)
            {
                currentRole = roleInFile.Value;
            }
            tracingService.Trace($"currentRole: {currentRole}");
            // Retrieve Transaction Role
            tracingService.Trace("Searching transaction role...");
            sequenceQuery = new QueryExpression("stt_transactionrole");
            sequenceQuery.ColumnSet = new ColumnSet("stt_transactionroleid", "stt_rolecode", "stt_isdirectingcient", "statecode");
            sequenceQuery.Criteria.AddCondition("stt_transactionid", ConditionOperator.Equal, transactionId);
            if (roleInFile != null)
            {
                sequenceQuery.Criteria.AddCondition("stt_rolecode", ConditionOperator.Equal, roleInFile.Value);
            }
            if (contactId != null && contactId != Guid.Empty)
            {
                sequenceQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
            }
            sequenceEntities = service.RetrieveMultiple(sequenceQuery);

            if (sequenceEntities.Entities.Count > 0)
            {
                tracingService.Trace("Transaction role already exists.");
                transactionRoleEntity = sequenceEntities.Entities[0];

                transactionRoleEntityId = transactionRoleEntity.Id;

                existingRole = transactionRoleEntity.Contains("stt_rolecode") && transactionRoleEntity["stt_rolecode"] != null ?
                      transactionRoleEntity.GetAttributeValue<OptionSetValue>("stt_rolecode").Value : 0;

                existingTransactionRoleStateCode = transactionRoleEntity.GetAttributeValue<OptionSetValue>("statecode").Value;
                tracingService.Trace("existingTransactionRoleStateCode =" + existingTransactionRoleStateCode);

                transactionRoleExists = true;
                existingIsdirectingcient = transactionRoleEntity.Contains("stt_isdirectingcient") && transactionRoleEntity["stt_isdirectingcient"] != null ?
                    transactionRoleEntity.GetAttributeValue<bool>("stt_isdirectingcient") : false;

                tracingService.Trace("existingIsdirectingcient =" + existingIsdirectingcient);
                transactionRoleEntity = new Entity("stt_transactionrole");
            }
            else
            {
                tracingService.Trace("Transaction role doesn't exist. It will be created.");
                transactionRoleEntity = new Entity("stt_transactionrole");
                existingRole = 1;
                tracingService.Trace("Transaction role doesn't exist. It will be created.");
            }

            //if existing role is empty and current role is not empty then search transaction role again by transaction id and contact id
            if (existingRole == 1 && currentRole != 0)
            {
                tracingService.Trace("Searching transaction role1 ...");
                sequenceQuery = new QueryExpression("stt_transactionrole");
                sequenceQuery.ColumnSet = new ColumnSet("stt_transactionroleid", "stt_rolecode", "stt_isdirectingcient", "statecode");
                sequenceQuery.Criteria.AddCondition("stt_transactionid", ConditionOperator.Equal, transactionId);

                if (contactId != null && contactId != Guid.Empty)
                {
                    sequenceQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
                }
                sequenceEntities = service.RetrieveMultiple(sequenceQuery);

                if (sequenceEntities.Entities.Count > 0)
                {
                    tracingService.Trace("Transaction role already exists1.");
                    transactionRoleEntity = sequenceEntities.Entities[0];
                    transactionRoleEntityId = transactionRoleEntity.Id;

                    existingRole = transactionRoleEntity.Contains("stt_rolecode") && transactionRoleEntity["stt_rolecode"] != null ?
                          transactionRoleEntity.GetAttributeValue<OptionSetValue>("stt_rolecode").Value : 0;
                    tracingService.Trace("existingRole =" + existingRole);

                    existingTransactionRoleStateCode = transactionRoleEntity.GetAttributeValue<OptionSetValue>("statecode").Value;
                    tracingService.Trace("existingTransactionRoleStateCode =" + existingTransactionRoleStateCode);

                    existingIsdirectingcient = transactionRoleEntity.Contains("stt_isdirectingcient") && transactionRoleEntity["stt_isdirectingcient"] != null ?
                        transactionRoleEntity.GetAttributeValue<bool>("stt_isdirectingcient") : false;

                    tracingService.Trace("existingIsdirectingcient =" + existingIsdirectingcient);
                    if ((existingRole == currentRole) || existingRole == 0)
                    {
                        transactionRoleEntity = new Entity("stt_transactionrole");
                        transactionRoleExists = true;
                    }
                    else
                    {
                        transactionRoleExists = false;
                        tracingService.Trace("Transaction role doesn't exist. It will be created_1.");
                        transactionRoleEntity = new Entity("stt_transactionrole");
                    }
                }
                else
                {
                    tracingService.Trace("Transaction role doesn't exist. It will be created_2.");
                    transactionRoleEntity = new Entity("stt_transactionrole");
                }

            }


            // Set fields
            tracingService.Trace("Before transaction set lookup.");
            transactionRoleEntity["stt_transactionid"] = new EntityReference("stt_transaction", transactionId);
            tracingService.Trace("after transaction set lookup.");
            if (contactId != null && contactId != Guid.Empty)
            {
                transactionRoleEntity["stt_contactid"] = new EntityReference("contact", contactId);
            }

            if (roleInFile != null)
            {
                transactionRoleEntity["stt_rolecode"] = roleInFile;
            }
            if (accountId != null && accountId != Guid.Empty)
            {
                transactionRoleEntity["stt_accountid"] = new EntityReference("account", accountId);
            }
            if (owner != null)
            {
                transactionRoleEntity["ownerid"] = owner;
            }
            if (!String.IsNullOrEmpty(transaction.directingPartyFlag))
            {
                currentIsdirectingcient = bool.Parse(transaction.directingPartyFlag);
                tracingService.Trace($"currentIsdirectingcient: {currentIsdirectingcient}");
            }

            if (!transactionRoleExists)
            {
                if (!String.IsNullOrEmpty(transaction.directingPartyFlag))
                {
                    transactionRoleEntity["stt_isdirectingcient"] = transaction.directingPartyFlag == "true";
                }
                transactionRoleEntity["stt_name"] = $"{party.firstName} {party.lastName} - {transaction.roleinFile} - {fileNumber}";
                Guid transactionRoleId = service.Create(transactionRoleEntity);
                tracingService.Trace($"Transaction role created with ID {transactionRoleId}.");
            }
            else
            {
                transactionRoleEntity.Id = transactionRoleEntityId;
                if (existingIsdirectingcient == false && currentIsdirectingcient == true)
                {
                    tracingService.Trace($"inside :existingIsdirectingcient == {existingIsdirectingcient} && currentIsdirectingcient == {currentIsdirectingcient} ");
                    transactionRoleEntity["stt_isdirectingcient"] = currentIsdirectingcient;
                }

                if (existingTransactionRoleStateCode != 0)
                {
                    transactionRoleEntity["statecode"] = new OptionSetValue(0);
                    service.Update(transactionRoleEntity);
                    tracingService.Trace("Transaction role reactivated.");
                }
                else
                {
                    tracingService.Trace("Before Transaction role updated.");
                    service.Update(transactionRoleEntity);
                    tracingService.Trace("Transaction role updated.");
                }
            }

        }
        private OptionSetValue GetRoleOptionByName(string name)
        {
            if (String.IsNullOrEmpty(name))
                return null;
            else
            {
                switch (name.ToLower())
                {
                    case "listing agent":
                        return new OptionSetValue(924510000);
                    case "selling agent":
                        return new OptionSetValue(924510001);
                    case "loan officer":
                        return new OptionSetValue(924510002);
                    case "buyer":
                        return new OptionSetValue(924510003);
                    case "seller":
                        return new OptionSetValue(924510004);
                    case "escrow officer":
                        return new OptionSetValue(924510005);
                    case "title officer":
                        return new OptionSetValue(924510006);
                    case "bdo":
                        return new OptionSetValue(924510007);
                    case "buyer's attorney":
                        return new OptionSetValue(924510008);
                    case "seller's attorney":
                        return new OptionSetValue(924510009);
                    case "lender's attorney":
                        return new OptionSetValue(924510010);
                    case "lender":
                        return new OptionSetValue(924510011);
                    case "mortgage broker":
                        return new OptionSetValue(924510012);
                    case "escrow company":
                        return new OptionSetValue(924510013);
                    case "title company":
                        return new OptionSetValue(924510014);
                    case "builder":
                        return new OptionSetValue(924510015);
                    case "affiliated business":
                        return new OptionSetValue(924510016);
                    case "relocation company":
                        return new OptionSetValue(924510017);
                    case "closing attorney":
                        return new OptionSetValue(924510018);
                    case "developer":
                        return new OptionSetValue(924510019);
                    case "third-party attorney":
                        return new OptionSetValue(924510020);
                    case "borrower representative":
                        return new OptionSetValue(924510021);
                    default:
                        return null;
                }
            }
        }

        private DateTime dateFormater(ITracingService tracingService, string dateString)
        {
            DateTime myDate;
            if (!DateTime.TryParse(dateString, out myDate))
            {
                tracingService.Trace("Incorrect date format.");
                throw new InvalidPluginExecutionException("Incorrect date format.");
            }
            return myDate;
        }
        private void ProcessTransaction(IOrganizationService service, ITracingService tracingService, InboundTPSParty party, InboundTPSTransaction transaction)
        {
            // If transaction doesn't exist in D365, it will be created
            Entity createUpdateTransaction = new Entity("stt_transaction");
            bool transactionExists = false;
            if (currentTransaction.Id.Equals(Guid.Empty))
            {
                tracingService.Trace("Transaction doesn't exist. It will be created.");
                // currentTransaction = new Entity("stt_transaction");
            }
            else
            {
                tracingService.Trace("Transaction already exists.");
                transactionExists = true;
                createUpdateTransaction.Id = currentTransaction.Id;
            }

            if (currentTransaction.GetAttributeValue<EntityReference>("stt_directingclientid") == null && contactId != null && contactId != Guid.Empty && !String.IsNullOrEmpty(transaction.directingPartyFlag) && transaction.directingPartyFlag.ToLower() == "true")
            {
                createUpdateTransaction["stt_directingclientid"] = new EntityReference("contact", contactId);
            }
            if (!String.IsNullOrEmpty(transaction.propertyAddress1))
            {
                createUpdateTransaction["stt_address1"] = transaction.propertyAddress1;
            }
            if (!String.IsNullOrEmpty(transaction.propertyAddress2))
            {
                createUpdateTransaction["stt_address2"] = transaction.propertyAddress2;
            }
            if (!String.IsNullOrEmpty(transaction.propertyCity))
            {
                createUpdateTransaction["stt_city"] = transaction.propertyCity;
            }
            if (!String.IsNullOrEmpty(transaction.transactionAmount))
            {
                createUpdateTransaction["stt_transactionamount"] = Convert.ToDecimal(transaction.transactionAmount);
            }
            if (!String.IsNullOrEmpty(transaction.propertyCounty))
            {
                tracingService.Trace($"Setting property county: {transaction.propertyCounty}");
                createUpdateTransaction["stt_countyid"] = GetLookupByAbbr(service, transaction.propertyCounty + " County", "stt_county", "stt_countyid", "stt_name");
            }
            if (!String.IsNullOrEmpty(transaction.propertyState))
            {
                createUpdateTransaction["stt_stateid"] = GetLookupByAbbr(service, transaction.propertyState, "stt_state", "stt_stateid", "stt_abbreviation");
            }
            if (!String.IsNullOrEmpty(transaction.propertyZip))
            {
                createUpdateTransaction["stt_zipcodeid"] = GetLookupByAbbr(service, transaction.propertyZip, "stt_zipcode", "stt_zipcodeid", "stt_name");
            }
            if (!String.IsNullOrEmpty(transaction.division))
            {
                createUpdateTransaction["stt_divisionid"] = GetLookupByAbbr(service, transaction.division, "stt_market", "stt_marketid", "stt_name");
            }
            if (!String.IsNullOrEmpty(transaction.transactionType))
            {
                switch (transaction.transactionType.ToLower())
                {
                    case "new sales": createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(924510000); break;
                    case "resale": createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(924510001); break;
                    case "refi": createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(924510002); break;
                    default: createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(198730001); break;
                }
            }
            else
            {
                tracingService.Trace("transaction.transactionType is null, set transactionType to TBD");
                createUpdateTransaction["stt_transactiontypecode"] = new OptionSetValue(198730001);//198730001 : TBD
            }

            if (currentOfficeLocationBranch != null)
            {

                createUpdateTransaction["stt_officelocationbranchid"] = new EntityReference("stt_officelocationbranch", currentOfficeLocationBranch.Id);
                if (currentOfficeLocationBranch.GetAttributeValue<EntityReference>("stt_brandid") != null)
                {
                    createUpdateTransaction["stt_brandid"] = currentOfficeLocationBranch.GetAttributeValue<EntityReference>("stt_brandid");
                }
            }

            if (!String.IsNullOrEmpty(transaction.BDOBranchInfo))
            {
                createUpdateTransaction["stt_titlecompany"] = transaction.BDOBranchInfo;
            }

            if (!String.IsNullOrEmpty(transaction.fileStartDate))
            {
                createUpdateTransaction["stt_filestarton"] = dateFormater(tracingService, transaction.fileStartDate);
            }
            if (!String.IsNullOrEmpty(transaction.estClosingDate))
            {
                createUpdateTransaction["stt_estimatedclosedon"] = dateFormater(tracingService, transaction.estClosingDate);
            }
            if (!String.IsNullOrEmpty(transaction.finalClosingDate))
            {
                createUpdateTransaction["stt_finalcloseon"] = dateFormater(tracingService, transaction.finalClosingDate);
            }

            #region 399638 - Set BDO
            //Set Bdo for 
            if (createUpdateTransaction != null && isHousePayload && houseAccountUser != null)
            {
                createUpdateTransaction["stt_bdoid"] = houseAccountUser;
                isHouseBdoAssigned = true;
                tracingService.Trace($"House BDO assigned. SystemUserId: {houseAccountUser.Id}");
            }
            #endregion
            else if (!isHousePayload && !String.IsNullOrEmpty(transaction.roleinFile) && transaction.roleinFile.ToLower() == "bdo" && bdoUser != null)
            {
                tracingService.Trace($"System User ID: {bdoUser.GetAttributeValue<Guid>("systemuserid")}");
                createUpdateTransaction["stt_bdoid"] = new EntityReference("systemuser", bdoUser.GetAttributeValue<Guid>("systemuserid"));
            }

            // Search for Client Detail
            tracingService.Trace("Searching Client detail");
            QueryExpression sequenceCDQuery = new QueryExpression("stt_clientdetails");
            sequenceCDQuery.ColumnSet = new ColumnSet("stt_clientdetailsid");
            if (marketEntity != null && isTPS)
            {
                tracingService.Trace("Market ref found");
                createUpdateTransaction["stt_marketid"] = new EntityReference("stt_market", marketEntity.Id);
                sequenceCDQuery.Criteria.AddCondition("stt_marketid", ConditionOperator.Equal, marketEntity.Id);
                sequenceCDQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
                EntityCollection sequenceClientDetails = service.RetrieveMultiple(sequenceCDQuery);
                var isDirectingClient = (!String.IsNullOrEmpty(transaction.directingPartyFlag) && transaction.directingPartyFlag.ToLower() == "true");
                if (isDirectingClient && sequenceClientDetails.Entities.Count > 0)
                {
                    tracingService.Trace("Client detail found");
                    createUpdateTransaction["stt_directingclientdetailsid"] = new EntityReference("stt_clientdetails", sequenceClientDetails[0].Id);
                    tracingService.Trace("Client detail inserted");
                }
                else
                {
                    tracingService.Trace("Client detail not found");
                }
            }
            else
            {
                tracingService.Trace("Market ref not found");
            }
            if (!String.IsNullOrEmpty(transaction.titleFee))
            {
                createUpdateTransaction["stt_titlefee"] = new Money(Convert.ToDecimal(transaction.titleFee));
            }
            if (!String.IsNullOrEmpty(transaction.escrowFee))
            {
                createUpdateTransaction["stt_escrowfee"] = new Money(Convert.ToDecimal(transaction.escrowFee));
            }
            if (!String.IsNullOrEmpty(transaction.endorsement))
            {
                createUpdateTransaction["stt_endorsementfee"] = new Money(Convert.ToDecimal(transaction.endorsement));
            }
            if (!String.IsNullOrEmpty(transaction.abstractFee))
            {
                createUpdateTransaction["stt_abstractfee"] = new Money(Convert.ToDecimal(transaction.abstractFee));
            }
            if (!String.IsNullOrEmpty(party.partySystem))
            {
                createUpdateTransaction["stt_source"] = party.partySystem;
            }
            if (!String.IsNullOrEmpty(transaction.salesPrice))
            {
                createUpdateTransaction["stt_salesprice"] = new Money(Convert.ToDecimal(transaction.salesPrice));
            }
            if (!String.IsNullOrEmpty(transaction.loanAmount))
            {
                createUpdateTransaction["stt_loanamount"] = new Money(Convert.ToDecimal(transaction.loanAmount));
            }

            createUpdateTransaction["stt_istransaction"] = isTPS;

            createUpdateTransaction["stt_filenumber"] = fileNumber;
            if (!String.IsNullOrEmpty(transaction.fileStatus))
            {
                switch (transaction.fileStatus.ToLower())
                {
                    case "open": createUpdateTransaction["stt_transactionstatuscode"] = new OptionSetValue(924510000); break;
                    case "closed": createUpdateTransaction["stt_transactionstatuscode"] = new OptionSetValue(924510001); break;
                    case "cancelled": createUpdateTransaction["stt_transactionstatuscode"] = new OptionSetValue(924510002); break;
                    default: break;
                }
            }
            #region Added code as part of task #386479

            if (createUpdateTransaction.GetAttributeValue<OptionSetValue>("stt_transactionstatuscode")?.Value == 924510000)
            {
                tracingService.Trace("Transaction status is Open.Clearing Final Close Date and zzzStatus fields.");

                createUpdateTransaction["stt_finalcloseon"] = null;
                createUpdateTransaction["stt_zzzstatus"] = null;
            }
            #endregion

            if (owner != null)
            {
                createUpdateTransaction["ownerid"] = owner;
            }

            if (!transactionExists)
            {
                try
                {
                    createUpdateTransaction["stt_transactionfinanceidtext"] = transaction.transactionID;

                    QueryExpression sequenceQuery = new QueryExpression("stt_transaction");
                    sequenceQuery.ColumnSet = new ColumnSet("stt_transactionid", "statecode", "stt_filenumber", "stt_transactionfinanceidtext", "stt_officelocationbranchid", "stt_marketid", "ownerid");
                    sequenceQuery.Criteria.AddCondition("stt_transactionfinanceidtext", ConditionOperator.Equal, transaction.transactionID);
                    EntityCollection sequenceEntities = service.RetrieveMultiple(sequenceQuery);

                    if (sequenceEntities.Entities.Count > 0)
                    {
                        // Transaction found
                        tracingService.Trace("Transaction found.");
                        currentTransaction = sequenceEntities[0];
                        transactionId = currentTransaction.Id;
                        createUpdateTransaction.Id = transactionId;
                    }
                    else
                    {
                        tracingService.Trace("Transaction not found. It will be created.");
                        UpsertRequest upsertRequest = new UpsertRequest
                        {
                            Target = createUpdateTransaction
                        };
                        UpsertResponse upsertResponse = (UpsertResponse)service.Execute(upsertRequest);

                        if (upsertResponse.RecordCreated)
                        {
                            tracingService.Trace($"New transaction created with ID: {upsertResponse.Target.Id}");
                        }
                        else
                        {
                            tracingService.Trace($"Existing transaction updated with ID: {upsertResponse.Target.Id}");
                        }
                        transactionId = upsertResponse.Target.Id;
                        currentTransaction = service.Retrieve("stt_transaction", transactionId, new ColumnSet("stt_transactionid", "statecode", "stt_filenumber", "stt_transactionfinanceidtext", "stt_officelocationbranchid", "stt_marketid", "ownerid"));
                        tracingService.Trace($"Transaction created with transactionID {transactionId}");
                        if (!isTPS)
                        {
                            foreach (InboundMSA msa in transaction.msa)
                            {
                                setMSA(service, tracingService, party, transaction, msa);
                            }
                        }
                    }

                }
                catch (Exception exCreate)
                {
                    tracingService.Trace($"Error creating/updating transaction: {exCreate.Message}");
                    if (exCreate.Message.Contains("Entity Key Transaction Finance ID violated"))
                    {
                        // Prepare the HTTP request
                        var url = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_ReprocessInboundApiURL");
                        tracingService.Trace($"Power Automate URL: {url}");
                        //var url = "https://0958d15cf44ee2eb8d03e7206f9a71.1d.environment.api.powerplatform.com:443/powerautomate/automations/direct/workflows/d9f689b9f46044c4a1d135590669219d/triggers/manual/paths/invoke/?api-version=1&tenantId=tId&environmentName=0958d15c-f44e-e2eb-8d03-e7206f9a711d&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=Vb6eR60CQzvR_4jTOtIDzUQmPlEb22aS2Xt2VGPCs-s";
                        var request = (HttpWebRequest)WebRequest.Create(url);
                        request.Method = "POST";
                        request.ContentType = "application/json";

                        string serializedComplexJson = JsonConvert.SerializeObject(partiesStringJson);
                        tracingService.Trace($"Serialized complex JSON: {serializedComplexJson}");
                        // Step 2: Create the final payload object expected by Power Automate
                        var finalPayload = new
                        {
                            payload = partiesStringJson // This will be a string containing your original JSON
                        };

                        // Step 3: Serialize the final payload object to send as the HTTP request body
                        string jsonBody = JsonConvert.SerializeObject(finalPayload);
                        tracingService.Trace($"JSON body to send: {jsonBody}");
                        using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                        {
                            streamWriter.Write(jsonBody);
                            streamWriter.Flush();
                        }
                        // Get the response (optional, but good for error handling)
                        try
                        {
                            using (var response = (HttpWebResponse)request.GetResponse())
                            {
                                tracingService.Trace($"Power Automate webhook called successfully. Status: {response.StatusCode}");
                                context.OutputParameters["ResultCount"] = "Execution ended successfully.";
                                context.OutputParameters["response"] = "Execution ended successfully.";
                                throw new InvalidPluginExecutionException("Transaction already exists. Power Automate webhook called to handle the conflict.");
                            }
                        }
                        catch (WebException webEx)
                        {
                            // Optionally log or handle HTTP errors
                            throw new InvalidPluginExecutionException("Failed to call Power Automate webhook: " + webEx.Message, webEx);
                        }
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException($"Error creating/updating transaction: {exCreate.Message}", exCreate);
                    }
                }
            }
            else
            {
                // transactionId = currentTransaction.GetAttributeValue<Guid>("stt_transactionid");
                transactionId = createUpdateTransaction.Id;
                if (currentTransaction.GetAttributeValue<OptionSetValue>("statecode") != new OptionSetValue(0))
                {
                    createUpdateTransaction["statecode"] = new OptionSetValue(0);
                }

                tracingService.Trace("To update.");
                tracingService.Trace("transactionId :" + transactionId);
                service.Update(createUpdateTransaction);
                tracingService.Trace("Updated.");
            }
           if (!String.IsNullOrEmpty(transaction.roleinFile) && !isBDOuser && (contactId != null && contactId != Guid.Empty))            {
                //Upsert transaction role
                UpsertTransactionRole(service, tracingService, party, transaction);
            }
            else
            {
                tracingService.Trace("Transaction processed.");
            }
        }
        private void ProcessTransactionsArray(IOrganizationService service, ITracingService tracingService, InboundTPSParty party)
        {
            if (party.Transactions.Length > 0)
            {
                int count = 0;
                while (party.Transactions.Length > count)
                {
                    ProcessTransaction(service, tracingService, party, party.Transactions[count]);
                    count++;
                    tracingService.Trace("party.Transactions.Length=" + party.Transactions.Length);
                    tracingService.Trace("count=" + count);
                    if (party.Transactions.Length > count)
                    {
                        SetTransactionVariables(service, tracingService, party, party.Transactions[count]);
                    }
                }
            }
            else
            {
                tracingService.Trace("No transactions to process.");
            }
        }
        private void CheckRoles(ITracingService tracingService, InboundTPSParty party)
        {
            int count = 0;
            string role;
            while (count < party.Transactions.Length && (!isBDO || !isContact))
            {
                role = party.Transactions[count].roleinFile;
                if (!String.IsNullOrEmpty(role))
                {
                    role = role.ToLower();
                    if (role == "bdo")
                    {
                        isBDO = true;
                        tracingService.Trace("BDO role found.");
                    }
                    else if (role != "buyer" && role != "seller")
                    {
                        isContact = true;
                        tracingService.Trace("Contact role found.");
                    }
                }
                count++;
            }
        }
        private void ResetPerPartyflag()
        {

            isHousePayload = false;
            isHouseBdoAssigned = false;
            houseAccountUser = null;

            isBDO = false;
            isContact = false;
            bdoUser = null;
            contactId = Guid.Empty;
            accountId = Guid.Empty;

            primaryAddressSet = false;
            primaryEmailSet = false;
            primaryPhoneSet = false;
        }
        public void ProcessParty(IOrganizationService service, ITracingService tracingService, InboundTPSParty party, bool isTransaction, string partiesString, IPluginExecutionContext contextExecution)
        {
            ResetPerPartyflag();
            try
            {
                bool ignoreParty = false;
                isTPS = isTransaction;
                partiesStringJson = partiesString;
                context = contextExecution;
                string houseAccountName = null;
                bool hasBdoRole = false;
                string bdoBranchInfo = null;


                if (party.Transactions != null && party.Transactions.Length > 0)
                {

                    #region 399638 -  Address House Account BDO processing

                    foreach (var transation in party.Transactions)
                    {
                        //A
                        if (!string.IsNullOrEmpty(transation.roleinFile) && transation.roleinFile.Equals("BDO", StringComparison.OrdinalIgnoreCase))
                        {
                            hasBdoRole = true;

                            //B
                            if (!string.IsNullOrEmpty(transation.BDOBranchInfo))
                            {
                                //2
                                bdoBranchInfo = transation.BDOBranchInfo;
                                tracingService.Trace($"Extracted BDOBranchInfo: {bdoBranchInfo} from transaction with BDO role");
                                break;
                            }
                            else
                            {
                                tracingService.Trace("BDOBranchInfo not found on transaction.");
                            }
                        }
                    }
                    bool hasValidAccountName = false;
                    if (party.Account != null && !string.IsNullOrEmpty(party.Account.accountName))
                    {
                        //C
                        var accountName = party.Account.accountName.Trim();
                        if (accountName.StartsWith("House Account", StringComparison.OrdinalIgnoreCase))
                        {
                            hasValidAccountName = true;
                            houseAccountName = party.Account.accountName;
                        }

                    }

                    //1
                    if (hasBdoRole && !string.IsNullOrEmpty(bdoBranchInfo) && hasValidAccountName)
                    {
                        isHousePayload = true;
                        tracingService.Trace("House payload VERIFIED.");
                    }
                    else
                    {
                        tracingService.Trace("Not a House payload.");
                    }
                    if (isHousePayload)
                    {
                        tracingService.Trace($"House payload detected. Account: {houseAccountName}, BDOBranchInfo: {bdoBranchInfo}");
                        //3
                        if (!string.IsNullOrEmpty(bdoBranchInfo))
                        {
                            tracingService.Trace($"House payload BDOBranchInfo: {bdoBranchInfo}");
                            QueryExpression branchquery = new QueryExpression("stt_officelocationbranch");
                            branchquery.ColumnSet = new ColumnSet("stt_officelocationbranchid", "stt_name", "stt_houseaccountuser");
                            branchquery.Criteria.AddCondition("stt_name", ConditionOperator.Equal, bdoBranchInfo);
                            EntityCollection branchResult = service.RetrieveMultiple(branchquery);
                            if (branchResult.Entities.Count > 0)
                            {
                                houseAccountUser = branchResult.Entities[0].GetAttributeValue<EntityReference>("stt_houseaccountuser");
                                if (houseAccountUser != null)
                                {
                                    tracingService.Trace($"House Account User found. SystemUserId: {houseAccountUser.Id}");

                                }
                                else
                                {
                                    tracingService.Trace("Office Location Branch matched but stt_houseaccountuser is NULL.");
                                }
                            }
                        }
                    }
                    #endregion

                    if (!String.IsNullOrEmpty(party.Transactions[0].roleinFile))
                    {
                        if (party.Transactions[0].roleinFile.ToLower() == "buyer" || party.Transactions[0].roleinFile.ToLower() == "seller")
                        {
                            ignoreParty = true;
                        }
                        if (isTPS)
                        {
                            CheckRoles(tracingService, party);
                        }
                        else
                        {
                            isContact = true;
                        }
                    }
                }
                if (!ignoreParty)
                {

                    if (isHousePayload)
                    {
                        if (party.Transactions != null)
                        {
                            SetTransactionVariables(service, tracingService, party, party.Transactions[0]);
                        }
                        if (party.Account != null && !string.IsNullOrEmpty(party.Account.accountID))
                        {
                            UpsertAccount(service, tracingService, party);
                        }
                        if (party.Transactions != null)
                        {
                            ProcessTransactionsArray(service, tracingService, party);
                        }
                    }
                    else
                    {
                        if (party.Transactions != null)
                        {
                            SetTransactionVariables(service, tracingService, party, party.Transactions[0]);
                        }
                        if (isBDO)
                        {
                            tracingService.Trace("Processing BDO party.");
                            FindUser(service, tracingService, party);
                        }
                        if (isContact || party.Transactions == null || party.Transactions.Length == 0)
                        {
                            if (party.Account != null && !String.IsNullOrEmpty(party.Account.accountID))
                            {
                                UpsertAccount(service, tracingService, party);
                            }

                            if (!String.IsNullOrEmpty(party.UUID))
                            {
                                UpsertContact(service, tracingService, party);
                            }
                            if (contactId != null && contactId != Guid.Empty)
                            {
                                UpsertAddresses(service, tracingService, party);
                            }
                        }
                        if (party.Transactions != null)
                        {
                            ProcessTransactionsArray(service, tracingService, party);
                        }
                    }

                }
                else
                {
                    tracingService.Trace("Party will be ignored.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException($"{ex.Message}", ex);
            }
        }
    }
}

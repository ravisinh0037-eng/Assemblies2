namespace StewartTitle.Argano.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Remoting.Services;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Match Opportunity to Transaction.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Update" - Primary Entity: "stt_transaction" - Secondary Entity: n/a - Filtering Attributes: stt_filenumber, stt_transactionstatuscode, stt_finalcloseon
    ///     Run as: Calling User - Execution Order: 2
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// Message: "Create" - Primary Entity: "stt_transaction" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    public class CreateUpdatePostOpTransaction : IPlugin
    {
        /// <inheritdoc/>
        IOrganizationService service = null;
        ITracingService tracingService = null;
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            tracingService.Trace("context.Depth = " + context.Depth);
           

            tracingService.Trace("CreateUpdatePostOpTransaction plugin triggered");

            int transactionopen = 924510000;
            int transactionclosed = 924510001;
            int transactioncancelled = 924510002;

            int opptransaction = 924510003;
            int oppclosed = 924510004;
            int oppcancelled = 924510005;

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
                {
                    Entity transactionRef = (Entity)context.InputParameters["Target"];
                    Entity transaction = service.Retrieve(transactionRef.LogicalName, transactionRef.Id, new ColumnSet("stt_directingclientdetailsid","stt_directingclientid", "stt_marketid", "stt_bdoid", "stt_istransaction", "stt_filenumber", "stt_opportunityid", "stt_transactionstatuscode", "stt_finalcloseon"));

                    if (transaction.Contains("stt_filenumber") && transaction["stt_filenumber"] != null)
                    {
                        if (!transaction.Contains("stt_opportunityid") || transaction["stt_opportunityid"] == null)
                        {
                            string fileNumber = transaction["stt_filenumber"].ToString();
                            tracingService.Trace($"Transaction has File number: {fileNumber}");

                            QueryExpression opportunityQuery = new QueryExpression("opportunity");
                            opportunityQuery.ColumnSet = new ColumnSet("opportunityid");
                            opportunityQuery.Criteria.AddCondition("stt_filenumber", ConditionOperator.Equal, fileNumber);

                            EntityCollection opportunityResults = service.RetrieveMultiple(opportunityQuery);

                            if (opportunityResults.Entities.Count > 0)
                            {
                                tracingService.Trace($"Found {opportunityResults.Entities.Count} opportunity(ies) with stt_filenumber: {fileNumber}");

                                EntityReference opportunityRef = opportunityResults.Entities[0].ToEntityReference();

                                Entity transactionToUpdate = new Entity(transactionRef.LogicalName, transactionRef.Id);
                                transactionToUpdate["stt_opportunityid"] = new EntityReference("opportunity", opportunityRef.Id);
                                service.Update(transactionToUpdate);

                                tracingService.Trace($"Assigned Opportunity {opportunityRef.Id} to stt_transaction {transactionRef.Id} in stt_opportunityid field in Transaction.");


                                Entity opportunityToUpdate = new Entity("opportunity", opportunityRef.Id);
                                opportunityToUpdate["stt_transactionid"] = new EntityReference(transactionRef.LogicalName, transactionRef.Id);

                                //Logic related to update NCS stage
                                if (context.MessageName.ToLower() == "create")
                                {
                                    opportunityToUpdate["stt_ncsstagecode"] = new OptionSetValue(opptransaction);
                                }
                                else
                                {
                                    if (transaction.Contains("stt_transactionstatuscode") && transaction["stt_transactionstatuscode"] != null)
                                    {
                                        OptionSetValue transactionstatuscode = (OptionSetValue)transaction["stt_transactionstatuscode"];
                                        tracingService.Trace($"Transaction StatusCode: {transactionstatuscode.Value}");
                                        if (transactionstatuscode.Value == transactioncancelled)
                                        {
                                            opportunityToUpdate["stt_ncsstagecode"] = new OptionSetValue(oppcancelled);
                                        }
                                        else
                                        {
                                            if (transactionstatuscode.Value == transactionclosed || (transaction.Contains("stt_finalcloseon") && transaction["stt_finalcloseon"] != null))
                                            {
                                                opportunityToUpdate["stt_ncsstagecode"] = new OptionSetValue(oppclosed);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (transaction.Contains("stt_finalcloseon") && transaction["stt_finalcloseon"] != null)
                                        {
                                            opportunityToUpdate["stt_ncsstagecode"] = new OptionSetValue(oppclosed);
                                        }
                                    }
                                }
                                service.Update(opportunityToUpdate);

                                tracingService.Trace($"Assigned Transaction {transactionRef.Id} to opportunity {opportunityRef.Id} in stt_transactionid field in Opportunity.");

                            }
                            else
                            {
                                tracingService.Trace($"No opportunity found with stt_filenumber: {fileNumber}");
                            }
                        }
                        else
                        {
                            EntityReference oppRef = (EntityReference)transaction["stt_opportunityid"];
                            tracingService.Trace($"Transaction already has stt_opportunityid set. OpportunityId: {oppRef.Id}");

                            Entity oppToUpdate = new Entity("opportunity", oppRef.Id);
                            tracingService.Trace($"Context Message: {context.MessageName.ToLower()}");
                            if (context.MessageName.ToLower() == "create")
                            {
                                oppToUpdate["stt_ncsstagecode"] = new OptionSetValue(opptransaction);
                            }
                            else
                            {
                                if (transaction.Contains("stt_transactionstatuscode") && transaction["stt_transactionstatuscode"] != null)
                                {
                                    OptionSetValue transactionstatuscode = (OptionSetValue)transaction["stt_transactionstatuscode"];
                                    tracingService.Trace($"Transaction StatusCode: {transactionstatuscode.Value}");
                                    if (transactionstatuscode.Value == transactioncancelled)
                                    {
                                        oppToUpdate["stt_ncsstagecode"] = new OptionSetValue(oppcancelled);
                                    }
                                    else
                                    {
                                        if (transactionstatuscode.Value == transactionclosed || (transaction.Contains("stt_finalcloseon") && transaction["stt_finalcloseon"] != null))
                                        {
                                            oppToUpdate["stt_ncsstagecode"] = new OptionSetValue(oppclosed);
                                        }
                                        else
                                        {
                                            if (transactionstatuscode.Value == transactionopen)
                                            {
                                                oppToUpdate["stt_ncsstagecode"] = new OptionSetValue(opptransaction);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (transaction.Contains("stt_finalcloseon") && transaction["stt_finalcloseon"] != null)
                                    {
                                        oppToUpdate["stt_ncsstagecode"] = new OptionSetValue(oppclosed);
                                    }
                                }
                            }
                            service.Update(oppToUpdate);
                            tracingService.Trace($"OpportunityId: {oppRef.Id} updated");
                        }
                    }
                    else
                    {
                        tracingService.Trace("Transaction doesn't have File number");
                    }
                    CreateClientDetails(transaction);
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error in UpdateClientDetails: " + ex.ToString());
                throw new InvalidPluginExecutionException("An error occurred while updating Client Details: " + ex.Message);
            }
        }
        private EntityCollection RetrieveRelatedTransactionRole(Entity transaction)
        {
            string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                      <entity name='stt_transactionrole'>
                        <attribute name='stt_transactionroleid' />
                        <attribute name='stt_name' />
                        <attribute name='createdon' />
                        <attribute name='stt_rolecode' />
                        <attribute name='stt_contactid' />
                        <attribute name='stt_transactionid' />
                        <attribute name='stt_isdirectingcient' />
                        <attribute name='stt_clientdetailsid' />
                        <order attribute='stt_name' descending='false' />
                        <filter type='and'>
                          <condition attribute='stt_transactionid' operator='eq'  uitype='stt_transaction' value='" + transaction.Id + @"' />
                          <condition attribute='stt_clientdetailsid' operator='null' />
                          <condition attribute='statecode' operator='eq' value='0' />
                        </filter>
                      </entity>
                    </fetch>";
            EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
            return collection;
        }
        private void CreateClientDetails(Entity transaction)
        {
            tracingService.Trace("INSIDE CreateClientDetails FUNCTION");
            EntityReference directingclientdetail = transaction.Contains("stt_directingclientdetailsid") && transaction["stt_directingclientdetailsid"] != null ?
                        transaction.GetAttributeValue<EntityReference>("stt_directingclientdetailsid") : null;
            EntityCollection collection = RetrieveRelatedTransactionRole(transaction);
            tracingService.Trace("collection.Entities.Count = "+ collection.Entities.Count);
            if (collection.Entities.Count > 0)
            {
                foreach (Entity tranasactionRole in collection.Entities)
                {
                    bool isDirectingClient = tranasactionRole.Contains("stt_isdirectingcient") && tranasactionRole["stt_isdirectingcient"] != null?
                        tranasactionRole.GetAttributeValue<bool>("stt_isdirectingcient") : false;
                    EntityReference contactIdReference = tranasactionRole.Contains("stt_contactid") && tranasactionRole["stt_contactid"] != null ?
                        tranasactionRole.GetAttributeValue<EntityReference>("stt_contactid") : null;
                    if (contactIdReference != null)
                    {
                        // Set Condition Values
                        var query_stt_typecode = 924510000;
                        var query_statecode = 0;

                        // Instantiate QueryExpression query
                        var query = new QueryExpression("stt_stewartsettings");
                        query.TopCount = 50;

                        // Add columns to query.ColumnSet
                        query.ColumnSet.AddColumns("stt_stewartsettingsid", "stt_name", "stt_purposecode");

                        // Add conditions to query.Criteria
                        query.Criteria.AddCondition("stt_typecode", ConditionOperator.Equal, query_stt_typecode);
                        query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);

                        EntityCollection stewartSettingsResults = service.RetrieveMultiple(query);

                        // Initialize variables to store the results
                        Entity stewartSettingTrans = null;
                        Entity stewartSettingAct = null;

                        // Iterate through the results and assign to the appropriate variable
                        foreach (Entity stewartSetting in stewartSettingsResults.Entities)
                        {
                            int purposeCode = stewartSetting.GetAttributeValue<OptionSetValue>("stt_purposecode")?.Value ?? -1;

                            if (purposeCode == 924510001)
                            {
                                stewartSettingTrans = stewartSetting;
                                tracingService.Trace($"Assigned Stewart Setting to Trans: {stewartSetting.GetAttributeValue<string>("stt_name")}");
                            }
                            else
                            {
                                stewartSettingAct = stewartSetting;
                                tracingService.Trace($"Assigned Stewart Setting to Act: {stewartSetting.GetAttributeValue<string>("stt_name")}");
                            }
                        }

                        // Check if Transaction is Transaction(stt_istransaction eq True)
                        if (transaction.Contains("stt_istransaction") && transaction.GetAttributeValue<bool>("stt_istransaction"))
                        {
                            tracingService.Trace("Transaction is a Transaction, proceeding with contact and client details creation.");
                            if (transaction.Contains("stt_marketid") && transaction["stt_marketid"] != null)
                            {
                                EntityReference bdoUserReference = new EntityReference();
                                EntityReference isaReference = new EntityReference();

                                // Set the ref for Market
                                EntityReference marketRef = (EntityReference)transaction["stt_marketid"];

                                // Get the Market with Outreach Owner field
                                Entity market = service.Retrieve(marketRef.LogicalName, marketRef.Id, new ColumnSet("stt_outreachownercode", "ownerid", "stt_msaid"));
                                EntityReference ownerReference = market.GetAttributeValue<EntityReference>("ownerid");

                                if (transaction.Contains("stt_bdoid"))
                                {
                                    bdoUserReference = transaction.GetAttributeValue<EntityReference>("stt_bdoid");

                                    QueryExpression marketTeamQuery = new QueryExpression("stt_marketteam");
                                    marketTeamQuery.ColumnSet = new ColumnSet("stt_isaid");
                                    marketTeamQuery.Criteria.AddCondition("stt_marketid", ConditionOperator.Equal, market.Id);
                                    marketTeamQuery.Criteria.AddCondition("stt_bdoid", ConditionOperator.Equal, bdoUserReference.Id);

                                    EntityCollection marketTeams = service.RetrieveMultiple(marketTeamQuery);

                                    if (marketTeams.Entities.Any())
                                    {
                                        Entity marketTeam = marketTeams.Entities.First();
                                        if (marketTeam.Contains("stt_isaid"))
                                        {
                                            isaReference = marketTeam.GetAttributeValue<EntityReference>("stt_isaid");
                                        }

                                        if (isaReference != null)
                                        {
                                            Guid isaId = isaReference.Id;
                                            tracingService.Trace($"Found Market Team and ISA ID: {isaId}");
                                        }
                                        else
                                        {
                                            tracingService.Trace($"ISA ID is null in the found Market Team.");
                                        }
                                    }
                                }

                                // Check for existing stt_clientdetails
                                QueryExpression clientDetailQuery = new QueryExpression("stt_clientdetails");
                                clientDetailQuery.ColumnSet = new ColumnSet("stt_clientdetailsid");
                                clientDetailQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactIdReference.Id);
                                clientDetailQuery.Criteria.AddCondition("stt_marketid", ConditionOperator.Equal, marketRef.Id);

                                EntityCollection clientDetailResults = service.RetrieveMultiple(clientDetailQuery);

                                if (clientDetailResults.Entities.Count == 0)
                                {
                                    // Create stt_clientdetail
                                    Entity clientDetail = new Entity("stt_clientdetails");
                                    clientDetail["stt_contactid"] = new EntityReference("contact", contactIdReference.Id);
                                    clientDetail["stt_marketid"] = new EntityReference("stt_market", marketRef.Id);
                                    clientDetail["stt_name"] = contactIdReference.Name + marketRef.Name;
                                    clientDetail["stt_lifecyclesettingstransactionsid"] = new EntityReference(stewartSettingTrans.LogicalName, stewartSettingTrans.Id);
                                    clientDetail["stt_lifecyclesettingsactivitiesid"] = new EntityReference(stewartSettingAct.LogicalName, stewartSettingAct.Id);
                                    tracingService.Trace($"BDO: {bdoUserReference.Id}");

                                    tracingService.Trace($"ISA: {isaReference.Id}");
                                    if (isaReference != null && isaReference.Id != Guid.Empty)
                                    {
                                        clientDetail["stt_isaid"] = new EntityReference(isaReference.LogicalName, isaReference.Id);
                                    }

                                    clientDetail["ownerid"] = new EntityReference(ownerReference.LogicalName, ownerReference.Id);

                                    if (isDirectingClient)
                                    {
                                        tracingService.Trace($"Directing Client:True and + {isaReference.Id}+ Adding bdo ");
                                        if (bdoUserReference != null && bdoUserReference.Id != Guid.Empty)
                                        {
                                            clientDetail["stt_bdoid"] = new EntityReference(bdoUserReference.LogicalName, bdoUserReference.Id);
                                        }

                                        clientDetail["stt_originalsource"] = new OptionSetValue(924510003);
                                        clientDetail["stt_clientstagecode"] = new OptionSetValue(924510002); //924510002 : Prospect
                                    }
                                    else
                                    {
                                        clientDetail["stt_originalsource"] = new OptionSetValue(924510002);
                                        clientDetail["stt_clientstagecode"] = new OptionSetValue(924510000); //924510000 : Cold producer
                                    }

                                    Guid clientDetailId = service.Create(clientDetail);
                                    tracingService.Trace($"Client Detail created with ID: {clientDetailId}");

                                    // Update Transaction with the Client Detail if it is Directing Client
                                    if (isDirectingClient)
                                    {
                                        tracingService.Trace("Trying to update the transaction with the Client Details because is it Directing Client");
                                        Entity transactionToUpdate = new Entity(transaction.LogicalName, transaction.Id);
                                        transactionToUpdate.Attributes.Add("stt_directingclientdetailsid", new EntityReference(clientDetail.LogicalName, clientDetailId));
                                        service.Update(transactionToUpdate);
                                        tracingService.Trace("Transaction updated");
                                    }

                                    // Update Transaction Role with the Client Details
                                    tracingService.Trace("Updating the Transaction Role with the actual Client Details.");
                                    Entity transactionRoleToUpdate = new Entity("stt_transactionrole", tranasactionRole.Id);
                                    transactionRoleToUpdate["stt_clientdetailsid"] = new EntityReference("stt_clientdetails", clientDetailId);
                                    service.Update(transactionRoleToUpdate);

                                    // Create stt_clientbranddetail
                                    Entity clientBrandDetail = new Entity("stt_clientbranddetails");
                                    clientBrandDetail["stt_contactid"] = new EntityReference("contact", contactIdReference.Id);
                                    clientBrandDetail["stt_marketid"] = new EntityReference("stt_market", marketRef.Id);
                                    clientBrandDetail["stt_name"] = contactIdReference.Name + marketRef.Name;
                                    clientBrandDetail["ownerid"] = new EntityReference(ownerReference.LogicalName, ownerReference.Id);

                                    Guid clientBrandDetailId = service.Create(clientBrandDetail);
                                    tracingService.Trace($"Client Brand Detail created with ID: {clientBrandDetailId}");

                                    EntityReference msaRef = market.GetAttributeValue<EntityReference>("stt_msaid");

                                    // Check if a Client MSA Details record already exists and if not create one
                                    var clientMSAquery = new QueryExpression("stt_clientmsadetails")
                                    {
                                        TopCount = 50,
                                        ColumnSet = new ColumnSet("stt_clientmsadetailsid"),
                                        Criteria =
                                                {
                                                    Conditions =
                                                    {
                                                        new ConditionExpression("stt_contactid", ConditionOperator.Equal, contactIdReference.Id),
                                                        new ConditionExpression("stt_msaid", ConditionOperator.Equal, msaRef.Id),
                                                    },
                                                },
                                    };
                                    EntityCollection existingMSADetails = service.RetrieveMultiple(clientMSAquery);
                                    if (existingMSADetails.Entities.Count > 0)
                                    {
                                        tracingService.Trace($"stt_clientmsadetail already exists for Contact '{contactIdReference.Id}' and MSA '{msaRef?.Id}'. Skipping creation.");
                                    }
                                    else
                                    {
                                        Entity msaDetail = new Entity("stt_clientmsadetails");
                                        msaDetail["stt_name"] = $"{contactIdReference.Name} - {msaRef.Name}";
                                        msaDetail["stt_contactid"] = new EntityReference("contact", contactIdReference.Id);
                                        msaDetail["stt_msaid"] = new EntityReference(msaRef.LogicalName, msaRef.Id);

                                        // msaDetail["stt_productiongradecode"] = new OptionSetValue(198730003);
                                        // msaDetail["stt_productionscore"] = new decimal(0);
                                        msaDetail["ownerid"] = new EntityReference(market.GetAttributeValue<EntityReference>("ownerid").LogicalName, market.GetAttributeValue<EntityReference>("ownerid").Id);
                                        service.Create(msaDetail);
                                    }

                                }
                                else
                                {
                                    tracingService.Trace("A Client Details record already exist, updating the Transaction Role with the actual Client Details.");
                                    Entity transactionRoleToUpdate = new Entity("stt_transactionrole", tranasactionRole.Id);
                                    transactionRoleToUpdate["stt_clientdetailsid"] = new EntityReference("stt_clientdetails", clientDetailResults.Entities[0].ToEntityReference().Id);
                                    service.Update(transactionRoleToUpdate);
                                }
                            }
                            else
                            {
                                tracingService.Trace("The Transaction doesn't have Market associated, skipping creation.");
                            }
                        }
                        else
                        {
                            tracingService.Trace("Transaction is Production, checking for related MSAs and Client Details.");

                            // 1. Get all stt_transactionmsa for this transaction
                            QueryExpression msaQuery = new QueryExpression("stt_transactionmsa")
                            {
                                ColumnSet = new ColumnSet("stt_msaid"),
                                Criteria = new FilterExpression
                                {
                                    Conditions =
                                                    {
                                                        new ConditionExpression("stt_transactionid", ConditionOperator.Equal, transaction.Id),
                                                    },
                                },
                            };
                            EntityCollection transactionMsas = service.RetrieveMultiple(msaQuery);

                            foreach (var transactionMsa in transactionMsas.Entities)
                            {
                                if (!transactionMsa.Contains("stt_msaid")) continue;

                                EntityReference msaRef = transactionMsa.GetAttributeValue<EntityReference>("stt_msaid");

                                // 2. Get the stt_market from the MSA
                                Entity msa = service.Retrieve(msaRef.LogicalName, msaRef.Id, new ColumnSet("stt_metropolitanstatisticalareaid", "stt_name", "ownerid"));

                                QueryExpression marketQuery = new QueryExpression("stt_market")
                                {
                                    ColumnSet = new ColumnSet("stt_marketid", "stt_name", "stt_msaid", "ownerid"),
                                    Criteria = new FilterExpression
                                    {
                                        Conditions =
                                                    {
                                                        new ConditionExpression("stt_msaid", ConditionOperator.Equal, msaRef.Id),
                                                    },
                                    },
                                };
                                EntityCollection markets = service.RetrieveMultiple(marketQuery);

                                foreach (Entity market in markets.Entities)
                                {
                                    EntityReference marketRef = market.ToEntityReference();

                                    // 3. Check if stt_clientdetails exists for this contact and market
                                    QueryExpression clientDetailQuery = new QueryExpression("stt_clientdetails")
                                    {
                                        ColumnSet = new ColumnSet("stt_clientdetailsid"),
                                        Criteria = new FilterExpression
                                        {
                                            Conditions =
                                                    {
                                                        new ConditionExpression("stt_contactid", ConditionOperator.Equal, contactIdReference.Id),
                                                        new ConditionExpression("stt_marketid", ConditionOperator.Equal, marketRef.Id),
                                                    },
                                        },
                                    };
                                    EntityCollection clientDetails = service.RetrieveMultiple(clientDetailQuery);

                                    if (clientDetails.Entities.Count == 0)
                                    {
                                        Entity clientDetail = new Entity("stt_clientdetails");

                                        tracingService.Trace($"No Client Details found for contact {contactIdReference.Id} and market {marketRef.Id}, creating new Client Details.");

                                        QueryExpression marketTeamQuery = new QueryExpression("stt_marketteam")
                                        {
                                            ColumnSet = new ColumnSet("stt_isaid", "stt_bdoid"),
                                            Criteria = new FilterExpression
                                            {
                                                Conditions =
                                                        {
                                                            new ConditionExpression("stt_marketid", ConditionOperator.Equal, marketRef.Id),
                                                        },
                                            },
                                        };
                                        EntityCollection marketTeams = service.RetrieveMultiple(marketTeamQuery);

                                        if (marketTeams.Entities.Count > 1 || marketTeams.Entities.Count == 0)
                                        {
                                            // More than one Market Team, no BDO or ISA assigned.
                                            tracingService.Trace($"More than one Market Team found for market {marketRef.Id}, or no Market Team found. No BDO or ISA assigned.");
                                        }
                                        else
                                        {
                                            Entity marketTeam = marketTeams.Entities.FirstOrDefault();
                                            if (marketTeam != null)
                                            {
                                                if (marketTeam.Contains("stt_bdoid"))
                                                {
                                                    clientDetail["stt_bdoid"] = new EntityReference(marketTeam.GetAttributeValue<EntityReference>("stt_bdoid").LogicalName, marketTeam.GetAttributeValue<EntityReference>("stt_bdoid").Id);
                                                }

                                                if (marketTeam.Contains("stt_isaid"))
                                                {
                                                    clientDetail["stt_isaid"] = new EntityReference(marketTeam.GetAttributeValue<EntityReference>("stt_isaid").LogicalName, marketTeam.GetAttributeValue<EntityReference>("stt_isaid").Id);
                                                }
                                            }
                                        }

                                        // 4. Create new stt_clientdetails
                                        clientDetail["stt_contactid"] = new EntityReference("contact", contactIdReference.Id);
                                        clientDetail["stt_marketid"] = new EntityReference("stt_market", marketRef.Id);
                                        clientDetail["stt_lifecyclesettingstransactionsid"] = new EntityReference(stewartSettingTrans.LogicalName, stewartSettingTrans.Id);
                                        clientDetail["stt_lifecyclesettingsactivitiesid"] = new EntityReference(stewartSettingAct.LogicalName, stewartSettingAct.Id);
                                        clientDetail["stt_name"] = contactIdReference.Name + marketRef.Name;
                                        clientDetail["stt_originalsource"] = new OptionSetValue(924510006);
                                        clientDetail["stt_clientstagecode"] = new OptionSetValue(924510000);

                                        Guid clientDetailId = service.Create(clientDetail);
                                        tracingService.Trace($"Created new Client Details for contact {contactIdReference.Id} and market {marketRef.Id}: {clientDetailId}");
                                    }
                                    else
                                    {
                                        tracingService.Trace($"Client Details already exists for contact {contactIdReference.Id} and market {marketRef.Id}");
                                    }
                                }

                                var clientMSAquery = new QueryExpression("stt_clientmsadetails")
                                {
                                    TopCount = 50,
                                    ColumnSet = new ColumnSet("stt_clientmsadetailsid"),
                                    Criteria =
                                                {
                                                    Conditions =
                                                    {
                                                        new ConditionExpression("stt_contactid", ConditionOperator.Equal, contactIdReference.Id),
                                                        new ConditionExpression("stt_msaid", ConditionOperator.Equal, msa.Id),
                                                    },
                                                },
                                };
                                EntityCollection existingMSADetails = service.RetrieveMultiple(clientMSAquery);
                                if (existingMSADetails.Entities.Count > 0)
                                {
                                    tracingService.Trace($"stt_clientmsadetail already exists for Contact '{contactIdReference.Id}' and MSA '{msa?.Id}'. Skipping creation.");
                                }
                                else
                                {
                                    Entity msaDetail = new Entity("stt_clientmsadetails");
                                    msaDetail["stt_name"] = $"{contactIdReference.Name} - {msa["stt_name"]}";
                                    msaDetail["stt_contactid"] = new EntityReference("contact", contactIdReference.Id);
                                    msaDetail["stt_msaid"] = new EntityReference(msa.LogicalName, msa.Id);
                                    // msaDetail["stt_productiongradecode"] = new OptionSetValue(198730003);
                                    // msaDetail["stt_productionscore"] = new decimal(0);
                                    msaDetail["ownerid"] = new EntityReference(msa.GetAttributeValue<EntityReference>("ownerid").LogicalName, msa.GetAttributeValue<EntityReference>("ownerid").Id);
                                    service.Create(msaDetail);
                                }
                            }
                        }
                    }
                }
            }
        }

    }
}

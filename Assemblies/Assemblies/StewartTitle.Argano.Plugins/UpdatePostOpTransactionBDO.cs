// <copyright file="UpdatePostOpTransactionBDO.cs" company="Stewart">
// Copyright 2025 Stewart
// </copyright>

namespace StewartTitle.Argano.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Plugin to handle the update to a BDO field in Transaction.
    /// Checks if the Transaction has a BDO and retrieves all associated Transaction Roles.
    /// Updates the associated Client Details with the BDO and ISA information.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Update" - Primary Entity: "stt_transaction" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    /// </remarks>
    public class UpdatePostOpTransactionBDO : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Retrieve the execution context, service factory, and tracing service
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                // Ensure the Target entity exists in the context
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
                {
                    Entity transaction = new Entity();
                    if (context.PostEntityImages.Contains("PostImage"))
                    {
                        // Retrieve the updated transaction record
                         transaction = (Entity)context.PostEntityImages["PostImage"];
                    }
                    else
                    {
                        // The post image is not present, skipping the action.
                        tracingService.Trace("The plugin does not contain PostImage, skipping update");
                    }

                    // Check if the target entity is the expected logical name
                    if (targetEntity.LogicalName == "stt_transaction")
                    {
                        tracingService.Trace("Processing update for stt_transaction.");

                        // Check if the transaction has a BDO
                        if (transaction.Contains("stt_bdoid") && transaction["stt_bdoid"] != null)
                        {
                            tracingService.Trace("Transaction has a BDO. Retrieving associated Transaction Roles.");

                            if (transaction.Contains("stt_marketid") && transaction["stt_marketid"] != null)
                            {
                                // Query to retrieve all Transaction Roles associated with the Transaction
                                QueryExpression transactionRoleQuery = new QueryExpression("stt_transactionrole");
                                transactionRoleQuery.ColumnSet = new ColumnSet("stt_transactionroleid", "stt_name", "stt_clientdetailsid");
                                transactionRoleQuery.Criteria.AddCondition("stt_transactionid", ConditionOperator.Equal, transaction.Id);

                                EntityCollection transactionRoles = service.RetrieveMultiple(transactionRoleQuery);

                                tracingService.Trace($"Found {transactionRoles.Entities.Count} Transaction Role(s) associated with the Transaction.");

                                EntityReference bdoUserReference = new EntityReference();
                                EntityReference isaReference = null;

                                // Set the ref for Market
                                EntityReference marketRef = (EntityReference)transaction["stt_marketid"];

                                // Get the Market with Outreach Owner field
                                Entity market = service.Retrieve(marketRef.LogicalName, marketRef.Id, new ColumnSet("stt_outreachownercode", "ownerid"));
                                
                                bdoUserReference = transaction.GetAttributeValue<EntityReference>("stt_bdoid");

                                if (market == null)
                                {
                                    tracingService.Trace("Market not found based on ID. ISA will not be defined.");
                                }
                                else
                                {
                                    // Get the ISA for the Market Team owned by the BDO
                                    QueryExpression marketTeamQuery = new QueryExpression("stt_marketteam");
                                    marketTeamQuery.ColumnSet = new ColumnSet("stt_isaid");
                                    marketTeamQuery.Criteria.AddCondition("stt_marketid", ConditionOperator.Equal, market.Id);
                                    marketTeamQuery.Criteria.AddCondition("stt_bdoid", ConditionOperator.Equal, bdoUserReference.Id);

                                    EntityCollection marketTeams = service.RetrieveMultiple(marketTeamQuery);

                                    if (marketTeams.Entities.Any())
                                    {
                                        Entity marketTeam = marketTeams.Entities.First();
                                        isaReference = marketTeam.GetAttributeValue<EntityReference>("stt_isaid");
                                    }
                                    else
                                    {
                                        tracingService.Trace("No Market Team found for the specified Market and BDO.");
                                    }
                                }

                                // Process each Transaction Role (if needed)
                                foreach (Entity transactionRole in transactionRoles.Entities)
                                {
                                    tracingService.Trace($"Processing Transaction Role: {transactionRole.GetAttributeValue<string>("stt_name")}");

                                    if (!transactionRole.Contains("stt_clientdetailsid") || transactionRole["stt_clientdetailsid"] == null)
                                    {
                                        tracingService.Trace("Transaction Role does not have a Client Details reference. Skipping update.");
                                        continue;
                                    }

                                    EntityReference clientDetailsRef = transactionRole.GetAttributeValue<EntityReference>("stt_clientdetailsid");

                                    Entity clientDetails = new Entity("stt_clientdetails", clientDetailsRef.Id);
                                    clientDetails["stt_bdoid"] = new EntityReference(bdoUserReference.LogicalName, bdoUserReference.Id);
                                    if (isaReference != null)
                                    {
                                        clientDetails["stt_isaid"] = new EntityReference(isaReference.LogicalName, isaReference.Id);
                                    }

                                    service.Update(clientDetails);
                                }
                            }
                            else
                            {
                                tracingService.Trace("Transaction does not have a Market. No further action required.");
                            }
                        }
                        else
                        {
                            tracingService.Trace("Transaction does not have a BDO. No further action required.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in UpdatePostOpTransactionBDO plugin: {ex.ToString()}");
                throw new InvalidPluginExecutionException($"Error in UpdatePostOpTransactionBDO plugin: {ex.ToString()}");
            }
        }
    }
}

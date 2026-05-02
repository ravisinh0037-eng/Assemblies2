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
    /// Match Opportunity to Transaction.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Update" - Primary Entity: "opportunity" - Secondary Entity: n/a - Filtering Attributes: stt_filenumber
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    public class UpdatePostOpOpportunity : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

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
                    Entity opportunityRef = (Entity)context.InputParameters["Target"];

                    Entity opportunity = service.Retrieve(opportunityRef.LogicalName, opportunityRef.Id, new ColumnSet("stt_filenumber", "stt_transactionid"));
                    if (opportunity.Contains("stt_filenumber") && opportunity["stt_filenumber"] != null)
                    {
                        if (!opportunity.Contains("stt_transactionid") || opportunity["stt_transactionid"] == null)
                        {
                            string fileNumber = opportunity["stt_filenumber"].ToString();
                            tracingService.Trace($"Opportunity has File number: {fileNumber}");

                            QueryExpression transactionQuery = new QueryExpression("stt_transaction");
                            transactionQuery.ColumnSet = new ColumnSet("stt_transactionid", "stt_opportunityid", "stt_transactionstatuscode", "stt_finalcloseon");
                            transactionQuery.Criteria.AddCondition("stt_filenumber", ConditionOperator.Equal, fileNumber);
                            transactionQuery.Criteria.AddCondition("stt_opportunityid", ConditionOperator.Null);

                            EntityCollection transactionResults = service.RetrieveMultiple(transactionQuery);
                            tracingService.Trace($"TransactionResults Count: {transactionResults.Entities.Count}");

                            if (transactionResults.Entities.Count > 0)
                            {
                                // Update Transaction
                                tracingService.Trace($"Found {transactionResults.Entities.Count} transaction(s) with stt_filenumber: {fileNumber}");

                                EntityReference transacRef = transactionResults.Entities[0].ToEntityReference();

                                Entity transactionToUpdate = new Entity(transacRef.LogicalName, transacRef.Id);
                                transactionToUpdate["stt_opportunityid"] = new EntityReference("opportunity", opportunity.Id);
                                service.Update(transactionToUpdate);

                                tracingService.Trace($"Assigned Opportunity {opportunity.Id} to stt_transaction {transacRef.Id} in stt_opportunityid field in Transaction.");

                                // Update Opportunity
                                Entity opportunityToUpdate = new Entity("opportunity", opportunity.Id);
                                opportunityToUpdate["stt_transactionid"] = new EntityReference(transacRef.LogicalName, transacRef.Id);

                                // Logic related to update NCS stage
                                Entity transaction = transactionResults.Entities[0];

                                if (transaction.Contains("stt_transactionstatuscode") && transaction["stt_transactionstatuscode"] != null)
                                {
                                        OptionSetValue transactionstatuscode = (OptionSetValue)transaction["stt_transactionstatuscode"];
                                        tracingService.Trace($"Transaction StatusCode: {transactionstatuscode.Value}");

                                        if (transactionstatuscode.Value == transactionopen)
                                        {
                                            opportunityToUpdate["stt_ncsstagecode"] = new OptionSetValue(opptransaction);
                                        }
                                        else
                                        {
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
                                }
                                else
                                {
                                        if (transaction.Contains("stt_finalcloseon") && transaction["stt_finalcloseon"] != null)
                                        {
                                            opportunityToUpdate["stt_ncsstagecode"] = new OptionSetValue(oppclosed);
                                        }
                                }

                                service.Update(opportunityToUpdate);

                                tracingService.Trace($"Assigned Transaction {transaction.Id} to opportunity {opportunity.Id} in stt_transactionid field in Opportunity.");
                            }
                            else
                            {
                                tracingService.Trace($"No Transaction found with stt_filenumber: {fileNumber}");
                            }
                        }
                        else
                        {
                            EntityReference transactionRef = (EntityReference)opportunity["stt_transactionid"];
                            tracingService.Trace($"Opportunity already has a Transaction set. TransactionId: {transactionRef.Id}");
                        }
                    }
                    else
                    {
                        tracingService.Trace("Opportunity doesn't have File number");
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error in UpdatePostOpOpportunity: " + ex.ToString());
                throw new InvalidPluginExecutionException("An error occurred in UpdatePostOpOpportunity: " + ex.Message);
            }
        }
    }
}

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
                    Entity transactionRef = (Entity)context.InputParameters["Target"];
                    Entity transaction = service.Retrieve(transactionRef.LogicalName, transactionRef.Id, new ColumnSet("stt_filenumber", "stt_opportunityid", "stt_transactionstatuscode", "stt_finalcloseon"));
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
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error in UpdateClientDetails: " + ex.ToString());
                throw new InvalidPluginExecutionException("An error occurred while updating Client Details: " + ex.Message);
            }
        }
    }
}

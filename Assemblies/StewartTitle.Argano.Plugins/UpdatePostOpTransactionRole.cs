using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace StewartTitle.Argano.Plugins
{
    public class UpdatePostOpTransactionRole : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.MessageName.ToLower() != "update" || !context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity targetEntity))
                {
                    return;
                }

                if (targetEntity.LogicalName == "stt_transactionrole")
                {
                    tracingService.Trace("Processing transaction role update");
                    try
                    {
                        // Get the pre-image to compare changes
                        Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;

                        bool isDirectingClientUpdated = targetEntity.Contains("stt_isdirectingcient") && targetEntity.GetAttributeValue<bool>("stt_isdirectingcient");

                        EntityReference contactIdReference = null;
                        if (targetEntity.Contains("stt_contactid") && targetEntity["stt_contactid"] != null)
                        {
                            contactIdReference = targetEntity.GetAttributeValue<EntityReference>("stt_contactid");
                        }
                        else if (preImage != null && preImage.Contains("stt_contactid") && preImage["stt_contactid"] != null)
                        {
                            contactIdReference = preImage.GetAttributeValue<EntityReference>("stt_contactid");
                        }

                        EntityReference transactionRef = null;
                        if (targetEntity.Contains("stt_transactionid") && targetEntity["stt_transactionid"] != null)
                        {
                            transactionRef = targetEntity.GetAttributeValue<EntityReference>("stt_transactionid");
                        }
                        else if (preImage != null && preImage.Contains("stt_transactionid") && preImage["stt_transactionid"] != null)
                        {
                            transactionRef = preImage.GetAttributeValue<EntityReference>("stt_transactionid");
                        }

                        tracingService.Trace($"Directing client updated: {isDirectingClientUpdated}, Contact: {contactIdReference?.Id}, Transaction: {transactionRef?.Id}");


                        if (isDirectingClientUpdated && contactIdReference != null && transactionRef != null)
                        {
                            this.ProcessDirectingClientUpdate(service, tracingService, targetEntity, contactIdReference, transactionRef);
                        }
                        else
                        {
                            tracingService.Trace("Conditions not met for processing directing client update");
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace($"Error in TransactionRoleUpdateHandler: {ex.Message}");
                        throw;
                    }

                    tracingService.Trace("Plugin completed");
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in TransactionRoleUpdateHandler: {ex.Message}");
                throw;
            }
        }

        private void ProcessDirectingClientUpdate(IOrganizationService service, ITracingService tracingService, Entity targetEntity, EntityReference contactIdReference, EntityReference transactionRef)
        {
            tracingService.Trace($"Setting transaction {transactionRef.Id} with directing role {targetEntity.Id}");


            tracingService.Trace($"Updated Plugin {targetEntity.Id}");
            
            Entity transactionToUpdate = service.Retrieve("stt_transaction", transactionRef.Id, new ColumnSet("stt_directingclientid"));
            if (transactionToUpdate.Contains("stt_directingclientid") && transactionToUpdate["stt_directingclientid"] != null)
            {
                QueryExpression detailsQuery = new QueryExpression("stt_clientdetails");
                detailsQuery.ColumnSet = new ColumnSet("stt_clientdetailsid");
                detailsQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactIdReference.Id);
                EntityCollection clientdetailsResult = service.RetrieveMultiple(detailsQuery);
                tracingService.Trace($"Client Details found against this Transaction to Update {clientdetailsResult.Entities[0].Id}");

                if (clientdetailsResult.Entities.Count > 0)
                {
                    transactionToUpdate["stt_directingclientdetailsid"] = new EntityReference("stt_clientdetails", clientdetailsResult.Entities[0].Id);
                    tracingService.Trace($"stt_directingclientdetailsid Updated : {clientdetailsResult.Entities[0].Id}");
                }
            }

            transactionToUpdate["stt_directableside1id"] = new EntityReference("contact", contactIdReference.Id);
            service.Update(transactionToUpdate);

            tracingService.Trace($"Successfully updated transaction directing side to role {targetEntity.Id}");
            tracingService.Trace("Ensuring only one directing client for this transaction...");

            QueryExpression otherDirectingRolesQuery = new QueryExpression("stt_transactionrole");
            otherDirectingRolesQuery.ColumnSet = new ColumnSet("stt_transactionroleid", "stt_isdirectingcient", "stt_contactid");
            otherDirectingRolesQuery.Criteria.AddCondition("stt_transactionid", ConditionOperator.Equal, transactionRef.Id);
            otherDirectingRolesQuery.Criteria.AddCondition("stt_transactionroleid", ConditionOperator.NotEqual, targetEntity.Id);
            otherDirectingRolesQuery.Criteria.AddCondition("stt_isdirectingcient", ConditionOperator.Equal, true);

            EntityCollection otherDirectingRoles = service.RetrieveMultiple(otherDirectingRolesQuery);

            if (otherDirectingRoles.Entities.Count > 0)
            {
                tracingService.Trace($"Found {otherDirectingRoles.Entities.Count} other transaction roles marked as directing client. Updating them...");
                foreach (Entity otherRole in otherDirectingRoles.Entities)
                {
                    Entity updateRole = new Entity("stt_transactionrole", otherRole.Id);
                    updateRole["stt_isdirectingcient"] = false;
                    service.Update(updateRole);
                    tracingService.Trace($"Updated transaction role {otherRole.Id} to not be directing client");
                }
            }
            else
            {
                tracingService.Trace("No other transaction roles found marked as directing client.");
            }
        }
    }
}

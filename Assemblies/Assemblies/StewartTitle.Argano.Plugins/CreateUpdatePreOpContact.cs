// <copyright file="CreateUpdatePreOpContact.cs" company="Stewart">
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
    /// Create or assing Enterprise Contact Auto Number to Contact
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create or Update" - Primary Entity: "contact" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Pre Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    public class CreateUpdatePreOpContact : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
                {
                    if (targetEntity.LogicalName == "contact")
                    {
                        Guid contactId = targetEntity.Id;
                        tracingService.Trace($"Contact ID: {contactId}");
                        Entity contactEntity = (Entity)context.InputParameters["Target"];
                        if (contactEntity.Contains("stt_enterpriseid") && contactEntity["stt_enterpriseid"] != null)
                        {
                            tracingService.Trace("Enterprise ID is not null, plugin will exit");
                            return;
                        }

                        // Create a new Record to update
                        Entity contactToUpdate = new Entity("contact", contactId);

                        // Check if an EnterpriseContactAutoNumber record already exists for this contact
                        QueryExpression existingQuery = new QueryExpression("stt_enterprisecontactautonumber");
                        existingQuery.ColumnSet = new ColumnSet("stt_enterpriseid");
                        existingQuery.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);

                        EntityCollection existingResults = service.RetrieveMultiple(existingQuery);

                        if (existingResults.Entities.Count > 0)
                        {
                            // A record already exists, copy the existing stt_enterpriseid to the contact
                            tracingService.Trace("EnterpriseContactAutoNumber record already exists for this contact.");
                            Entity existingEnterpriseContactAutoNumber = existingResults.Entities[0];
                            if (existingEnterpriseContactAutoNumber.Contains("stt_enterpriseid") && existingEnterpriseContactAutoNumber["stt_enterpriseid"] != null)
                            {
                                string enterpriseIdValue = existingEnterpriseContactAutoNumber["stt_enterpriseid"].ToString();
                                contactToUpdate["stt_enterpriseid"] = enterpriseIdValue;
                                service.Update(contactToUpdate);
                                tracingService.Trace("Existing Enterprise ID copied to Contact record.");
                            }
                            else
                            {
                                tracingService.Trace("stt_enterpriseID was null or not found in existing record");
                            }

                            return;
                        }

                        // If it does not exist, create a new one and generate the stt_enterpriseid
                        Entity enterpriseContactAutoNumber = new Entity("stt_enterprisecontactautonumber");
                        enterpriseContactAutoNumber["stt_contactid"] = new EntityReference("contact", contactId);

                        Guid enterpriseContactAutoNumberId = service.Create(enterpriseContactAutoNumber);
                        tracingService.Trace($"New EnterpriseContactAutoNumber record created with ID: {enterpriseContactAutoNumberId}");

                        // Update Contact's Enterprise ID
                        Entity ecanResults = service.Retrieve("stt_enterprisecontactautonumber", enterpriseContactAutoNumberId, new ColumnSet("stt_enterpriseid"));

                        contactToUpdate["stt_enterpriseid"] = ecanResults["stt_enterpriseid"].ToString();
                        service.Update(contactToUpdate);
                        tracingService.Trace("Enterprise ID copied to Contact record.");
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in EnterpriseContactAutoNumber plugin: {ex.ToString()}");
                throw new InvalidPluginExecutionException($"Error in EnterpriseContactAutoNumber plugin: {ex.ToString()}");
            }
        }
    }
}

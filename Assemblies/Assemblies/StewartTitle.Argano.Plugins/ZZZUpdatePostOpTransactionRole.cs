// <copyright file="UpdatePostOpTransactionRole.cs" company="Stewart">
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
    /// When the contact related to the Transaction Role is updated, found if already exist a Client Details record if not create a new one, same for Client Brand Details .
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Update" - Primary Entity: "transaction role" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    internal class ZZZUpdatePostOpTransactionRole : IPlugin
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
                    if (targetEntity.LogicalName == "stt_transactionrole")
                    {
                        // Filter: stt_isdirectingclient equals No
                        if (targetEntity.Contains("stt_isdirectingcient") && targetEntity["stt_isdirectingcient"] != null && (bool)targetEntity["stt_isdirectingcient"] == false)
                        {
                            // Condition: stt_contactid is an existing Contact
                            if (targetEntity.Contains("stt_contactid") || targetEntity["stt_contactid"] != null)
                            {
                                EntityReference contactIdReference = (EntityReference)targetEntity["stt_contactid"];

                                // Check for stt_transaction and stt_brand
                                if (targetEntity.Contains("stt_transactionid") && targetEntity["stt_transactionid"] != null)
                                {
                                    EntityReference transactionRef = (EntityReference)targetEntity["stt_transactionid"];
                                    Entity transaction = service.Retrieve(transactionRef.LogicalName, transactionRef.Id, new ColumnSet("stt_officelocationbranchid"));

                                    if (transaction.Contains("stt_officelocationbranchid") && transaction["stt_officelocationbranchid"] != null)
                                    {
                                        EntityReference officeLocationRef = (EntityReference)transaction["stt_officelocationbranchid"];

                                        // Retrieve office location branch with Market.
                                        Entity officeLocation = service.Retrieve(officeLocationRef.LogicalName, officeLocationRef.Id, new ColumnSet("stt_marketid"));

                                        if (officeLocation.Contains("stt_marketid") && officeLocation["stt_marketid"] != null)
                                        {
                                            // Set the ref for Market
                                            EntityReference marketRef = (EntityReference)officeLocation["stt_marketid"];

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
                                                clientDetail["stt_clientstagecode"] = new OptionSetValue(924510000);
                                                clientDetail["stt_name"] = contactIdReference.Name + marketRef.Name;

                                                Guid clientDetailId = service.Create(clientDetail);
                                                tracingService.Trace($"Client Detail created with ID: {clientDetailId}");

                                                // Create stt_clientbranddetail
                                                Entity clientBrandDetail = new Entity("stt_clientbranddetails");
                                                clientBrandDetail["stt_contactid"] = new EntityReference("contact", contactIdReference.Id);
                                                clientBrandDetail["stt_marketid"] = new EntityReference("stt_market", marketRef.Id);
                                                clientBrandDetail["stt_name"] = contactIdReference.Name + marketRef.Name;

                                                Guid clientBrandDetailId = service.Create(clientBrandDetail);
                                                tracingService.Trace($"Client Brand Detail created with ID: {clientBrandDetailId}");
                                            }
                                            else
                                            {
                                                tracingService.Trace("stt_clientdetail already exists, skipping creation.");
                                            }
                                        }
                                        else
                                        {
                                            tracingService.Trace("stt_marketid is null or empty in Office Location Branch, skipping creation.");
                                        }
                                    }
                                    else
                                    {
                                        tracingService.Trace("stt_officelocationbranchid is null in transaction, skipping clientdetail creation.");
                                    }
                                }
                                else
                                {
                                    tracingService.Trace("stt_transaction is null, skipping contact and clientdetail creation.");
                                }
                            }
                            else
                            {
                                tracingService.Trace("stt_contactid not exists, skipping contact details creation.");
                            }
                        }
                        else
                        {
                            tracingService.Trace("stt_isdirectingclient is true, skipping contact details creation.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in UpdatePostOpTransactionRole plugin: {ex.ToString()}");
                throw new InvalidPluginExecutionException($"Error in UpdatePostOpTransactionRole plugin: {ex.ToString()}");
            }
        }
    }
}

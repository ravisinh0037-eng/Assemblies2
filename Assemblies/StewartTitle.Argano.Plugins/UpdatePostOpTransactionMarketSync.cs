using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace StewartTitle.Argano.Plugins
{
    public class UpdatePostOpTransactionMarketSync : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.Depth > 1)
                {
                    return;
                }

                if (context.MessageName != "Update")
                {
                    return;
                }

                if (!context.InputParameters.Contains("Target"))
                {
                    return;
                }

                Entity target = (Entity)context.InputParameters["Target"];

                if (!target.Contains("stt_marketid"))
                {
                    return;
                }

                EntityReference newMarket = target.GetAttributeValue<EntityReference>("stt_marketid");

                if (newMarket == null)
                {
                    return;
                }

                Guid transactionId = target.Id;

                tracing.Trace("Transaction Market Updated: " + newMarket.Id);

                // Get all Transaction Roles linked with Transaction
                QueryExpression roleQuery = new QueryExpression("stt_transactionrole");
                roleQuery.ColumnSet = new ColumnSet("stt_transactionroleid", "stt_contactid", "stt_clientdetailsid");

                roleQuery.Criteria.AddCondition("stt_transactionid", ConditionOperator.Equal, transactionId);

                EntityCollection roles = service.RetrieveMultiple(roleQuery);

                tracing.Trace("Transaction Roles Found: " + roles.Entities.Count);

                // Loop through each Transaction Role
                foreach (Entity role in roles.Entities)
                {
                    EntityReference roleContact = role.GetAttributeValue<EntityReference>("stt_contactid");

                    EntityReference clientDetailRef = role.GetAttributeValue<EntityReference>("stt_clientdetailsid");

                    if (roleContact == null || clientDetailRef == null)
                    {
                        tracing.Trace("Skipping Role: Contact or ClientDetail missing.");
                        continue;
                    }

                    Entity clientDetail = service.Retrieve("stt_clientdetails", clientDetailRef.Id, new ColumnSet("stt_contactid", "stt_marketid", "stt_name"));

                    // Client Detail Contact
                    EntityReference clientContact = clientDetail.GetAttributeValue<EntityReference>("stt_contactid");
                    string clientName = clientDetail.GetAttributeValue<string>("stt_name");

                    if (clientContact == null)
                    {
                        tracing.Trace("Client Detail Contact missing. Skipping...");
                        continue;
                    }

                    // SContact Match Check
                    if (roleContact.Id != clientContact.Id)
                    {
                        tracing.Trace("Contact mismatch. Skipping update...");
                        continue;
                    }

                    // Step 5: Update Client Detail Market
                    Entity updateClientDetail = new Entity("stt_clientdetails");
                    updateClientDetail.Id = clientDetailRef.Id;
                    updateClientDetail["stt_marketid"] = newMarket;

                    service.Update(updateClientDetail);

                    tracing.Trace($"Client Detail Updated Successfully: {clientName} - ({clientDetailRef.Id})");
                }
            }
            catch (Exception ex)
            {
                tracing.Trace("Plugin Error: " + ex.Message);
                throw new InvalidPluginExecutionException("Market Sync Plugin Failed.", ex);
            }
        }
    }
}
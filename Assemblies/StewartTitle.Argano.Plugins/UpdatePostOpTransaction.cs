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
    /// Change Lifecycle Stage to Client in Client Details Directing Client.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Update" - Primary Entity: "stt_transaction" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    public class UpdatePostOpTransaction : IPlugin
    {
        int clientStage = 924510003;

        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.MessageName.ToLower() != "update" || context.PrimaryEntityName != "stt_transaction")
            {
                tracingService.Trace("The step is not Update, plugin will exit");
                return;
            }

            try
            {
                Entity transactionRef = (Entity)context.InputParameters["Target"];
                Entity transaction = service.Retrieve(transactionRef.LogicalName, transactionRef.Id, new ColumnSet("stt_directingclientdetailsid", "stt_finalcloseon"));
                if (transaction.Contains("stt_directingclientdetailsid") && transaction["stt_directingclientdetailsid"] != null && transaction.Contains("stt_finalcloseon") && transaction["stt_finalcloseon"] != null)
                {
                    tracingService.Trace("Transaction has Directing Client Details or Final Close Date");
                    EntityReference clientDetailsReference = (EntityReference)transaction["stt_directingclientdetailsid"];
                    Guid clientDetailsId = clientDetailsReference.Id;

                    Entity clientDetails = service.Retrieve(clientDetailsReference.LogicalName, clientDetailsId, new ColumnSet("stt_clientstagecode"));

                    if (clientDetails != null)
                    {
                        tracingService.Trace($"Client Details found: {clientDetails.Id}");
                        Entity clientDetailsToUpdate = new Entity(clientDetails.LogicalName, clientDetails.Id);
                        clientDetailsToUpdate["stt_clientstagecode"] = new OptionSetValue(this.clientStage);
                        service.Update(clientDetailsToUpdate);
                    }
                    else
                    {
                        tracingService.Trace("Client Details doesn't found");
                    }
                }
                else
                {
                    tracingService.Trace("Transaction doesn't have Directing Client Details or Final Close Date");
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

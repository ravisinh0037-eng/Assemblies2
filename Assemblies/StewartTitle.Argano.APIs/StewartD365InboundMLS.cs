using Microsoft.Xrm.Sdk;
using StewartTitle.Argano.APIs.Models;
using StewartTitle.Argano.APIs.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StewartTitle.Argano.APIs
{
    public class StewartD365InboundMLS : IPlugin
    {
        private TransactionProcessor transactionProcessor;

        // Limit of the amount of records to be processed. If exceded, the execution is canceled.
        private int recordLimit = 1000;

        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory organizationServiceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = organizationServiceFactory.CreateOrganizationService(context.UserId);

            transactionProcessor = new TransactionProcessor();

            tracingService.Trace("StewartD365InboundMLS API executed.");

            try
            {
                if (context.InputParameters.Keys.Contains("payload"))
                {
                    string partiesString = (string)context.InputParameters["payload"];
                    tracingService.Trace(partiesString);

                    InboundTPSParties parties = JsonSerializer.Deserialize<InboundTPSParties>(partiesString);
                    if (transactionProcessor.CountRecordsToProcess(tracingService, parties) < recordLimit)
                    {
                        tracingService.Trace($"Valid amount of records.");
                        for (int i = 0; i < parties.Parties.Count; i++)
                        {
                            transactionProcessor.ProcessParty(service, tracingService, parties.Parties[i], false, partiesString, context);
                        }
                    }
                    else
                    {
                        tracingService.Trace("The number of records to process was exceded.");
                        context.OutputParameters["ResultCount"] = "The number of records to process was exceded";
                        throw new ArgumentException("The number of records to process was exceded");
                    }
                    tracingService.Trace("Execution ended successfully.");
                    context.OutputParameters["ResultCount"] = "Execution ended successfully.";
                    context.OutputParameters["response"] = "Execution ended successfully.";
                }
                else
                {
                    tracingService.Trace("Expected parameter Parties not found.");
                    context.OutputParameters["ResultCount"] = "Expected parameter Parties not found.";
                    throw new ArgumentException("Parties is a required parameter.");
                }
            }
            catch (Exception ex)
            {
                context.OutputParameters["ResultCount"] = $"Error inStewartD365InboundMLS API: {ex.Message}.";
                throw new InvalidPluginExecutionException($"Error inStewartD365InboundMLS API: {ex.Message}.", ex);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using StewartTitle.Argano.APIs.Models;
using StewartTitle.Argano.APIs.Utils;
using System.Text.Json;

namespace StewartTitle.Argano.APIs
{
    public class ReprocessInboundTPS : IPlugin
    {
        private TransactionProcessor transactionProcessor;
        private int recordLimit = 1000;
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory organizationServiceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = organizationServiceFactory.CreateOrganizationService(context.UserId);

            transactionProcessor = new TransactionProcessor();

            tracingService.Trace("Reprocess Inbound TPS executed.");

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
                {
                    if (targetEntity.LogicalName == "stt_inboundapierrors")
                    {
                        if (targetEntity.Contains("stt_payload") && !String.IsNullOrEmpty(targetEntity.GetAttributeValue<string>("stt_payload")))
                        {
                            string partiesString = targetEntity["stt_payload"].ToString();
                            tracingService.Trace(partiesString);

                            InboundTPSParties parties = JsonSerializer.Deserialize<InboundTPSParties>(partiesString);

                            for (int i = 0; i < parties.Parties.Count; i++)
                            {
                                transactionProcessor.ProcessParty(service, tracingService, parties.Parties[i], true, partiesString, context);
                            }

                            tracingService.Trace("Execution ended successfully.");
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                tracingService.Trace($"Exception: {ex.Message}");
                context.OutputParameters["ResultCount"] = "An error occurred during processing.";
                context.OutputParameters["response"] = $"Error: {ex.Message}";
            }
        }
    }
}

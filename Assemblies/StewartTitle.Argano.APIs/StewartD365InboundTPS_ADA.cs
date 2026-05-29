using Microsoft.Xrm.Sdk;
using StewartTitle.Argano.APIs.Models;
using StewartTitle.Argano.APIs.Utils;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace StewartTitle.Argano.APIs
{
    public class StewartD365InboundTPS_ADA : IPlugin
    {
        private ProductionProcessor productionProcessor;
        private int recordLimit = 1000;

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory organizationServiceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = organizationServiceFactory.CreateOrganizationService(context.UserId);

            productionProcessor = new ProductionProcessor();
            tracingService.Trace("StewartD365InboundTPS_ADA executed.");

            try
            {
                if (context.InputParameters.Keys.Contains("payload"))
                {
                    string payloadString = (string)context.InputParameters["payload"];
                    tracingService.Trace($"Raw payload received: {payloadString}");

                    InboundTPSTransactionPayload payload = null;
                    string trimmed = payloadString.Trim();

                    JObject jsonObj = JObject.Parse(trimmed);

                    if (jsonObj.ContainsKey("transactions"))
                    {
                        tracingService.Trace("Payload is wrapped transactions array.");
                        payload = JsonConvert.DeserializeObject<InboundTPSTransactionPayload>(trimmed);
                    }
                    else if (jsonObj.ContainsKey("transactionID"))
                    {
                        tracingService.Trace("Payload is single transaction object.");
                        var singleTx = JsonConvert.DeserializeObject<InboundTPSTransactionItem>(trimmed);
                        payload = new InboundTPSTransactionPayload
                        {
                            transactions = new List<InboundTPSTransactionItem> { singleTx }
                        };
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException("Payload format not recognized. Expected 'transactions' array or single transaction object.");
                    }

                    if (payload == null || payload.transactions == null || payload.transactions.Count == 0)
                    {
                        throw new InvalidPluginExecutionException("Transaction payload is invalid or empty.");
                    }

                    tracingService.Trace($"Total transactions to process: {payload.transactions.Count}");

                    int totalRecords = CountRecordsToProcess(tracingService, payload);

                    if (totalRecords < recordLimit)
                    {
                        tracingService.Trace($"Valid amount of records. Total: {totalRecords}");

                        foreach (InboundTPSTransactionItem transaction in payload.transactions)
                        {
                            if (transaction.parties == null || transaction.parties.Count == 0)
                            {
                                tracingService.Trace($"Transaction {transaction.transactionID} has no parties. Skipping.");
                                continue;
                            }

                            tracingService.Trace($"Processing Transaction: {transaction.transactionID} with {transaction.parties.Count} party(ies).");

                            productionProcessor.ProcessTransaction(service, tracingService, transaction, false, payloadString, context);
                        }
                    }
                    else
                    {
                        tracingService.Trace("The number of records to process was exceeded.");
                        context.OutputParameters["ResultCount"] = "The number of records to process was exceeded";
                        throw new ArgumentException("The number of records to process was exceeded");
                    }

                    tracingService.Trace("Execution ended successfully.");
                    context.OutputParameters["ResultCount"] = "Execution ended successfully.";
                    context.OutputParameters["response"] = "Execution ended successfully.";
                }
                else
                {
                    tracingService.Trace("Expected parameter payload not found.");
                    context.OutputParameters["ResultCount"] = "Expected parameter payload not found.";
                    throw new ArgumentException("Payload is a required parameter.");
                }
            }
            catch (Exception ex)
            {
                context.OutputParameters["ResultCount"] = $"Error in StewartD365InboundTPS_ADA: {ex.Message}.";
                throw new InvalidPluginExecutionException($"Error in StewartD365InboundTPS_ADA: {ex.Message}.", ex);
            }
        }

        #region Count Records To Process
        private int CountRecordsToProcess(ITracingService tracingService, InboundTPSTransactionPayload payload)
        {
            int count = 0;
            try
            {
                foreach (var transaction in payload.transactions)
                {
                    if (transaction.parties != null)
                    {
                        count += transaction.parties.Count;
                    }
                }
                tracingService.Trace($"Total records to process: {count}");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error counting records: {ex.Message}");
                throw;
            }
            return count;
        }
        #endregion
    }
}
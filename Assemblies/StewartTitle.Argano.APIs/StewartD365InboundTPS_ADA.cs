using Microsoft.Xrm.Sdk;
using StewartTitle.Argano.APIs.Models;
using StewartTitle.Argano.APIs.Utils;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                    #region Extract and Validate JSON Payload
                    string payloadString = (string)context.InputParameters["payload"];

                    tracingService.Trace($"Raw payload received: {payloadString}");

                    try
                    {
                        JObject jsonObj = JObject.Parse(payloadString);
                        foreach (var key in jsonObj.Properties())
                        {
                            tracingService.Trace($"Top-level JSON key found: '{key.Name}'");
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        tracingService.Trace($"JSON parse debug failed: {jsonEx.Message}");
                    }

                    InboundTPSTransactionPayload payload = JsonConvert.DeserializeObject<InboundTPSTransactionPayload>(payloadString);


                    tracingService.Trace($"Deserialized payload is null: {payload == null}");
                    if (payload != null)
                    {
                        tracingService.Trace($"payload.transactions is null: {payload.transactions == null}");
                        if (payload.transactions != null)
                        {
                            tracingService.Trace($"payload.transactions count: {payload.transactions.Count}");
                        }
                    }

                    if (payload == null || payload.transactions == null)
                    {
                        throw new InvalidPluginExecutionException("Transaction payload is invalid.");
                    }

                    tracingService.Trace($"Payload deserialized successfully. Transaction count: {payload.transactions.Count}");
                    #endregion

                    int totalRecords = CountRecordsToProcess(tracingService, payload);

                    if (totalRecords < recordLimit)
                    {
                        tracingService.Trace($"Valid amount of records. Total: {totalRecords}");

                        for (int i = 0; i < payload.transactions.Count; i++)
                        {
                            InboundTPSTransactionItem transaction = payload.transactions[i];

                            if (transaction.parties == null || transaction.parties.Count == 0)
                            {
                                tracingService.Trace($"Transaction {transaction.transactionID} has no parties. Skipping.");
                                continue;
                            }

                            tracingService.Trace($"Processing Transaction: {transaction.transactionID} with {transaction.parties.Count} partie(s).");

                            for (int j = 0; j < transaction.parties.Count; j++)
                            {
                                InboundTPSTransactionParty party = transaction.parties[j];
                                tracingService.Trace($"Processing Party: {party.enterpriseID} under Transaction: {transaction.transactionID}.");

                                productionProcessor.ProcessTransaction(service, tracingService, party, transaction, false, payloadString, context);
                            }
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
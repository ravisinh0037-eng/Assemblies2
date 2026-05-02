namespace StewartTitle.Argano.Plugins
{
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Query;
    using System;
    using System.Collections.Generic;
    using System.Security;
    using System.Text;
    using System.Xml;
    using System.IO;
    using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

    public class UpdatePostTransactionMSA : IPlugin
    {
        ITracingService tracingService = null;
        IOrganizationService service = null;
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                string fetchXml_ResetRecord = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='stt_transactionmsa'>
                                                <attribute name='stt_transactionmsaid' />
                                                <attribute name='stt_name' />
                                                <attribute name='stt_msaid' />
                                                <order attribute='createdon' descending='true' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0'/>
                                                  <filter type='or'>
                                                    <condition attribute='stt_includeinnonstewartproductioncount' operator='eq' value='1' />
                                                    <condition attribute='stt_includeinstewartproductioncount' operator='eq' value='1' />
                                                  </filter>
                                                </filter>
                                                <link-entity name='stt_transaction' from='stt_transactionid' to='stt_transactionid' link-type='inner' >
                                                  <filter type='and'>
                                                    <condition attribute='stt_finalcloseon' operator='olderthan-x-days' value='365' />
		                                            <condition attribute='statecode' operator='eq' value='0' />
                                                  </filter>
                                                </link-entity>
                                              </entity>
                                            </fetch>";

                List<Entity> listTransactionMsaResetRecord = GetRecordsByFetchXml(service, fetchXml_ResetRecord);
                tracingService.Trace("listTransactionMsaResetRecord.Count = " + listTransactionMsaResetRecord.Count);
                
                BulkUpdateTransactionMSA(listTransactionMsaResetRecord, "resetrecord");

                String fetchXml_Includeinstewartproduction = @"<fetch version='1.0' mapping='logical' distinct='true'>
                                    <entity name='stt_transactionmsa'>
                                        <attribute name='statecode'/>
                                        <attribute name='stt_transactionmsaid'/>
                                        <attribute name='stt_name'/>
                                        <attribute name='createdon'/>
                                        <filter type='and'>
                                        <condition attribute='statecode' operator='eq' value='0'/>
                                        <filter type='or'>
                                        <condition attribute='stt_includeinnonstewartproductioncount' operator='eq' value='1' />
                                        <condition attribute='stt_includeinstewartproductioncount' operator='eq' value='0' />
                                        </filter>
                                        </filter>
                                        <link-entity name='stt_transaction' to='stt_transactionid' from='stt_transactionid' link-type='inner'>
                                        <filter type='and'>
                                        <condition attribute='stt_brandid' operator='not-null'/>
                                        <condition attribute='stt_finalcloseon' operator='last-x-days' value='365'/>
                                        <condition attribute='statecode' operator='eq' value='0' />
                                        </filter>
                                        </link-entity>
                                        <order attribute='createdon' descending='true'/>
                                       </entity>
                                    </fetch>";
                List<Entity> listTransactionMsaIncludeinstewartproduction = GetRecordsByFetchXml(service, fetchXml_Includeinstewartproduction);

                tracingService.Trace("listTransactionMsaIncludeinstewartproduction.Count = " + listTransactionMsaIncludeinstewartproduction.Count);

                BulkUpdateTransactionMSA(listTransactionMsaIncludeinstewartproduction, "includestewartproductioncount");
                //---------------------------------------------------------------------------------------------------------------

                String fetchXml_IncludeinNonstewartproduction = @"<fetch version='1.0' mapping='logical' distinct='true'>
                                            <entity name='stt_transactionmsa'>
                                            <attribute name='statecode'/>
                                            <attribute name='stt_transactionmsaid'/>
                                            <attribute name='stt_name'/>
                                            <attribute name='createdon'/>
                                            <filter type='and'>
                                            <condition attribute='statecode' operator='eq' value='0'/>
                                            <filter type='or'>
                                            <condition attribute='stt_includeinnonstewartproductioncount' operator='eq' value='0' />
                                            <condition attribute='stt_includeinstewartproductioncount' operator='eq' value='1' />
                                            </filter>
                                            </filter>
                                            <link-entity name='stt_transaction' to='stt_transactionid' from='stt_transactionid' link-type='inner'>
                                            <filter type='and'>
                                            <condition attribute='stt_brandid' operator='null'/>
                                            <condition attribute='stt_finalcloseon' operator='last-x-days' value='365'/>
                                            <condition attribute='statecode' operator='eq' value='0' />
                                            </filter>
                                            </link-entity>
                                            <order attribute='createdon' descending='true'/>
                                            </entity>
                                            </fetch>";
                List<Entity> listTransactionMsaIncludeinNonstewartproduction = GetRecordsByFetchXml(service, fetchXml_IncludeinNonstewartproduction);
                tracingService.Trace("listTransactionMsaIncludeinNonstewartproduction.Count = " + listTransactionMsaIncludeinNonstewartproduction.Count);

                BulkUpdateTransactionMSA(listTransactionMsaIncludeinNonstewartproduction, "includenonstewartproductioncount");
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error in UpdatePostTransactionMSA: " + ex.ToString());
                throw new InvalidPluginExecutionException("An error occurred in UpdatePostTransactionMSA: " + ex.Message);
            }
        }
        public void BulkUpdateTransactionMSA(List<Entity> listTransactionMSA, string stewartproductioncount)
        {
            int chunkSize = 1000;
            int total = listTransactionMSA.Count;
            int processed = 0;
            while (processed < total)
            {
                // Take the next chunk
                var chunk = listTransactionMSA.GetRange(processed, Math.Min(chunkSize, total - processed));
                // Create a collection of update requests
                var multipleRequest = new ExecuteMultipleRequest()
                {
                    Requests = new OrganizationRequestCollection(),
                    Settings = new ExecuteMultipleSettings()
                    {
                        ContinueOnError = true,    // continue even if one fails
                        ReturnResponses = true     // get individual responses
                    }
                };

                // Example: update 100 accounts
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity transactionMSA = chunk[i];
                    var updateEntity = new Entity(transactionMSA.LogicalName, transactionMSA.Id);
                    if (stewartproductioncount.Equals("resetrecord"))
                    {
                        updateEntity["stt_includeinstewartproductioncount"] = false;
                        updateEntity["stt_includeinnonstewartproductioncount"] = false;
                    }
                    if (stewartproductioncount.Equals("includestewartproductioncount"))
                    {
                        updateEntity["stt_includeinstewartproductioncount"] = true;
                        updateEntity["stt_includeinnonstewartproductioncount"] = false;
                    }
                    else if (stewartproductioncount.Equals("includenonstewartproductioncount"))
                    {
                        updateEntity["stt_includeinstewartproductioncount"] = false;
                        updateEntity["stt_includeinnonstewartproductioncount"] = true;
                    }

                    var updateRequest = new UpdateRequest { Target = updateEntity };
                    multipleRequest.Requests.Add(updateRequest);
                }

                // Execute in bulk
                var response = (ExecuteMultipleResponse)service.Execute(multipleRequest);

                // Check for errors
                foreach (var r in response.Responses)
                {
                    if (r.Fault != null)
                    {
                        tracingService.Trace($"Error updating record: {r.Fault.Message}");
                    }
                }
                processed += chunk.Count;
                tracingService.Trace($"Updated {processed} of {total} records...");
            }
        }
        public List<Entity> GetRecordsByFetchXml(IOrganizationService service, String fetchXML)
        {
            List<Entity> objList = new List<Entity>();
            try
            {
                // Define the fetch attributes.
                // Set the number of records per page to retrieve.
                int fetchCount = 5000;
                // Initialize the page number.
                int pageNumber = 1;
                // Specify the current paging cookie. For retrieving the first page,
                // pagingCookie should be null.
                String pagingCookie = null;
                while (true)
                {
                    // Build fetchXml String with the placeholders.
                    String xml = CreateXml(fetchXML, pagingCookie, pageNumber, fetchCount);
                    // Excute the fetch query and get the xml result.
                    RetrieveMultipleRequest fetchRequest1 = new RetrieveMultipleRequest
                    {
                        Query = new FetchExpression(xml)
                    };
                    EntityCollection returnCollection = ((RetrieveMultipleResponse)service.Execute(fetchRequest1)).EntityCollection;
                    foreach (var entity in returnCollection.Entities)
                    {
                        objList.Add(entity);
                    }
                    // Check for morerecords, if it returns 1.
                    if (returnCollection.MoreRecords)
                    {
                        // Increment the page number to retrieve the next page.
                        pageNumber++;
                    }
                    else
                    {
                        // If no more records in the result nodes, exit the loop.
                        return objList;
                        //  break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return objList;
        }
        public String CreateXml(String xml, String cookie, int page, int count)
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                StringReader StringReader = new StringReader(xml);
                XmlTextReader reader = new XmlTextReader(StringReader);
                // Load document
                doc.Load(reader);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return CreateXml(doc, cookie, page, count);
        }
        public String CreateXml(XmlDocument doc, String cookie, int page, int count)
        {
            try
            {
                XmlAttributeCollection attrs = doc.DocumentElement.Attributes;
                if (cookie != null)
                {
                    XmlAttribute pagingAttr = doc.CreateAttribute("paging-cookie");
                    pagingAttr.Value = cookie;
                    attrs.Append(pagingAttr);
                }
                XmlAttribute pageAttr = doc.CreateAttribute("page");
                pageAttr.Value = System.Convert.ToString(page);
                attrs.Append(pageAttr);
                XmlAttribute countAttr = doc.CreateAttribute("count");
                countAttr.Value = System.Convert.ToString(count);
                attrs.Append(countAttr);
                StringBuilder sb = new StringBuilder(1024);
                StringWriter StringWriter = new StringWriter(sb);
                XmlTextWriter writer = new XmlTextWriter(StringWriter);
                doc.WriteTo(writer);
                writer.Close();
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return String.Empty;
        }

    }
}

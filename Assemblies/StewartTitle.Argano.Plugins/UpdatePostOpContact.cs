// <copyright file="UpdatePostOpContact.cs" company="Stewart">
// Copyright 2025 Stewart
// </copyright>

namespace StewartTitle.Argano.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Permissions;
    using System.Text;
    using System.Threading.Tasks;
    using global::Argano.Utils;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using StewartTitle.Argano.Plugins.Models;
    using StewartTitle.Argano.Plugins.Utils;
    using static StewartTitle.Argano.Plugins.Models.ContactUpdatePayload;

    /// <summary>
    /// On the Contact Updatewe send that update to Stewart's API.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Update" - Primary Entity: "contact" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    public class UpdatePostOpContact : IPlugin
    {
        private string DataverseOrgUrl;
        private string AzureAdTenantId;
        private string ClientId;
        private string ClientSecret;
        private string[] Scopes;
        private string Authority;
        private static string CachedAccessToken = null;
        private static DateTime CachedAccessTokenExpiresOn = DateTime.MinValue;
        private static readonly object TokenLock = new object();
        private ITracingService tracingService = null;
        private IOrganizationService service = null;
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace("Execution started.");

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
                {
                    if (targetEntity.LogicalName == "contact")
                    {
                        tracingService.Trace("Contact found.");
                        Entity contactEntity = (Entity)context.InputParameters["Target"];
                        Guid contactId = targetEntity.Id;
                        tracingService.Trace($"Contact ID: {contactId}");

                        // Retrieve contact to update
                        tracingService.Trace("Searching contact...");
                        ColumnSet existingColumnSet = new ColumnSet("stt_enterpriseid", "stt_contactfinanceidtext", "stt_countyid", "firstname", "lastname", "emailaddress1", "telephone1", "address1_line1", "address1_city", "address1_stateorprovince", "address1_postalcode", "parentcustomerid", "address1_line2", "jobtitle", "stt_stateid", "stt_zipcodeid", "statecode");
                        Entity contactToUpdate = service.Retrieve("contact", contactId, existingColumnSet);
                        tracingService.Trace($"Contact retrieved: {contactToUpdate.Id}");
                        if (!contactToUpdate.ContainsData("stt_enterpriseid"))
                        {
                            tracingService.Trace("Enterprise ID is null, plugin will exit");
                            return;
                        }

                        if (contactToUpdate.ContainsData("stt_contactfinanceidtext"))
                        {
                            tracingService.Trace($"Contact Finance ID: {contactToUpdate.GetAttributeValue<string>("stt_contactfinanceidtext")}");
                        }
                        string apiUrl = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_outboundContactAPI_URL");
                        /*  Entity accountToUpdate = null;

                          // Retrieve account from contact to update
                          if (contactToUpdate.ContainsData("parentcustomerid"))
                          {
                              tracingService.Trace("Searching account...");
                              EntityReference accountRef = contactToUpdate.GetAttributeValue<EntityReference>("parentcustomerid");
                              ColumnSet accountColumnSet = new ColumnSet("name", "address1_line1", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "statecode", "stt_stateid", "stt_zipcodeid");
                              accountToUpdate = service.Retrieve("account", accountRef.Id, accountColumnSet);
                          }

                          if (contactToUpdate != null)
                          {
                              // Retrieve the API URL from Environment Variable
                              tracingService.Trace("Retrieving API URL...");
                              string apiUrl = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_outboundContactAPI_URL");

                              // Build KnownEmailAddresses list
                              var knownEmails = new List<ContactUpdatePayload.KnownEmailAddress>();
                              knownEmails = this.GetKnownEmails(service, tracingService, contactId);

                              var accountPayload = new AccountPayload();

                              // Build Account payload
                              if (accountToUpdate != null)
                              {
                                  tracingService.Trace("Setting name.");
                                  accountPayload = new AccountPayload
                                  {
                                      name = accountToUpdate.GetAttributeValue<string>("name"),
                                  };
                                  tracingService.Trace("Setting address line 1.");
                                  if (accountToUpdate.Contains("address1_line1"))
                                  {
                                      accountPayload.address1_line1 = accountToUpdate.GetAttributeValue<string>("address1_line1");
                                  }

                                  tracingService.Trace("Setting city.");
                                  if (accountToUpdate.Contains("address1_city"))
                                  {
                                      accountPayload.address1_city = accountToUpdate.GetAttributeValue<string>("address1_city");
                                  }

                                  tracingService.Trace("Setting state.");
                                  if (accountToUpdate.ContainsData("stt_stateid"))
                                  {
                                      QueryExpression stateQuery = new QueryExpression("stt_state");
                                      stateQuery.ColumnSet = new ColumnSet("stt_abbreviation", "stt_name");
                                      stateQuery.Criteria.AddCondition("stt_stateid", ConditionOperator.Equal, accountToUpdate.GetAttributeValue<EntityReference>("stt_stateid").Id);
                                      EntityCollection sequenceEntities = service.RetrieveMultiple(stateQuery);
                                      if (sequenceEntities != null && sequenceEntities.Entities.Count > 0)
                                      {
                                          accountPayload.address1_stateorprovince = sequenceEntities.Entities[0].GetAttributeValue<string>("stt_abbreviation");
                                      }
                                  }

                                  tracingService.Trace("Setting county.");
                                  if (accountToUpdate.Contains("stt_countyid"))
                                  {
                                      accountPayload.address1_country = accountToUpdate.GetAttributeValue<EntityReference>("stt_countyid").Name;
                                  }

                                  tracingService.Trace("Setting zip.");
                                  if (accountToUpdate.Contains("stt_zipcodeid"))
                                  {
                                      accountPayload.address1_postalcode = accountToUpdate.GetAttributeValue<EntityReference>("stt_zipcodeid").Name;
                                  }
                              }

                              // Build Contact payload
                              var contactPayload = new ContactUpdatePayload.ContactPayload
                              {
                                  KnownEmailAddresses = knownEmails,
                                  Account = accountPayload,
                              };

                              tracingService.Trace("Setting email.");
                              if (contactToUpdate.ContainsData("emailaddress1"))
                              {
                                  contactPayload.emailaddress1 = contactToUpdate.GetAttributeValue<string>("emailaddress1");
                              }

                              tracingService.Trace("Setting enterprise ID.");
                              if (contactToUpdate.ContainsData("stt_enterpriseid"))
                              {
                                  contactPayload.stt_enterpriseid = contactToUpdate.GetAttributeValue<string>("stt_enterpriseid");
                              }

                              tracingService.Trace("Setting first name.");
                              if (contactToUpdate.ContainsData("firstname"))
                              {
                                  contactPayload.firstname = contactToUpdate.GetAttributeValue<string>("firstname");
                              }

                              tracingService.Trace("Setting last name.");
                              if (contactToUpdate.ContainsData("lastname"))
                              {
                                  contactPayload.lastname = contactToUpdate.GetAttributeValue<string>("lastname");
                              }

                              tracingService.Trace("Setting telephone.");
                              if (contactToUpdate.ContainsData("telephone1"))
                              {
                                  contactPayload.telephone1 = contactToUpdate.GetAttributeValue<string>("telephone1");
                              }

                              tracingService.Trace("Setting email.");
                              if (contactToUpdate.ContainsData("address1_line1"))
                              {
                                  contactPayload.address1_line1 = contactToUpdate.GetAttributeValue<string>("address1_line1");
                              }

                              tracingService.Trace("Setting address line 2.");
                              if (contactToUpdate.ContainsData("address1_line2"))
                              {
                                  contactPayload.address1_line2 = contactToUpdate.GetAttributeValue<string>("address1_line2");
                              }

                              tracingService.Trace("Setting city.");
                              if (contactToUpdate.ContainsData("address1_city"))
                              {
                                  contactPayload.address1_city = contactToUpdate.GetAttributeValue<string>("address1_city");
                              }

                              tracingService.Trace("Setting state.");
                              if (contactToUpdate.ContainsData("stt_stateid"))
                              {
                                  QueryExpression stateQuery = new QueryExpression("stt_state");
                                  stateQuery.ColumnSet = new ColumnSet("stt_abbreviation", "stt_name");
                                  stateQuery.Criteria.AddCondition("stt_stateid", ConditionOperator.Equal, contactToUpdate.GetAttributeValue<EntityReference>("stt_stateid").Id);
                                  EntityCollection sequenceEntities = service.RetrieveMultiple(stateQuery);
                                  if (sequenceEntities != null && sequenceEntities.Entities.Count > 0)
                                  {
                                      contactPayload.address1_stateorprovince = sequenceEntities.Entities[0].GetAttributeValue<string>("stt_abbreviation");
                                  }
                              }

                              tracingService.Trace("Setting zip.");
                              if (contactToUpdate.ContainsData("stt_zipcodeid"))
                              {
                                  contactPayload.address1_postalcode = contactToUpdate.GetAttributeValue<EntityReference>("stt_zipcodeid").Name;
                              }

                              tracingService.Trace("Setting job.");
                              if (contactToUpdate.ContainsData("jobtitle"))
                              {
                                  contactPayload.jobtitle = contactToUpdate.GetAttributeValue<string>("jobtitle");
                              }

                              tracingService.Trace("Setting county.");
                              if (contactToUpdate.ContainsData("stt_countyid"))
                              {
                                  contactPayload.address1_county = contactToUpdate.GetAttributeValue<EntityReference>("stt_countyid").Name;
                              }

                              tracingService.Trace("Setting contact finance id.");
                              if (contactToUpdate.ContainsData("stt_contactfinanceidtext"))
                              {
                                  contactPayload.stt_uuid = contactToUpdate.GetAttributeValue<string>("stt_contactfinanceidtext");
                              }

                              tracingService.Trace("Setting statecode.");
                              if (contactToUpdate.ContainsData("statecode"))
                              {
                                  contactPayload.statecode = contactToUpdate.GetAttributeValue<OptionSetValue>("statecode").Value;
                              }

                              var root = new ContactUpdatePayload
                              {
                                  contacts = new List<ContactUpdatePayload.ContactPayload> { contactPayload },
                              }; */
                        var root = GetPayLoad(contactToUpdate, contactId);
                        string jsonPayload = JsonSerializerHelper.JsonSerializer(root);

                        tracingService.Trace("Payload: " + jsonPayload);

                        this.SendPayload(apiUrl, tracingService, jsonPayload);
                    }
                    else
                    {
                        if (targetEntity.LogicalName == "stt_address")
                        {
                            tracingService.Trace("Address found.");
                            string apiUrl = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_outboundContactAPI_URL");

                            tracingService.Trace("Searching for contact ID.");
                            Guid contactId = targetEntity.GetAttributeValue<EntityReference>("stt_contactid").Id;

                            if (contactId == Guid.Empty)
                            {
                                tracingService.Trace("Address with no contact ID.");
                                return;
                            }

                            //Entity contact = service.Retrieve("contact", contactId, new ColumnSet("stt_contactfinanceidtext", "stt_enterpriseid"));

                            Entity contact = service.Retrieve("contact", contactId, new ColumnSet("stt_enterpriseid", "stt_contactfinanceidtext", "stt_countyid", "firstname", "lastname", "emailaddress1", "telephone1", "address1_line1", "address1_city", "address1_stateorprovince", "address1_postalcode", "parentcustomerid", "address1_line2", "jobtitle", "stt_stateid", "stt_zipcodeid", "statecode"));

                            if (contact == null)
                            {
                                tracingService.Trace("Contact not found.");
                                return;
                            }

                            // Build KnownEmailAddresses list
                            /*  var knownEmails = new List<ContactUpdatePayload.KnownEmailAddress>();
                              knownEmails = this.GetKnownEmails(service, tracingService, contactId);

                              // Build Contact payload
                              var contactPayload = new ContactUpdatePayload.ContactPayload
                              {
                                  KnownEmailAddresses = knownEmails,
                                  stt_uuid = contact.GetAttributeValue<string>("stt_contactfinanceidtext"),
                                  stt_enterpriseid = contact.GetAttributeValue<string>("stt_enterpriseid"),
                              };

                              var root = new ContactUpdatePayload
                              {
                                  contacts = new List<ContactUpdatePayload.ContactPayload> { contactPayload },
                              }; */

                            var root = GetPayLoad(contact, contactId);

                            string jsonPayload = JsonSerializerHelper.JsonSerializer(root);

                            tracingService.Trace("Payload: " + jsonPayload);

                            this.SendPayload(apiUrl, tracingService, jsonPayload);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in UpdatePostOpContact plugin: {ex}");
                throw new InvalidPluginExecutionException($"Error in UpdatePostOpContact plugin: {ex}");
            }
        }
        private ContactUpdatePayload GetPayLoad(Entity contactToUpdate, Guid contactId)
        {
            tracingService.Trace("inside GetPayLoad: ");
            Entity accountToUpdate = null;
            ContactUpdatePayload root = new ContactUpdatePayload();
            // Retrieve account from contact to update
            if (contactToUpdate.ContainsData("parentcustomerid"))
            {
                tracingService.Trace("Searching account...");
                EntityReference accountRef = contactToUpdate.GetAttributeValue<EntityReference>("parentcustomerid");
                ColumnSet accountColumnSet = new ColumnSet("name", "address1_line1", "address1_city", "address1_stateorprovince", "address1_country", "address1_postalcode", "statecode", "stt_stateid", "stt_zipcodeid");
                accountToUpdate = service.Retrieve("account", accountRef.Id, accountColumnSet);
            }

            if (contactToUpdate != null)
            {
                // Retrieve the API URL from Environment Variable
                tracingService.Trace("Retrieving API URL...");
                string apiUrl = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_outboundContactAPI_URL");

                // Build KnownEmailAddresses list
                var knownEmails = new List<ContactUpdatePayload.KnownEmailAddress>();
                knownEmails = this.GetKnownEmails(service, tracingService, contactId);

                var accountPayload = new AccountPayload();

                // Build Account payload
                if (accountToUpdate != null)
                {
                    tracingService.Trace("Setting name.");
                    accountPayload = new AccountPayload
                    {
                        name = accountToUpdate.GetAttributeValue<string>("name"),
                    };
                    tracingService.Trace("Setting address line 1.");
                    if (accountToUpdate.Contains("address1_line1"))
                    {
                        accountPayload.address1_line1 = accountToUpdate.GetAttributeValue<string>("address1_line1");
                    }

                    tracingService.Trace("Setting city.");
                    if (accountToUpdate.Contains("address1_city"))
                    {
                        accountPayload.address1_city = accountToUpdate.GetAttributeValue<string>("address1_city");
                    }

                    tracingService.Trace("Setting state.");
                    if (accountToUpdate.ContainsData("stt_stateid"))
                    {
                        QueryExpression stateQuery = new QueryExpression("stt_state");
                        stateQuery.ColumnSet = new ColumnSet("stt_abbreviation", "stt_name");
                        stateQuery.Criteria.AddCondition("stt_stateid", ConditionOperator.Equal, accountToUpdate.GetAttributeValue<EntityReference>("stt_stateid").Id);
                        EntityCollection sequenceEntities = service.RetrieveMultiple(stateQuery);
                        if (sequenceEntities != null && sequenceEntities.Entities.Count > 0)
                        {
                            accountPayload.address1_stateorprovince = sequenceEntities.Entities[0].GetAttributeValue<string>("stt_abbreviation");
                        }
                    }

                    tracingService.Trace("Setting county.");
                    if (accountToUpdate.Contains("stt_countyid"))
                    {
                        accountPayload.address1_country = accountToUpdate.GetAttributeValue<EntityReference>("stt_countyid").Name;
                    }

                    tracingService.Trace("Setting zip.");
                    if (accountToUpdate.Contains("stt_zipcodeid"))
                    {
                        accountPayload.address1_postalcode = accountToUpdate.GetAttributeValue<EntityReference>("stt_zipcodeid").Name;
                    }
                }

                // Build Contact payload
                var contactPayload = new ContactUpdatePayload.ContactPayload
                {
                    KnownEmailAddresses = knownEmails,
                    Account = accountPayload,
                };

                tracingService.Trace("Setting email.");
                if (contactToUpdate.ContainsData("emailaddress1"))
                {
                    contactPayload.emailaddress1 = contactToUpdate.GetAttributeValue<string>("emailaddress1");
                }

                tracingService.Trace("Setting enterprise ID.");
                if (contactToUpdate.ContainsData("stt_enterpriseid"))
                {
                    contactPayload.stt_enterpriseid = contactToUpdate.GetAttributeValue<string>("stt_enterpriseid");
                }

                tracingService.Trace("Setting first name.");
                if (contactToUpdate.ContainsData("firstname"))
                {
                    contactPayload.firstname = contactToUpdate.GetAttributeValue<string>("firstname");
                }

                tracingService.Trace("Setting last name.");
                if (contactToUpdate.ContainsData("lastname"))
                {
                    contactPayload.lastname = contactToUpdate.GetAttributeValue<string>("lastname");
                }

                tracingService.Trace("Setting telephone.");
                if (contactToUpdate.ContainsData("telephone1"))
                {
                    contactPayload.telephone1 = contactToUpdate.GetAttributeValue<string>("telephone1");
                }

                tracingService.Trace("Setting email.");
                if (contactToUpdate.ContainsData("address1_line1"))
                {
                    contactPayload.address1_line1 = contactToUpdate.GetAttributeValue<string>("address1_line1");
                }

                tracingService.Trace("Setting address line 2.");
                if (contactToUpdate.ContainsData("address1_line2"))
                {
                    contactPayload.address1_line2 = contactToUpdate.GetAttributeValue<string>("address1_line2");
                }

                tracingService.Trace("Setting city.");
                if (contactToUpdate.ContainsData("address1_city"))
                {
                    contactPayload.address1_city = contactToUpdate.GetAttributeValue<string>("address1_city");
                }

                tracingService.Trace("Setting state.");
                if (contactToUpdate.ContainsData("stt_stateid"))
                {
                    QueryExpression stateQuery = new QueryExpression("stt_state");
                    stateQuery.ColumnSet = new ColumnSet("stt_abbreviation", "stt_name");
                    stateQuery.Criteria.AddCondition("stt_stateid", ConditionOperator.Equal, contactToUpdate.GetAttributeValue<EntityReference>("stt_stateid").Id);
                    EntityCollection sequenceEntities = service.RetrieveMultiple(stateQuery);
                    if (sequenceEntities != null && sequenceEntities.Entities.Count > 0)
                    {
                        contactPayload.address1_stateorprovince = sequenceEntities.Entities[0].GetAttributeValue<string>("stt_abbreviation");
                    }
                }

                tracingService.Trace("Setting zip.");
                if (contactToUpdate.ContainsData("stt_zipcodeid"))
                {
                    contactPayload.address1_postalcode = contactToUpdate.GetAttributeValue<EntityReference>("stt_zipcodeid").Name;
                }

                tracingService.Trace("Setting job.");
                if (contactToUpdate.ContainsData("jobtitle"))
                {
                    contactPayload.jobtitle = contactToUpdate.GetAttributeValue<string>("jobtitle");
                }

                tracingService.Trace("Setting county.");
                if (contactToUpdate.ContainsData("stt_countyid"))
                {
                    contactPayload.address1_county = contactToUpdate.GetAttributeValue<EntityReference>("stt_countyid").Name;
                }

                tracingService.Trace("Setting contact finance id.");
                if (contactToUpdate.ContainsData("stt_contactfinanceidtext"))
                {
                    contactPayload.stt_uuid = contactToUpdate.GetAttributeValue<string>("stt_contactfinanceidtext");
                }

                tracingService.Trace("Setting statecode.");
                if (contactToUpdate.ContainsData("statecode"))
                {
                    contactPayload.statecode = contactToUpdate.GetAttributeValue<OptionSetValue>("statecode").Value;
                }

                root = new ContactUpdatePayload
                {
                    contacts = new List<ContactUpdatePayload.ContactPayload> { contactPayload },
                };
                return root;
            }
            return root;
        }
        private void SendPayload(string apiUrl, ITracingService tracingService, string content)
        {
            tracingService.Trace("Entered in the send payload method");
            using (HttpClient client = new HttpClient())
            {
                if (apiUrl == null)
                {
                    tracingService.Trace("Environment Variable for apiUrl not defined.");
                    return;
                }

                // Expecting: url,apiKey,DataverseOrgUrl,AzureAdTenantId,ClientId,ClientSecret
                string[] parts = apiUrl.Split(',');
                string url = string.Empty;
                string apiKey = string.Empty;

                if (parts.Length >= 6)
                {
                    url = parts[0];
                    apiKey = parts[1];
                    this.DataverseOrgUrl = parts[2];
                    this.AzureAdTenantId = parts[3];
                    this.ClientId = parts[4];
                    this.ClientSecret = parts[5];
                }
                else
                {
                    throw new Exception("apiUrl does not contain enough parameters for authentication.");
                }

                this.Scopes = new string[] { $"{this.DataverseOrgUrl}/.default" };
                this.Authority = $"https://login.microsoftonline.com/{this.AzureAdTenantId}";

                tracingService.Trace($"API URL: {url}");

                // Creating HTTP Request MSG
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Post,
                };
                request.Content = new StringContent(content, Encoding.UTF8, "application/json");
                request.Headers.Clear();

                // Get Authentication token (Bearer)
                string bearerToken = this.GetAuthTokenAsync(tracingService).GetAwaiter().GetResult();

                tracingService.Trace($"Bearer Token: {bearerToken}");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                // Add Ocp-Apim-Subscription-Key header
                request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

                HttpResponseMessage response = client.SendAsync(request).Result;
                string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                tracingService.Trace("Sending Payload...");
                tracingService.Trace($"API Response: {responseContent}");
                if (!response.IsSuccessStatusCode)
                {
                    tracingService.Trace($"API Request failed with status code: {response.StatusCode}");
                    string errorContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    tracingService.Trace($"API Error Content: {errorContent}");
                    throw new Exception();
                }
            }
        }

        private async Task<string> GetAuthTokenAsync(ITracingService tracingService)
        {
            tracingService.Trace("Entered GetAuthTokenAsync");

            lock (TokenLock)
            {
                if (!string.IsNullOrEmpty(CachedAccessToken) && CachedAccessTokenExpiresOn > DateTime.UtcNow.AddMinutes(2))
                {
                    tracingService.Trace("Using cached access token.");
                    return CachedAccessToken;
                }
            }

            try
            {
                using (var client = new HttpClient())
                {
                    var parameters = new Dictionary<string, string>
                        {
                            { "grant_type", "client_credentials" },
                            { "client_id", this.ClientId },
                            { "client_secret", this.ClientSecret },
                            { "scope", this.Scopes[0] }, // Adjust if you're using multiple scopes
                        };

                    tracingService.Trace("Sending request to acquire token...");

                    var request = new HttpRequestMessage(HttpMethod.Post, this.Authority + "/oauth2/v2.0/token")
                    {
                        Content = new FormUrlEncodedContent(parameters)
                    };

                    var response = await client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        tracingService.Trace($"Token request failed. Status: {response.StatusCode}, Response: {content}");
                        return string.Empty;
                    }

                    var tokenResponse = JsonSerializerHelper.JsonDeserialize<TokenResponse>(content);
                    if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.access_token))
                    {
                        lock (TokenLock)
                        {
                            CachedAccessToken = tokenResponse.access_token;
                            CachedAccessTokenExpiresOn = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);
                        }

                        tracingService.Trace("Token successfully acquired.");
                        return tokenResponse.access_token;
                    }

                    tracingService.Trace("Token response was null or missing access_token.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Token acquisition exception: {ex.Message}");
                return string.Empty;
            }
        }

        private List<ContactUpdatePayload.KnownEmailAddress> GetKnownEmails(IOrganizationService service, ITracingService tracingService, Guid contactId)
        {
            tracingService.Trace("Retrieving known email addresses...");

            // Query to retrieve stt_address records
            QueryExpression query = new QueryExpression("stt_address")
            {
                ColumnSet = new ColumnSet("stt_emailaddress"), // Only retrieve the email address field
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("stt_contactid", ConditionOperator.Equal, contactId), // Match the contact
                        new ConditionExpression("stt_addresstypecode", ConditionOperator.Equal, 924510000), // Type = Email (adjust the value if needed)
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0), // Status = Active (adjust the value if needed)
                    },
                },
            };

            // Execute the query
            EntityCollection emailAddresses = service.RetrieveMultiple(query);

            // Populate the knownEmails list
            List<ContactUpdatePayload.KnownEmailAddress> knownEmails = new List<ContactUpdatePayload.KnownEmailAddress>();
            foreach (Entity email in emailAddresses.Entities)
            {
                string emailAddress = email.GetAttributeValue<string>("stt_emailaddress");
                if (!string.IsNullOrEmpty(emailAddress))
                {
                    knownEmails.Add(new ContactUpdatePayload.KnownEmailAddress { stt_emailaddress = emailAddress });
                }
            }

            tracingService.Trace($"Retrieved {knownEmails.Count} known email addresses.");
            return knownEmails;
        }
    }
}

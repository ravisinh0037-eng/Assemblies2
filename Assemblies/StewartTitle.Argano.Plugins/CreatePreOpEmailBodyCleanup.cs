using Microsoft.Xrm.Sdk;
using System;
using System.ServiceModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StewartTitle.Argano.Plugins.Models;
using Microsoft.Xrm.Sdk.Query;
using System.Text.RegularExpressions;

namespace StewartTitle.Argano.Plugins
{
    public class CreatePreOpEmailBodyCleanup : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin - Start");

            try
            {
                if (context.Depth > 1)
                {
                    tracingService.Trace($"Depth: {context.Depth}");
                    return;
                }
                tracingService.Trace($"Depth: {context.Depth}");

                Entity emailEntity = null;
                bool updateRequired = false;

                // Create handler
                if (context.MessageName == "Create" || context.MessageName == "Update")
                {
                    if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity email)
                    {
                        tracingService.Trace("Target email found for Create.");
                        if (context.MessageName == "Update")
                        {
                            emailEntity = service.Retrieve(
                        Emails.LogicalName,
                        email.Id,
                        new ColumnSet(Emails.Description, Emails.Status)
                        );
                        }
                        else
                        {
                            emailEntity = email;

                        }
                        updateRequired = false;
                    }
                }
                // DeliverIncoming / DeliverPromote handler
                else if (context.MessageName == "DeliverIncoming" || context.MessageName == "DeliverPromote")
                {
                    tracingService.Trace("Loading email using PrimaryEntityId for DeliverIncoming/DeliverPromote.");
                    Guid emailId = context.PrimaryEntityId;

                    if (emailId == Guid.Empty)
                    {
                        tracingService.Trace($"emailID from context primary entity is 00000000-0000-0000-0000-000000000000 {emailId}");
                        return;
                    }

                    tracingService.Trace($"EmailId: {emailId}");
                    emailEntity = service.Retrieve(
                    Emails.LogicalName,
                    emailId,
                    new ColumnSet(Emails.Description, Emails.Status)
                    );

                    updateRequired = true;
                }

                if (emailEntity == null)
                {
                    tracingService.Trace("Email entity not found. Plugin exiting.");
                    return;
                }

                tracingService.Trace($"Processing entity: {emailEntity.LogicalName}");
                var emailStatus = emailEntity.GetAttributeValue<OptionSetValue>(Emails.Status);
                tracingService.Trace($"emailStatus{emailStatus.Value}");
                if (emailStatus == null || (
                    emailStatus.Value != 3 &&
                    emailStatus.Value != 2 &&
                    emailStatus.Value != 4)
                    )
                {
                    tracingService.Trace("the email status is either not present or not sent/received/completed.");
                    return;
                }

                // Clean description
                Implementation(emailEntity, tracingService);

                // If retrieving the email from DB, update it
                if (updateRequired)
                {
                    tracingService.Trace("Updating email description...");
                    service.Update(emailEntity);
                }

                //Delete inline image attachments (reduces storage)
                if (context.PrimaryEntityId != Guid.Empty)
                {
                    tracingService.Trace("Deleting inline image attachments...");
                    DeleteInlineImageAttachments(context.PrimaryEntityId, service, tracingService);
                }
            }
            catch (FaultException<OrganizationServiceFault> fex)
            {
                tracingService.Trace("Error: " + fex.ToString());
                throw new InvalidPluginExecutionException(
                $"Error in {this.GetType().FullName}: {fex.Detail.Message}",
                fex
                );
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error: " + ex.ToString());
                throw new InvalidPluginExecutionException(
                $"Error in {this.GetType().FullName}: {ex.Message}", ex
                );
            }

            tracingService.Trace("Plugin - End");
        }

        private void Implementation(Entity email, ITracingService tracingService)
        {
            tracingService.Trace($"Start HTML cleanup implementation.Email id: {email.Id}");

            if (!email.Contains(Emails.Description) || email[Emails.Description] == null)
            {
                tracingService.Trace("Email description is empty. Skipping.");
                return;
            }

            string html = email[Emails.Description].ToString();

            if (string.IsNullOrWhiteSpace(html))
            {
                tracingService.Trace("Email description is whitespace. Skipping.");
                return;
            }

            tracingService.Trace("Original length: " + html.Length);

            html = Regex.Replace(html, "<img[^>]*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, "&lt;img[^&gt;]*&gt;", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, "&amp;lt;img[^&amp;gt;]*&amp;gt;", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Remove base64 inline images
            html = Regex.Replace(html, "data:image\\/[^;]+;base64,[^\"]+", string.Empty, RegexOptions.IgnoreCase);

            // Remove cid: references
            html = Regex.Replace(
            html,
            "cid:[^\"'>\\s]+",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            // (Optional) clean empty p tags, VML, comments etc.
            html = Regex.Replace(
            html,
            "&lt;p[^&gt;]*&gt;(\\s|&amp;nbsp;|&lt;br\\s*/?&gt;|&lt;o:p&gt;.*?&lt;/o:p&gt;)*&lt;/p&gt;",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            tracingService.Trace("Cleaned length: " + html.Length);

            //email[Emails.Description] = html;

            var stripped = Regex.Replace(html, "<.*?>", string.Empty);
            email[Emails.Description] = stripped;

            tracingService.Trace("End of Implementation");
        }

        private void DeleteInlineImageAttachments(Guid emailId, IOrganizationService service, ITracingService tracing)
        {
            tracing.Trace("Searching for inline image attachments...");

            QueryExpression qe = new QueryExpression("activitymimeattachment");
            qe.ColumnSet = new ColumnSet("activitymimeattachmentid", "mimetype", "objectid");
            qe.Criteria.AddCondition("objectid", ConditionOperator.Equal, emailId);
            qe.Criteria.AddCondition("mimetype", ConditionOperator.Like, "image/%");

            var attachments = service.RetrieveMultiple(qe);

            tracing.Trace($"Found {attachments.Entities.Count} inline images.");

            foreach (var att in attachments.Entities)
            {
                tracing.Trace("Deleting attachment: " + att.Id);
                service.Delete("activitymimeattachment", att.Id);
            }
        }
    }
}
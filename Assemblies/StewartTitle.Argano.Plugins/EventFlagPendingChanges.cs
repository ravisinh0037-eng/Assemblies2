using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StewartTitle.Argano.Plugins
{
    public class EventFlagPendingChanges : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {
                    Entity targetEntity = (Entity)context.InputParameters["Target"];
                    Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;

                    // Check if any of the 3 key fields changed
                    bool fieldsChanged =
                        targetEntity.Contains("msevtmgt_eventstartdate") ||
                        targetEntity.Contains("msevtmgt_eventenddate") ||
                        targetEntity.Contains("msevtmgt_building");

                    tracingService.Trace("Fields Changed: {0}", fieldsChanged);

                    if (fieldsChanged)
                    {
                        // Get current publish status from preImage
                        OptionSetValue publishStatus = null;

                        if (preImage != null && preImage.Contains("msevtmgt_publishstatus"))
                        {
                            publishStatus = preImage.GetAttributeValue<OptionSetValue>("msevtmgt_publishstatus");
                        }

                        tracingService.Trace("Publish Status: {0}", publishStatus?.Value.ToString() ?? "null");

                        // 100000003 = Live — do NOT flag if event is currently Live
                        if (publishStatus == null || publishStatus.Value != 100000003)
                        {
                            // Set the pending changes flag to Yes
                            targetEntity["stt_haspendingchanges"] = true;

                            tracingService.Trace("stt_haspendingchanges set to true");
                        }
                        else
                        {
                            tracingService.Trace("Event is Live - flag not set");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error in EventFlagPendingChanges plugin: {0}", ex.ToString());
                throw;
            }
        }
    }
}

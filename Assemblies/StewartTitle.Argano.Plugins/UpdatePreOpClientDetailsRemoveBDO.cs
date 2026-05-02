using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;

namespace StewartTitle.Argano.Plugins
{
    public class UpdatePreOpClientDetailsRemoveBDO : IPlugin
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
                    Entity targetentity = (Entity)context.InputParameters["Target"];
                    Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;

                    if (targetentity.Contains("stt_clientstagecode"))
                    {
                        OptionSetValue newStage = targetentity.GetAttributeValue<OptionSetValue>("stt_clientstagecode");

                        if (newStage.Value == 924510000)
                        {
                            if (preImage != null && preImage.Contains("stt_bdoid") && preImage["stt_bdoid"] != null)
                            {
                                targetentity["stt_bdoid"] = null;
                                tracingService.Trace("BDO removed because contact demoted to Cold Producer");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error in RemoveBDOOnColdProducer plugin: {0}", ex.ToString());
                throw;
            }
        }
    }
}

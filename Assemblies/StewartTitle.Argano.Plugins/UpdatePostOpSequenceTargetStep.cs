// <copyright file="CreatePostOpPhonecallTwilio.cs" company="Stewart">
// Copyright 2025 Stewart
// </copyright>

namespace StewartTitle.Argano.Plugins
{
    using System;
    using global::Argano.Utils;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// When a phonecall is created by a Sequence Twilio, then this plugin should update that phone call by turning on a flag called stt_wascreatedbyasequence.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Update" - Primary Entity: "msdyn_sequencetargetstep" - Secondary Entity: n/a
    ///     Attributes: all
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Synchronous-Deploy; Server.
    ///
    /// Images: n/a.
    /// </remarks>
    public class UpdatePostOpSequenceTargetStep : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace($"Starting UpdatePostOpSequenceTargetStep plugin... ");

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity sequenceTargetStepTrigger)
                {
                    if (sequenceTargetStepTrigger.ContainsData("msdyn_linkedactivityid"))
                    {
                        string fetch = $@"<fetch>	                                        
	                                        <entity name='phonecall'>		                                        
		                                        <attribute name='activityid' />
                                                <attribute name='stt_wascreatedbyasequence' />                                                
		                                        <filter type='and'>
			                                        <condition attribute='activityid' operator='eq' value='{sequenceTargetStepTrigger.GetAttributeValue<Guid>("msdyn_linkedactivityid")}' />
		                                        </filter>
	                                        </entity>
                                        </fetch>";

                        EntityCollection phonecallsCollection = service.RetrieveMultiple(new FetchExpression(fetch));
                        if (phonecallsCollection.Entities.Count == 1)
                        {
                            tracingService.Trace("Related Phone call found");
                            Entity phonecallToUpdate = phonecallsCollection.Entities[0];

                            if (!phonecallToUpdate.GetAttributeValue<bool>("stt_wascreatedbyasequence"))
                            {
                                phonecallToUpdate.Attributes["stt_wascreatedbyasequence"] = true;
                                service.Update(phonecallToUpdate);
                                tracingService.Trace("This a phone call created by a Sequence, we update phone call setting the boolean");
                            }
                            else
                            {
                                tracingService.Trace("This a phone call has already this boolean set in true.");
                            }
                        }
                    }
                    else
                    {
                        tracingService.Trace("This step doesn't have a related activity");
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"!! Error in UpdatePostOpSequenceTargetStep plugin: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error in UpdatePostOpSequenceTargetStep plugin: {ex.Message}");
            }
        }
    }
}

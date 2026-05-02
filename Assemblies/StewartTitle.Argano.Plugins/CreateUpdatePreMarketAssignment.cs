using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace StewartTitle.Argano.Plugins
{
    public class CreateUpdatePreMarketAssignment : IPlugin
    {
        IOrganizationService service = null;
        ITracingService tracingService = null;
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace("inside plugin CreateUpdatePreMarketAssignment");
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
            {
                if (targetEntity.LogicalName == "stt_marketmember")
                {

                    tracingService.Trace("context.MessageName = " + context.MessageName.ToLower());
                    if (context.MessageName.ToLower().Equals("create"))
                    {
                        Entity marketAssignment = (Entity)context.InputParameters["Target"];
                        CheckUpdateMarketAssignment(marketAssignment);

                    }
                }
            }
            else if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is EntityReference targetEntityRef)
            {
                if (targetEntityRef.LogicalName == "stt_marketmember")
                {
                    if (context.MessageName.ToLower().Equals("delete"))
                    {
                        int memberType = 0;
                        EntityReference market = null;
                        EntityReference user = null;

                        // var target = (EntityReference)context.InputParameters["Target"];
                        tracingService.Trace($"Pre-Delete triggered for: {targetEntityRef.LogicalName} - {targetEntityRef.Id}");
                        // Retrieve Pre-Image (must be registered)
                        Entity preImage = null;
                        if (context.PreEntityImages.Contains("PreImage"))
                        {
                            preImage = context.PreEntityImages["PreImage"];
                        }
                        // Example: read fields from Pre-Image
                        if (preImage != null)
                        {
                            if (preImage.Contains("stt_membertype"))
                            {
                                memberType = preImage.GetAttributeValue<OptionSetValue>("stt_membertype").Value;
                                tracingService.Trace($"stt_membertype: {memberType}");
                            }
                            if (preImage.Contains("stt_user"))
                            {
                                user = preImage.GetAttributeValue<EntityReference>("stt_user");
                                tracingService.Trace($"stt_user: {user.Id}");
                            }
                            if (preImage.Contains("stt_market"))
                            {
                                market = preImage.GetAttributeValue<EntityReference>("stt_market");
                                tracingService.Trace($"stt_market: {market.Id}");
                            }
                            if (memberType != 0 && user != null && market != null)
                            {
                                Entity entityUser = service.Retrieve(user.LogicalName, user.Id, new ColumnSet("fullname"));

                                RemoveMemeberFromMarketOnDelete(memberType, market, entityUser);
                            }
                        }
                    }
                }
            }
        }
        private void RemoveMemeberFromMarketOnDelete(int memberType, EntityReference market, Entity entityUser)
        {
            tracingService.Trace("inside RemoveMemeberFromMarketOnDelete");
            string userFullName = entityUser.GetAttributeValue<string>("fullname");

            Entity marketEntity = RetrieveMarket(market);

            EntityReference marketOwner = marketEntity.GetAttributeValue<EntityReference>("ownerid");

            bool userNotExistInOtherRole = CheckUserInMarketMerketMember(memberType, market, entityUser);
            if (userNotExistInOtherRole)
            {
                RemoveMemberFromTeam(marketOwner.Id, entityUser.Id);
            }
            UpdateMarektDelete(memberType, marketEntity, userFullName);
        }
        private bool CheckUserInMarketMerketMember(int memberType, EntityReference market, Entity entityUser)
        {
            bool flag = true;
            string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
              <entity name='stt_marketmember'>
                <attribute name='stt_marketmemberid' />
                <attribute name='stt_name' />
                <attribute name='createdon' />
                <order attribute='stt_name' descending='false' />
                <filter type='and'>
                  <condition attribute='stt_membertype' operator='ne' value='" + memberType + @"' />
                  <condition attribute='stt_user' operator='eq'  uitype='systemuser' value='" + entityUser.Id + @"' />
                  <condition attribute='stt_market' operator='eq'  uitype='stt_market' value='" + market.Id + @"' />
	              <condition attribute='statecode' operator='eq' value='0' />
                </filter>
              </entity>
            </fetch>";
            EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
            if (collection.Entities.Count > 0)
            {
                flag = false;
            }
            fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
              <entity name='stt_marketteam'>
                <attribute name='stt_marketteamid' />
                <attribute name='stt_name' />
                <attribute name='createdon' />
                <attribute name='stt_bdoid' />
                <attribute name='stt_isaid' />
                <attribute name='stt_marketid' />
                <order attribute='stt_marketid' descending='false' />
                <order attribute='stt_isaid' descending='false' />
                <filter type='and'>
                  <filter type='or'>
                    <condition attribute='stt_bdoid' operator='eq'  uitype='systemuser' value='"+ entityUser.Id + @"' />
                    <condition attribute='stt_isaid' operator='eq'  uitype='systemuser' value='"+ entityUser.Id + @"' />
                  </filter>
                  <condition attribute='stt_marketid' operator='eq'  uitype='stt_market' value='"+ market.Id + @"' />
                </filter>
              </entity>
            </fetch>";
            collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
            if (collection.Entities.Count > 0)
            {
                flag = false;
            }
            return flag;
        }
        private void RemoveMemberFromTeam(Guid teamId, Guid userId)
        {
            try
            {
                // Example: remove a user from a team
                // Guid teamId = new Guid("PUT-TEAM-ID-HERE");
                // Guid userId = new Guid("PUT-USER-ID-HERE");

                var request = new DisassociateRequest
                {
                    Target = new EntityReference("team", teamId),
                    RelatedEntities = new EntityReferenceCollection
                {
                    new EntityReference("systemuser", userId)
                },
                    Relationship = new Relationship("teammembership_association")
                };

                service.Execute(request);

                tracingService.Trace($"User {userId} removed from team {teamId}");
            }
            catch (Exception ex)
            {
                tracingService.Trace("Plugin error in function RemoveMemberFromTeam: " + ex.Message);
            }
        }
        private void CheckUpdateMarketAssignment(Entity marketAssignment)
        {
            int memberType = 0;
            EntityReference market = null;
            EntityReference user = null;

            if (marketAssignment.Contains("stt_membertype") && marketAssignment["stt_membertype"] != null)
            {
                memberType = marketAssignment.GetAttributeValue<OptionSetValue>("stt_membertype").Value;
            }
            if (marketAssignment.Contains("stt_market") && marketAssignment["stt_market"] != null)
            {
                market = marketAssignment.GetAttributeValue<EntityReference>("stt_market");
            }
            if (marketAssignment.Contains("stt_user") && marketAssignment["stt_user"] != null)
            {
                user = marketAssignment.GetAttributeValue<EntityReference>("stt_user");
            }

            if (memberType != 0 && market != null && user != null)
            {
                bool flag = CheckMartketAssignment(memberType, market, user);
                tracingService.Trace("flag = " + flag);
                if (flag)
                {
                    tracingService.Trace("inside  if (flag)= " + flag);
                    throw new InvalidPluginExecutionException("A user already exist with same role, duplicate are not allowed");
                }
                else
                {
                    Entity marketEntity = RetrieveMarket(market);

                    Entity userEntity = service.Retrieve(user.LogicalName, user.Id, new ColumnSet("fullname"));

                    string userFullName = userEntity.GetAttributeValue<string>("fullname");
                    tracingService.Trace("userFullName = " + userFullName);

                    UpdateMarekt(memberType, marketEntity, userFullName);

                }
            }
        }
        private void UpdateMarektDelete(int memberType, Entity marketEntity, string userFullName)
        {
            string stt_seniordivisionpresident = string.Empty;
            string stt_salesleader = string.Empty;
            string stt_salesdirector = string.Empty;
            string stt_groupseniorvicepresident = string.Empty;
            string stt_divisionpresident = string.Empty;
            string stt_divisionadministrator = string.Empty;
            string stt_businessstrategist = string.Empty;

            string nameToRemove = userFullName;

            tracingService.Trace("memberType = " + memberType);

            Entity updateMarket = new Entity(marketEntity.LogicalName);
            updateMarket.Id = marketEntity.Id;

            if (memberType == 198730000)//198730000 : Business Strategist
            {
                stt_businessstrategist = marketEntity.Contains("stt_businessstrategist") && marketEntity["stt_businessstrategist"] != null ?
                    marketEntity.GetAttributeValue<string>("stt_businessstrategist") : string.Empty;

                // Split → remove → join
                var result = string.Join("; ",
                    stt_businessstrategist.Split(';')
                         .Select(x => x.Trim())
                         .Where(x => !x.Equals(nameToRemove, StringComparison.OrdinalIgnoreCase)));

                stt_businessstrategist = result;
                updateMarket["stt_businessstrategist"] = stt_businessstrategist;
                tracingService.Trace("stt_businessstrategist = " + stt_businessstrategist);
            }
            else if (memberType == 198730001)//198730001 : Sales Leader
            {
                stt_salesleader = marketEntity.Contains("stt_salesleader") && marketEntity["stt_salesleader"] != null ?
                    marketEntity.GetAttributeValue<string>("stt_salesleader") : string.Empty;

                // Split → remove → join
                var result = string.Join("; ",
                    stt_salesleader.Split(';')
                         .Select(x => x.Trim())
                         .Where(x => !x.Equals(nameToRemove, StringComparison.OrdinalIgnoreCase)));

                stt_salesleader = result;

                updateMarket["stt_salesleader"] = stt_salesleader;

                tracingService.Trace("stt_salesleader = " + stt_salesleader);
            }
            else if (memberType == 198730002)//198730002 : Division President
            {
                stt_divisionpresident = marketEntity.Contains("stt_divisionpresident") && marketEntity["stt_divisionpresident"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_divisionpresident") : string.Empty;

                // Split → remove → join
                var result = string.Join("; ",
                    stt_divisionpresident.Split(';')
                         .Select(x => x.Trim())
                         .Where(x => !x.Equals(nameToRemove, StringComparison.OrdinalIgnoreCase)));

                stt_divisionpresident = result;

                updateMarket["stt_divisionpresident"] = stt_divisionpresident;

                tracingService.Trace("stt_divisionpresident = " + stt_divisionpresident);
            }
            else if (memberType == 198730006)//198730006 : Division Administrator
            {
                stt_divisionadministrator = marketEntity.Contains("stt_divisionadministrator") && marketEntity["stt_divisionadministrator"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_divisionadministrator") : string.Empty;

                // Split → remove → join
                var result = string.Join("; ",
                    stt_divisionadministrator.Split(';')
                         .Select(x => x.Trim())
                         .Where(x => !x.Equals(nameToRemove, StringComparison.OrdinalIgnoreCase)));

                stt_divisionadministrator = result;

                updateMarket["stt_divisionadministrator"] = stt_divisionadministrator;

                tracingService.Trace("stt_divisionadministrator = " + stt_divisionadministrator);
            }
            if (memberType == 198730003)//198730003 : Senior Division President
            {
                stt_seniordivisionpresident = marketEntity.Contains("stt_seniordivisionpresident") && marketEntity["stt_seniordivisionpresident"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_seniordivisionpresident") : string.Empty;

                // Split → remove → join
                var result = string.Join("; ",
                    stt_seniordivisionpresident.Split(';')
                         .Select(x => x.Trim())
                         .Where(x => !x.Equals(nameToRemove, StringComparison.OrdinalIgnoreCase)));

                stt_seniordivisionpresident = result;

                updateMarket["stt_seniordivisionpresident"] = stt_seniordivisionpresident;


                tracingService.Trace("stt_seniordivisionpresident = " + stt_seniordivisionpresident);
            }
            else if (memberType == 198730004)//198730004 : Sales Director
            {
                stt_salesdirector = marketEntity.Contains("stt_salesdirector") && marketEntity["stt_salesdirector"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_salesdirector") : string.Empty;

                // Split → remove → join
                var result = string.Join("; ",
                    stt_salesdirector.Split(';')
                         .Select(x => x.Trim())
                         .Where(x => !x.Equals(nameToRemove, StringComparison.OrdinalIgnoreCase)));

                stt_salesdirector = result;

                updateMarket["stt_salesdirector"] = stt_salesdirector;


                tracingService.Trace("stt_salesdirector = " + stt_salesdirector);
            }
            else if (memberType == 198730005)//198730005 : Group Senior Vice President
            {
                stt_groupseniorvicepresident = marketEntity.Contains("stt_groupseniorvicepresident") && marketEntity["stt_groupseniorvicepresident"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_groupseniorvicepresident") : string.Empty;

                // Split → remove → join
                var result = string.Join("; ",
                    stt_groupseniorvicepresident.Split(';')
                         .Select(x => x.Trim())
                         .Where(x => !x.Equals(nameToRemove, StringComparison.OrdinalIgnoreCase)));

                stt_groupseniorvicepresident = result;

                updateMarket["stt_groupseniorvicepresident"] = stt_groupseniorvicepresident;

                tracingService.Trace("stt_groupseniorvicepresident = " + stt_groupseniorvicepresident);
            }

            service.Update(updateMarket);
        }
        private void UpdateMarekt(int memberType, Entity marketEntity, string userFullName)
        {
            string stt_seniordivisionpresident = string.Empty;
            string stt_salesleader = string.Empty;
            string stt_salesdirector = string.Empty;
            string stt_groupseniorvicepresident = string.Empty;
            string stt_divisionpresident = string.Empty;
            string stt_divisionadministrator = string.Empty;
            string stt_businessstrategist = string.Empty;
            tracingService.Trace("memberType = " + memberType);
            if (memberType == 198730000)//198730000 : Business Strategist
            {
                stt_businessstrategist = marketEntity.Contains("stt_businessstrategist") && marketEntity["stt_businessstrategist"] != null ?
                    marketEntity.GetAttributeValue<string>("stt_businessstrategist") : string.Empty;

                if (!stt_businessstrategist.Contains(userFullName))
                {
                    if (stt_businessstrategist != string.Empty)
                        stt_businessstrategist += "; " + userFullName;
                    else
                        stt_businessstrategist += userFullName;
                }

                tracingService.Trace("stt_businessstrategist = " + stt_businessstrategist);
            }
            else if (memberType == 198730001)//198730001 : Sales Leader
            {
                stt_salesleader = marketEntity.Contains("stt_salesleader") && marketEntity["stt_salesleader"] != null ?
                    marketEntity.GetAttributeValue<string>("stt_salesleader") : string.Empty;

                if (!stt_salesleader.Contains(userFullName))
                {
                    if (stt_salesleader != string.Empty)
                        stt_salesleader += "; " + userFullName;
                    else
                        stt_salesleader += userFullName;
                }

                tracingService.Trace("stt_salesleader = " + stt_salesleader);
            }
            else if (memberType == 198730002)//198730002 : Division President
            {
                stt_divisionpresident = marketEntity.Contains("stt_divisionpresident") && marketEntity["stt_divisionpresident"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_divisionpresident") : string.Empty;

                if (!stt_divisionpresident.Contains(userFullName))
                {
                    if (stt_divisionpresident != string.Empty)
                        stt_divisionpresident += "; " + userFullName;
                    else
                        stt_divisionpresident += userFullName;
                }

                tracingService.Trace("stt_divisionpresident = " + stt_divisionpresident);
            }
            else if (memberType == 198730006)//198730006 : Division Administrator
            {
                stt_divisionadministrator = marketEntity.Contains("stt_divisionadministrator") && marketEntity["stt_divisionadministrator"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_divisionadministrator") : string.Empty;

                if (!stt_divisionadministrator.Contains(userFullName))
                {
                    if (stt_divisionadministrator != string.Empty)
                        stt_divisionadministrator += "; " + userFullName;
                    else
                        stt_divisionadministrator += userFullName;
                }

                tracingService.Trace("stt_divisionadministrator = " + stt_divisionadministrator);
            }
            if (memberType == 198730003)//198730003 : Senior Division President
            {
                stt_seniordivisionpresident = marketEntity.Contains("stt_seniordivisionpresident") && marketEntity["stt_seniordivisionpresident"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_seniordivisionpresident") : string.Empty;

                if (!stt_seniordivisionpresident.Contains(userFullName))
                {
                    if (stt_seniordivisionpresident != string.Empty)
                        stt_seniordivisionpresident += "; " + userFullName;
                    else
                        stt_seniordivisionpresident += userFullName;
                }

                tracingService.Trace("stt_seniordivisionpresident = " + stt_seniordivisionpresident);
            }
            else if (memberType == 198730004)//198730004 : Sales Director
            {
                stt_salesdirector = marketEntity.Contains("stt_salesdirector") && marketEntity["stt_salesdirector"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_salesdirector") : string.Empty;

                if (!stt_salesdirector.Contains(userFullName))
                {
                    if (stt_salesdirector != string.Empty)
                        stt_salesdirector += "; " + userFullName;
                    else
                        stt_salesdirector += userFullName;
                }

                tracingService.Trace("stt_salesdirector = " + stt_salesdirector);
            }
            else if (memberType == 198730005)//198730005 : Group Senior Vice President
            {
                stt_groupseniorvicepresident = marketEntity.Contains("stt_groupseniorvicepresident") && marketEntity["stt_groupseniorvicepresident"] != null ?
                   marketEntity.GetAttributeValue<string>("stt_groupseniorvicepresident") : string.Empty;

                if (!stt_groupseniorvicepresident.Contains(userFullName))
                {
                    if (stt_groupseniorvicepresident != string.Empty)
                        stt_groupseniorvicepresident += "; " + userFullName;
                    else
                        stt_groupseniorvicepresident += userFullName;
                }

                tracingService.Trace("stt_groupseniorvicepresident = " + stt_groupseniorvicepresident);
            }

            Entity updateMarket = new Entity(marketEntity.LogicalName);
            updateMarket.Id = marketEntity.Id;

            if (stt_seniordivisionpresident != string.Empty)
            {
                updateMarket["stt_seniordivisionpresident"] = stt_seniordivisionpresident;
            }
            if (stt_salesleader != string.Empty)
            {
                updateMarket["stt_salesleader"] = stt_salesleader;
            }
            if (stt_salesdirector != string.Empty)
            {
                updateMarket["stt_salesdirector"] = stt_salesdirector;
            }
            if (stt_groupseniorvicepresident != string.Empty)
            {
                updateMarket["stt_groupseniorvicepresident"] = stt_groupseniorvicepresident;
            }
            if (stt_divisionpresident != string.Empty)
            {
                updateMarket["stt_divisionpresident"] = stt_divisionpresident;
            }
            if (stt_divisionadministrator != string.Empty)
            {
                updateMarket["stt_divisionadministrator"] = stt_divisionadministrator;
            }
            if (stt_businessstrategist != string.Empty)
            {
                updateMarket["stt_businessstrategist"] = stt_businessstrategist;
            }

            service.Update(updateMarket);
        }
        private Entity RetrieveMarket(EntityReference market)
        {
            string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='stt_market'>
                                <attribute name='stt_marketid' />
                                <attribute name='stt_name' />
                                <attribute name='ownerid' />
                                <attribute name='createdon' />
                                <attribute name='stt_seniordivisionpresident' />
                                <attribute name='stt_salesleader' />
                                <attribute name='stt_salesdirector' />
                                <attribute name='stt_groupseniorvicepresident' />
                                <attribute name='stt_divisionpresident' />
                                <attribute name='stt_divisionadministrator' />
                                <attribute name='stt_businessstrategist' />
                                <order attribute='stt_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='stt_marketid' operator='eq' uiname='Alaska' uitype='stt_market' value='" + market.Id + @"' />
                                </filter>
                              </entity>
                            </fetch>";
            EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
            Entity marketEntity = null;
            if (collection.Entities.Count > 0)
                marketEntity = collection.Entities[0];

            return marketEntity;
        }
        private bool CheckMartketAssignment(int memberType, EntityReference market, EntityReference user)
        {
            bool flag = false;
            string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                      <entity name='stt_marketmember'>
                        <attribute name='stt_marketmemberid' />
                        <attribute name='stt_name' />
                        <attribute name='createdon' />
                        <order attribute='stt_name' descending='false' />
                        <filter type='and'>
                          <condition attribute='stt_market' operator='eq'  uitype='stt_market' value='" + market.Id + @"' />
                          <condition attribute='stt_user' operator='eq'  uitype='systemuser' value='" + user.Id + @"' />
                          <condition attribute='stt_membertype' operator='eq' value='" + memberType + @"' />
                          <condition attribute='statecode' operator='eq' value='0' />
                        </filter>
                      </entity>
                    </fetch>";
            EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
            if (collection.Entities.Count > 0)
            {
                flag = true;
            }
            return flag;
        }
    }
}

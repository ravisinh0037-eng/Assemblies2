using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;

namespace StewartTitle.Argano.Plugins.Utils
{
    public static class EnvironmentVariableHelper
    {
        public static string GetEnvironmentVariableValue(IOrganizationService service, string schemaName)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrWhiteSpace(schemaName)) throw new ArgumentNullException(nameof(schemaName));

            // Query the environmentvariabledefinition for the record with the given schema name
            QueryExpression defQuery = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("environmentvariabledefinitionid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("schemaname", ConditionOperator.Equal, schemaName)
                    }
                }
            };

            var defEntity = service.RetrieveMultiple(defQuery).Entities.FirstOrDefault();
            if (defEntity == null)
            {
                return null;
            }

            Guid defId = defEntity.Id;

            // Now query the environmentvariablevalue record for that definition
            QueryExpression valueQuery = new QueryExpression("environmentvariablevalue")
            {
                ColumnSet = new ColumnSet("value"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("environmentvariabledefinitionid", ConditionOperator.Equal, defId)
                    }
                }
            };

            var valueEntity = service.RetrieveMultiple(valueQuery).Entities.FirstOrDefault();
            if (valueEntity != null && valueEntity.Attributes.Contains("value"))
            {
                return valueEntity.GetAttributeValue<string>("value");
            }

            return null;
        }
    }
}

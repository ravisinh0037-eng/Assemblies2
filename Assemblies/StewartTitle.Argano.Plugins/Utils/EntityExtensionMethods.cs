// <copyright file="EntityExtensionMethods.cs" company="Stewart">
// Copyright 2025 Stewart
// </copyright>

namespace Argano.Utils
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Extension methods adding functionality to Microsoft.Xrm.Sdk.Entity.
    /// </summary>
    public static class EntityExtensionMethods
    {
        /// <summary>
        /// Creates a new entity from a factory.
        /// </summary>
        /// <param name="entity">Entity to apply on.</param>
        /// <param name="options">Initialization options.</param>
        /// <returns>An entity initialized.</returns>
        public static Entity Setup(this Entity entity, EntityFactory.Initializer options)
        {
            return EntityFactory.Unpack(entity, options);
        }

        /// <summary>
        /// Checks if value for an attribute is specified, and that it's not null.
        /// </summary>
        /// <param name="entity">Entity to check.</param>
        /// <param name="attributeName">Logical name of attribute.</param>
        /// <returns>True if the attribute has value and the value is not null, otherwise false.</returns>
        public static bool ContainsData(this Entity entity, string attributeName)
        {
            if (entity.Contains(attributeName))
            {
                if (entity[attributeName] != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the value for an attribute, when it's an aliased value.
        /// </summary>
        /// <typeparam name="T">Type of the underlying object.</typeparam>
        /// <param name="entity">Entity to check.</param>
        /// <param name="attributeName">Logical name of attribute.</param>
        /// <returns>The value, cast as type T.</returns>
        /// <exception cref="ArgumentNullException">When any parameter is null.</exception>
        /// <exception cref="InvalidOperationException">When the attribute requested is not an aliased value.</exception>
        public static T GetAliasedAttributeValue<T>(this Entity entity, string attributeName)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));
            _ = attributeName ?? throw new ArgumentNullException(nameof(attributeName));

            if (!entity.Contains(attributeName))
            {
                return default(T);
            }
            else
            {
                if (entity[attributeName] is AliasedValue aliased)
                {
                    return (T)aliased.Value;
                }
                else
                {
                    throw new InvalidOperationException("Attribute is not an aliased value.");
                }
            }
        }

        /// <summary>
        ///  Try to get the value for an attribute, when it's an aliased value.
        /// </summary>
        /// <typeparam name="T">Type of the underlying object.</typeparam>
        /// <param name="entity">Entity to check.</param>
        /// <param name="attributeName">Logical name of attribute.</param>
        /// <param name="result">Resulting value for the aliased attribute.</param>
        /// <returns>True if the value could be retrieved and converted, false otherwise.</returns>
        public static bool TryGetAliasedAttributeValue<T>(this Entity entity, string attributeName, out T result)
        {
            try
            {
                object attributeValue = entity[attributeName];
                if (attributeValue == null)
                {
                    result = default(T);
                    return false;
                }

                if (attributeValue is AliasedValue aliased)
                {
                    if (aliased.Value is T)
                    {
                        result = (T)aliased.Value;
                        return true;
                    }
                }
                else
                {
                    result = default(T);
                    return false;
                }

                TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
                result = (T)converter.ConvertFrom(attributeValue);
                return true;
            }
            catch
            {
                // Ignore error and return false.
            }

            result = default(T);
            return false;
        }

        /// <summary>
        /// Copies an attribute from an entity to another, specifying the attribute name from the source, returning it.
        /// </summary>
        /// <typeparam name="T">Type of the underlying object.</typeparam>
        /// <param name="destination">Destination entity to which to copy.</param>
        /// <param name="source">Source entity from which to copy.</param>
        /// <param name="sourceAttributeName">Source name for the attribute.</param>
        /// <returns>Value of the attribute copied, if found.</returns>
        public static T CopyAttributeValueIfItExists<T>(this Entity destination, Entity source, string sourceAttributeName)
            => CopyAttributeValueIfItExists<T>(destination, sourceAttributeName, source, sourceAttributeName);

        /// <summary>
        /// Copies an attribute from an entity to another, specifying the name of both the source and destination, returning it.
        /// </summary>
        /// <typeparam name="T">Type of the underlying object.</typeparam>
        /// <param name="destination">Destination entity to which to copy.</param>
        /// <param name="destinationAttributeName">Destination name for the attribute.</param>
        /// <param name="source">Source entity from which to copy.</param>
        /// <param name="sourceAttributeName">Source name for the attribute.</param>
        /// <returns>Value of the attribute copied, if found.</returns>
        public static T CopyAttributeValueIfItExists<T>(this Entity destination, string destinationAttributeName, Entity source, string sourceAttributeName)
        {
            if (source.Contains(sourceAttributeName))
            {
                T result = source.GetAttributeValue<T>(sourceAttributeName);
                destination.Attributes.Add(destinationAttributeName, result);
                return result;
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// Updates an entity with the attributes from a second one, updating attributes existing on both.
        /// </summary>
        /// <param name="baseEntity">Entity to update.</param>
        /// <param name="updatedEntity">Entity from which to take updated attributes.</param>
        /// <exception cref="ArgumentNullException">When any parameter is null.</exception>
        public static void UpdateAttributesWith(this Entity baseEntity, Entity updatedEntity)
        {
            _ = baseEntity ?? throw new ArgumentNullException(nameof(baseEntity));
            _ = updatedEntity ?? throw new ArgumentNullException(nameof(updatedEntity));

            foreach (string key in updatedEntity.Attributes.Keys)
            {
                if (!baseEntity.Attributes.ContainsKey(key))
                {
                    baseEntity.Attributes.Add(key, updatedEntity.Attributes[key]);

                    if (updatedEntity.Attributes[key] is OptionSetValue && updatedEntity.FormattedValues.ContainsKey(key))
                    {
                        baseEntity.FormattedValues.Add(key, updatedEntity.FormattedValues[key]);
                    }
                }
                else
                {
                    baseEntity.Attributes[key] = updatedEntity.Attributes[key];

                    if (updatedEntity.Attributes[key] is OptionSetValue)
                    {
                        if (updatedEntity.FormattedValues.ContainsKey(key))
                        {
                            baseEntity.FormattedValues[key] = updatedEntity.FormattedValues[key];
                        }
                        else
                        {
                            baseEntity.FormattedValues.Remove(key);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Appends attributes of an entity with the ones from a second one, not updating any existing.
        /// </summary>
        /// <param name="baseEntity">Entity to update.</param>
        /// <param name="updatedEntity">Entity from which to take updated attributes.</param>
        /// <exception cref="ArgumentNullException">When any parameter is null.</exception>
        public static void AppendAttributesWith(this Entity baseEntity, Entity updatedEntity)
        {
            _ = baseEntity ?? throw new ArgumentNullException(nameof(baseEntity));
            _ = updatedEntity ?? throw new ArgumentNullException(nameof(updatedEntity));

            foreach (string key in updatedEntity.Attributes.Keys)
            {
                if (!baseEntity.Attributes.ContainsKey(key))
                {
                    baseEntity.Attributes.Add(key, updatedEntity.Attributes[key]);

                    if (updatedEntity.Attributes[key] is OptionSetValue && updatedEntity.FormattedValues.ContainsKey(key))
                    {
                        baseEntity.FormattedValues.Add(key, updatedEntity.FormattedValues[key]);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the name from an entity reference, querying an organization service if needed.
        /// </summary>
        /// <param name="entityReference">Entity reference for which to obtain the name.</param>
        /// <param name="nameField">Logical name of the name field.</param>
        /// <param name="service">Organization service to use if needed.</param>
        /// <returns>Name for the record referenced.</returns>
        public static string GetName(this EntityReference entityReference, string nameField, IOrganizationService service)
        {
            if (entityReference.Name is null)
            {
                var entity = service.Retrieve(entityReference.LogicalName, entityReference.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet(nameField));
                return entity.GetAttributeValue<string>(nameField);
            }
            else
            {
                return entityReference.Name;
            }
        }

        /// <summary>
        /// Retrieves the name from an option set value, querying an organization service if needed.
        /// </summary>
        /// <param name="option">Option for which to obtain the name.</param>
        /// <param name="entity">Entity containing the option set.</param>
        /// <param name="field">Logical name of the name field.</param>
        /// <param name="service">Organization service to use if needed.</param>
        /// <returns>Name for the option set value.</returns>
        public static string GetName(this OptionSetValue option, Entity entity, string field, IOrganizationService service)
        {
            if (entity.FormattedValues.ContainsKey(field))
            {
                return entity.FormattedValues[field];
            }
            else
            {
                var retrieveAttributeRequest = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entity.LogicalName,
                    LogicalName = field,
                    RetrieveAsIfPublished = true,
                };

                var response = (RetrieveAttributeResponse)service.Execute(retrieveAttributeRequest);
                var metadata = (EnumAttributeMetadata)response.AttributeMetadata;
                var desiredOption = metadata.OptionSet.Options.FirstOrDefault(o => o.Value == option.Value);
                if (desiredOption != null)
                {
                    return desiredOption.Label.UserLocalizedLabel.Label;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the entities related to a current entity records.
        /// </summary>
        /// <param name="target">Entity containing the option set.</param>
        /// <param name="referenceEntityName">Name of entity referenced by the relationship to query.</param>
        /// <param name="relationshipName">Name of the relationship to query.</param>
        /// <param name="service">Organization service to use if needed.</param>
        /// <returns>Results from the query.</returns>
        public static DataCollection<Entity> QueryRelationships(this Entity target, string referenceEntityName, string relationshipName, IOrganizationService service)
            => target.ToEntityReference().QueryRelationships(referenceEntityName, relationshipName, service);

        /// <summary>
        /// Gets the entities related to a current entity records.
        /// </summary>
        /// <param name="reference">Entity containing the option set.</param>
        /// <param name="referenceEntityName">Name of entity referenced by the relationship to query.</param>
        /// <param name="relationshipName">Name of the relationship to query.</param>
        /// <param name="service">Organization service to use if needed.</param>
        /// <returns>Results from the query.</returns>
        public static DataCollection<Entity> QueryRelationships(this EntityReference reference, string referenceEntityName, string relationshipName, IOrganizationService service)
        {
            QueryExpression query = new QueryExpression(referenceEntityName);
            query.ColumnSet = new ColumnSet(true);

            Relationship relationship = new Relationship(relationshipName);
            relationship.PrimaryEntityRole = EntityRole.Referenced;

            RelationshipQueryCollection relatedEntity = new RelationshipQueryCollection();
            relatedEntity.Add(relationship, query);

            RetrieveRequest request = new RetrieveRequest();
            request.RelatedEntitiesQuery = relatedEntity;
            request.ColumnSet = new ColumnSet(true);
            request.Target = reference;

            RetrieveResponse response = (RetrieveResponse)service.Execute(request);

            return response.Entity.RelatedEntities[relationship].Entities;
        }

        /// <summary>
        /// Prints all attributes of an entity into a string.
        /// </summary>
        /// <param name="entity">Entity to print.</param>
        /// <returns>String with the entity attributes.</returns>
        public static string PrintToString(this Entity entity)
        {
            try
            {
                List<string> attributes = new List<string>();

                foreach (var attribute in entity.Attributes)
                {
                    try
                    {
                        string extra = string.Empty;
                        object value = attribute.Value;

                        if (value is AliasedValue)
                        {
                            extra = " (AliasedValue)";
                            value = (attribute.Value as AliasedValue).Value;
                        }

                        if (value is EntityReference)
                        {
                            attributes.Add($"{attribute.Key}{extra}: {(value as EntityReference).Id} (EntityReference)");
                        }
                        else if (value is Money)
                        {
                            attributes.Add($"{attribute.Key}{extra}: {(value as Money).Value} (Money)");
                        }
                        else if (value is OptionSetValue)
                        {
                            if (entity.FormattedValues.ContainsKey(attribute.Key))
                            {
                                attributes.Add($"{attribute.Key}{extra}: {entity.FormattedValues[attribute.Key]} ({(value as OptionSetValue).Value})");
                            }
                            else
                            {
                                attributes.Add($"{attribute.Key}{extra}: {(value as OptionSetValue).Value} (OptionSetValue)");
                            }
                        }
                        else
                        {
                            attributes.Add($"{attribute.Key}{extra}: {value} ({value.GetType().Name})");
                        }
                    }
                    catch
                    {
                        attributes.Add($"{attribute.Key}: error");
                    }
                }

                return $"{entity.LogicalName} - {entity.Id} - {string.Join(", ", attributes)}";
            }
            catch (Exception)
            {
                return $"Error during conversion into string.";
            }
        }
    }
}

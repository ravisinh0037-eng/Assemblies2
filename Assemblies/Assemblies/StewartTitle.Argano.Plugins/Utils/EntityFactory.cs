// <copyright file="EntityFactory.cs" company="Stewart">
// Copyright 2025 Stewart
// </copyright>

namespace Argano.Utils
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Xrm.Sdk;
    using static Argano.Utils.EntityFactory.Initializer;

    /// <summary>
    /// Factory to help in building or modifying instances of the Entity class.
    /// </summary>
    public static class EntityFactory
    {
        /// <summary>
        /// Creates a new Entity from an initializer.
        /// </summary>
        /// <param name="entity">Entity to apply on, or null if a complete new entity needs to be created.</param>
        /// <param name="options">Initializer to set the Entity's data to.</param>
        /// <returns>A new Entity instance initialized from the passed parameter.</returns>
        public static Entity Unpack(Entity entity, Initializer options)
        {
            entity = entity ?? new Entity();
            _ = options ?? throw new ArgumentNullException(nameof(options));

            entity.LogicalName = options.LogicalName;
            entity.Id = options.Id;
            entity.EntityState = EntityState.Unchanged;

            foreach (string key in options.Attributes.Keys)
            {
                if (options.Attributes[key] is EntityFactory.Initializer.FormattedValue value)
                {
                    entity.Attributes[key] = value.Value;
                    if (value.Formatted != null)
                    {
                        entity.FormattedValues.Add(key, value.Formatted);
                    }
                }
                else
                {
                    entity.Attributes[key] = options.Attributes[key];
                }
            }

            if (options.RelatedEntities != null && options.RelatedEntities.Length > 0)
            {
                foreach (var relation in options.RelatedEntities)
                {
                    EntityCollection relatedEntities = new EntityCollection();
                    foreach (var item in relation.Entities)
                    {
                        relatedEntities.Entities.Add(Unpack(null, item));
                    }

                    entity.RelatedEntities.Add(relation.Relationship, relatedEntities);
                }
            }

            return entity;
        }

        /// <summary>
        /// Creates a new Initializer Instance from an existing Entity.
        /// </summary>
        /// <param name="entity">Entity to pack into an Initializer.</param>
        /// <returns>An Initializer instance that can be serialized.</returns>
        public static Initializer Pack(Entity entity)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));

            var initializer = new Initializer(entity)
            {
                LogicalName = entity.LogicalName,
                Id = entity.Id,
                Attributes = new Dictionary<string, object>(),
                RelatedEntities = new Related[entity.RelatedEntities.Count],
            };

            foreach (var attribute in entity.Attributes)
            {
                initializer.Attributes.Add(attribute.Key, attribute.Value);

                if (entity.FormattedValues.ContainsKey(attribute.Key))
                {
                    initializer.Attributes[attribute.Key] = new FormattedValue()
                    {
                        Value = attribute.Value,
                        Formatted = entity.FormattedValues[attribute.Key],
                    };
                }
            }

            int i = 0;
            foreach (var related in entity.RelatedEntities)
            {
                initializer.RelatedEntities[i++] = new Related(related.Key, Array.ConvertAll(related.Value.Entities.ToArray(), e => Pack(e)));
            }

            return initializer;
        }

        /// <summary>
        /// Shorthand for creating a new Entity Reference attribute on an initializer.
        /// </summary>
        /// <param name="logicalName">The logical name of the entity.</param>
        /// <param name="id">The unique identifier of the entity.</param>
        /// <param name="formattedValue">The formatted value or description of the entity reference. Optional.</param>
        /// <returns>A new FormattedValue instance with the specified parameters.</returns>
        public static FormattedValue Lookup(string logicalName, Guid id, string formattedValue = null)
        {
            return new Initializer.FormattedValue()
            {
                Value = new EntityReference()
                {
                    LogicalName = logicalName,
                    Id = id,
                },
                Formatted = formattedValue,
            };
        }

        /// <summary>
        /// Shorthand for creating a new related Entity on an initializer.
        /// </summary>
        /// <param name="related">Relationship and entities to relate.</param>
        /// <returns>A list of related entities created from the provided parameters.</returns>
        public static Related[] RelatedEntities(params Related[] related)
            => related;

        /// <summary>
        /// Initializer class to define a Entity declaratively.
        /// </summary>
        public class Initializer
        {
            public Initializer()
            {

            }

            public Initializer(Entity entity)
            {
                _ = entity ?? throw new ArgumentNullException(nameof(entity));

            }

            /// <summary>
            /// Gets or sets the Logical Name.
            /// </summary>
            public string LogicalName { get; set; }

            /// <summary>
            /// Gets or sets the Entity name.
            /// </summary>
            /// <remarks>
            /// Retained for backward compatibility.
            /// </remarks>
            public string EntityName
            {
                get
                {
                    return this.LogicalName;
                }

                set
                {
                    this.LogicalName = value;
                }
            }

            /// <summary>
            /// Gets or sets the Entity identifier.
            /// </summary>
            public Guid Id { get; set; }

            /// <summary>
            /// Gets or sets the attribute collection.
            /// </summary>
            public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();

            /// <summary>
            /// Gets or sets the collection of related entities associated with the entity.
            /// </summary>
            public Related[] RelatedEntities { get; set; }

            /// <summary>
            /// Indexer for the attribute collection.
            /// </summary>
            /// <param name="i">Attribute key.</param>
            /// <returns>Attribute value.</returns>
            public object this[string i]
            {
                get { return this.Attributes[i]; }
                set { this.Attributes[i] = value; }
            }

            /// <summary>
            /// Helper subclass to aid in adding attributes with a formatted value or description.
            /// </summary>
            public class FormattedValue
            {
                /// <summary>
                /// Gets or sets attribute value.
                /// </summary>
                public object Value { get; set; }

                /// <summary>
                /// Gets or sets formated description.
                /// </summary>
                public string Formatted { get; set; }
            }

            /// <summary>
            /// Helper subclass to aid in adding relationships to an entity.
            /// </summary>
            public class Related
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="Related"/> class with the specified relationship and entities.
                /// </summary>
                /// <param name="relationship">The relationship schema name that links the entities.</param>
                /// <param name="values">The collection of initializer entities to associate with the relationship.</param>
                public Related(Relationship relationship, params Initializer[] values)
                {
                    this.Relationship = relationship;
                    this.Entities = values;
                }

                /// <summary>
                /// Gets or sets the relationship schema name that links the entities.
                /// </summary>
                public Relationship Relationship { get; set; }

                /// <summary>
                /// Gets or sets the collection of initializer entities associated with the relationship.
                /// </summary>
                public Initializer[] Entities { get; set; }
            }
        }
    }
}

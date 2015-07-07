﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Plainion.Flames.Infrastructure.Model;
using Plainion.Flames.Infrastructure.Services;
using Plainion.Flames.Modules.Filters.Model;

namespace Plainion.Flames.Modules.Filters
{
    /// <summary>
    /// Adds persistancy of filters to the project
    /// </summary>
    class FiltersProvider : ProjectItemProviderBase
    {
        private const string ProviderId = "{FFBBE09A-C73A-43EC-AF77-8E146B078E01}.Filters";

        // TODO: actually we just need the eariest possible trigger that project was loaded
        // -> we use TraceLog loaded as workaround here
        public override void OnProjectDeserialized(IProject project, IProjectSerializationContext context)
        {
            if (!context.HasEntry(ProviderId))
            {
                return;
            }

            using (var stream = context.GetEntry(ProviderId))
            {
                var serializer = new DataContractSerializer(typeof(FiltersDocument), GetKnownDataContractTypes());
                project.Items.Add((FiltersDocument)serializer.ReadObject(stream));
            }
        }

        public override void OnProjectSerializing(IProject project, IProjectSerializationContext context)
        {
            var document = project.Items.OfType<FiltersDocument>().SingleOrDefault();
            if (document == null)
            {
                return;
            }

            using (var stream = context.CreateEntry(ProviderId))
            {
                var serializer = new DataContractSerializer(typeof(FiltersDocument), GetKnownDataContractTypes());
                serializer.WriteObject(stream, document);
            }
        }

        private IEnumerable<Type> GetKnownDataContractTypes()
        {
            return GetType().Assembly.GetTypes()
                .Where(type => !type.IsAbstract)
                .Where(type => type.GetCustomAttributes(typeof(DataContractAttribute), false).Any())
                .ToList();
        }
    }
}

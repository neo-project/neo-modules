// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.OpenApi.Models;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Neo.Plugins.RestServer.Swagger.Filters
{
    internal class SwaggerExcludeFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema?.Properties == null)
                return;
            if (context.Type == typeof(Block))
                schema.Properties.Remove("header");
            if (context.Type == typeof(NefFile) ||
                context.Type == typeof(MethodToken) ||
                context.Type == typeof(Witness) ||
                context.Type == typeof(Signer) ||
                context.Type == typeof(TransactionAttribute))
                schema.Properties.Remove("size");

        }
    }
}

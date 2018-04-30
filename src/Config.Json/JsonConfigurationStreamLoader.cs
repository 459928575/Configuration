// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.Configuration.Json
{
    /// <summary>
    /// 
    /// </summary>
    public class JsonConfigurationStreamLoader : IConfigurationStreamLoader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="stream"></param>
        public void Load(IConfigurationProvider provider, Stream stream)
            => new JsonLoader(provider, stream).Load();

        // Tweaked from JsonConfigurationFileParser
        private class JsonLoader
        {
            private readonly IConfigurationProvider _provider;
            private readonly Stack<string> _context = new Stack<string>();
            private readonly JsonTextReader _reader;
            private string _currentPath;

            public JsonLoader(IConfigurationProvider provider, Stream stream)
            {
                _reader = new JsonTextReader(new StreamReader(stream));
                _reader.DateParseHandling = DateParseHandling.None;
                _provider = provider;
            }

            public void Load()
            {
                var jsonConfig = JObject.Load(_reader);
                VisitJObject(jsonConfig);
            }

            private void VisitJObject(JObject jObject)
            {
                foreach (var property in jObject.Properties())
                {
                    EnterContext(property.Name);
                    VisitProperty(property);
                    ExitContext();
                }
            }

            private void VisitProperty(JProperty property)
                => VisitToken(property.Value);

            private void VisitToken(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        VisitJObject(token.Value<JObject>());
                        break;

                    case JTokenType.Array:
                        VisitArray(token.Value<JArray>());
                        break;

                    case JTokenType.Integer:
                    case JTokenType.Float:
                    case JTokenType.String:
                    case JTokenType.Boolean:
                    case JTokenType.Bytes:
                    case JTokenType.Raw:
                    case JTokenType.Null:
                        VisitPrimitive(token.Value<JValue>());
                        break;

                    default:
                        throw new FormatException(Resources.FormatError_UnsupportedJSONToken(
                            _reader.TokenType,
                            _reader.Path,
                            _reader.LineNumber,
                            _reader.LinePosition));
                }
            }

            private void VisitArray(JArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    EnterContext(index.ToString());
                    VisitToken(array[index]);
                    ExitContext();
                }
            }

            private void VisitPrimitive(JValue data)
            {
                var key = _currentPath;

                if (_provider.TryGet(key, out var ignored))
                {
                    throw new FormatException(Resources.FormatError_KeyIsDuplicated(key));
                }
                _provider.Set(key, data.ToString(CultureInfo.InvariantCulture));
            }

            private void EnterContext(string context)
            {
                _context.Push(context);
                _currentPath = ConfigurationPath.Combine(_context.Reverse());
            }

            private void ExitContext()
            {
                _context.Pop();
                _currentPath = ConfigurationPath.Combine(_context.Reverse());
            }
        }
    }
}

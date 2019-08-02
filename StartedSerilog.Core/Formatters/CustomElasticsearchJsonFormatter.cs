// based on https://github.com/serilog/serilog-sinks-elasticsearch/blob/dev/src/Serilog.Formatting.Elasticsearch/ElasticsearchJsonFormatter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elasticsearch.Net;
using Serilog.Events;
using Serilog.Formatting.Elasticsearch;
using Serilog.Parsing;
using StartedSerilog.WebUI;

namespace StartedSerilog.Core.Formatters
{
    public class CustomElasticsearchJsonFormatter : DefaultJsonFormatter
    {
        readonly IElasticsearchSerializer _serializer;
        readonly bool _inlineFields;

        /// <summary>
        /// Render message property name
        /// </summary>
        public const string RenderedMessagePropertyName = "@message";

        /// <summary>
        /// Message template property name
        /// </summary>
        public const string MessageTemplatePropertyName = "messageTemplate";

        /// <summary>
        /// Level property name
        /// </summary>
        public const string LevelPropertyName = "level";

        /// <summary>
        /// Timestamp property name
        /// </summary>
        public const string TimestampPropertyName = "@timestamp";

        /// <summary>
        /// Construct a CustomElasticsearchJsonFormatter.
        /// </summary>
        /// <param name="omitEnclosingObject">If true, the properties of the event will be written to
        /// the output without enclosing braces. Otherwise, if false, each event will be written as a well-formed
        /// JSON object.</param>
        /// <param name="closingDelimiter">A string that will be written after each log event is formatted.
        /// If null, <see cref="Environment.NewLine"/> will be used. Ignored if <paramref name="omitEnclosingObject"/>
        /// is true.</param>
        /// <param name="renderMessage">If true, the message will be rendered and written to the output as a
        /// property named RenderedMessage.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="serializer">Inject a serializer to force objects to be serialized over being ToString()</param>
        /// <param name="inlineFields">When set to true values will be written at the root of the json document</param>
        /// <param name="renderMessageTemplate">If true, the message template will be rendered and written to the output as a
        /// property named RenderedMessageTemplate.</param>
        public CustomElasticsearchJsonFormatter(
            bool omitEnclosingObject = false,
            string closingDelimiter = null,
            bool renderMessage = true,
            IFormatProvider formatProvider = null,
            IElasticsearchSerializer serializer = null,
            bool inlineFields = false,
            bool renderMessageTemplate = true)
            : base(omitEnclosingObject, closingDelimiter, renderMessage, formatProvider, renderMessageTemplate)
        {
            _serializer = serializer;
            _inlineFields = inlineFields;
        }

        /// <summary>
        /// Writes out individual renderings of attached properties
        /// </summary>
        protected override void WriteRenderings(IGrouping<string, PropertyToken>[] tokensWithFormat,
            IReadOnlyDictionary<string, LogEventPropertyValue> properties, TextWriter output)
        {
            output.Write(",\"{0}\":{{", "renderings");
            WriteRenderingsValues(tokensWithFormat, properties, output);
            output.Write("}");
        }

        /// <summary>
        /// Writes out the attached properties
        /// </summary>
        protected override void WriteProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties,
            TextWriter output)
        {
            if (!_inlineFields)
                output.Write(",\"{0}\":{{", "fields");
            else
                output.Write(",");

            WritePropertiesValues(properties, output);

            if (!_inlineFields)
                output.Write("}");
        }

        /// <summary>
        /// Escape the name of the Property before calling ElasticSearch
        /// </summary>
        protected override void WriteDictionary(IReadOnlyDictionary<ScalarValue, LogEventPropertyValue> elements,
            TextWriter output)
        {
            var escaped = elements.ToDictionary(e => DotEscapeFieldName(e.Key), e => e.Value);

            base.WriteDictionary(escaped, output);
        }

        /// <summary>
        /// Escape the name of the Property before calling ElasticSearch
        /// </summary>
        protected override void WriteJsonProperty(string name, object value, ref string precedingDelimiter,
            TextWriter output)
        {
            var propertiesToOmit = new List<string> { "SourceContext", "EventId" };
            if (propertiesToOmit.Any(a => a == name))
                return;

            if (string.Equals(name, "HttpContext", StringComparison.InvariantCultureIgnoreCase))
            {
                if (value is StructureValue sv)
                    foreach (var prop in sv.Properties)
                    {
                        name = DotEscapeFieldName(prop.Name);
                        base.WriteJsonProperty(name, prop.Value, ref precedingDelimiter, output);
                    }
            }
            else
            {
                name = DotEscapeFieldName(name);
                base.WriteJsonProperty(name, value, ref precedingDelimiter, output);
            }
        }

        /// <summary>
        /// Escapes Dots in Strings and does nothing to objects
        /// </summary>
        protected virtual ScalarValue DotEscapeFieldName(ScalarValue value)
        {
            return value.Value is string s ? new ScalarValue(DotEscapeFieldName(s)) : value;
        }

        /// <summary>
        /// Dots are not allowed in Field Names, replaces '.' with '/'
        /// https://github.com/elastic/elasticsearch/issues/14594
        /// </summary>
        protected virtual string DotEscapeFieldName(string value)
        {
            if (value == null) return null;

            return value.Replace('.', '/');
        }

        /// <summary>
        /// Writes out the attached exception
        /// </summary>
        protected override void WriteException(Exception exception, ref string delim, TextWriter output)
        {
            WriteJsonProperty("exception", exception.ToCustomError(), ref delim, output);

            output.Write(delim);
            output.Write("\"");
            output.Write("@exceptionMessage");
            output.Write("\":");
            output.Write($"\"{exception.InnermostMessage()}\"");
        }

        /// <summary>
        /// (Optionally) writes out the rendered message
        /// </summary>
        protected override void WriteRenderedMessage(string message, ref string delim, TextWriter output)
        {
            WriteJsonProperty(RenderedMessagePropertyName, message, ref delim, output);
        }

        /// <summary>
        /// Writes out the message template for the logevent.
        /// </summary>
        protected override void WriteMessageTemplate(string template, ref string delim, TextWriter output)
        {
            WriteJsonProperty(MessageTemplatePropertyName, template, ref delim, output);
        }

        /// <summary>
        /// Writes out the log level
        /// </summary>
        protected override void WriteLevel(LogEventLevel level, ref string delim, TextWriter output)
        {
            var stringLevel = Enum.GetName(typeof(LogEventLevel), level);
            WriteJsonProperty(LevelPropertyName, stringLevel, ref delim, output);
        }

        /// <summary>
        /// Writes out the log timestamp
        /// </summary>
        protected override void WriteTimestamp(DateTimeOffset timestamp, ref string delim, TextWriter output)
        {
            WriteJsonProperty(TimestampPropertyName, timestamp, ref delim, output);
        }

        /// <summary>
        /// Allows a subclass to write out objects that have no configured literal writer.
        /// </summary>
        /// <param name="value">The value to be written as a json construct</param>
        /// <param name="output">The writer to write on</param>
        protected override void WriteLiteralValue(object value, TextWriter output)
        {
            if (_serializer != null)
            {
                string jsonString = _serializer.SerializeToString(value, SerializationFormatting.None);
                output.Write(jsonString);
                return;
            }

            base.WriteLiteralValue(value, output);
        }
    }
}

namespace StartedSerilog.WebUI
{
    public class CustomError
    {
        public string ExceptionType { get; set; }
        public string ModuleName { get; set; }
        public string DeclaringTypeName { get; set; }
        public string TargetSiteName { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public List<CustomDictEntry> Data { get; set; }
        public CustomError InnerError { get; set; }

        public override string ToString()
        {
            return WriteError(new StringBuilder(), "");
        }

        private string WriteError(StringBuilder workInProgress, string prefix)
        {
            workInProgress.AppendLine($"{prefix}ExceptionType: {ExceptionType}");
            workInProgress.AppendLine($"{prefix}Message: {Message}");
            workInProgress.AppendLine($"{prefix}ModuleName: {ModuleName}");
            workInProgress.AppendLine($"{prefix}DeclaringTypeName: {DeclaringTypeName}");
            workInProgress.AppendLine($"{prefix}TargetSiteName: {TargetSiteName}");

            foreach (var item in Data)
                workInProgress.AppendLine($"{prefix}Data-{item.Key}: {item.Value}");

            workInProgress.AppendLine($"{prefix}StackTrace: {StackTrace}");
            if (InnerError != null)
                workInProgress.AppendLine($"{prefix}InnerError: {InnerError.WriteError(workInProgress, $"{prefix}\t")}");

            return workInProgress.ToString();
        }
    }
    public class CustomDictEntry
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
    public static class ExceptionExtensions
    {
        public static CustomError ToCustomError(this Exception ex)
        {
            var rpError = new CustomError
            {
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace,
                Message = ex.Message,
                Data = new List<CustomDictEntry>()
            };

            var ts = ex.TargetSite;
            if (ts != null)
            {
                rpError.ModuleName = ts.Module.Name;
                rpError.DeclaringTypeName = ts.DeclaringType?.Name;
                rpError.TargetSiteName = ts.Name;
            }

            foreach (var dataKey in ex.Data.Keys)
            {
                if (ex.Data[dataKey] != null)
                {
                    rpError.Data.Add(new CustomDictEntry
                    {
                        Key = dataKey.ToString(),
                        Value = ex.Data[dataKey]?.ToString()
                    });
                }
            }

            if (ex.InnerException != null)
                rpError.InnerError = ex.InnerException.ToCustomError();

            return rpError;
        }

        public static string InnermostMessage(this Exception e)
        {
            while (true)
            {
                if (e.InnerException == null) return e.Message;
                e = e.InnerException;
            }
        }
    }
}


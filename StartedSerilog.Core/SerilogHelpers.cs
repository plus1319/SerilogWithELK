﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Enrichers.AspnetcoreHttpcontext;
using Serilog.Filters;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.MSSqlServer;
using StartedSerilog.Core.Formatters;

namespace StartedSerilog.Core
{
    public static class SerilogHelpers
    {
        /// <summary>
        /// Provides standardized, centralized Serilog wire-up for a suite of applications.
        /// </summary>
        /// <param name="loggerConfig">Provide this value from the UseSerilog method param</param>
        /// <param name="provider">Provide this value from the UseSerilog method param as well</param>
        /// <param name="applicationName">Represents the name of YOUR APPLICATION and will be used to segregate your app
        /// from others in the logging sink(s).</param>
        /// <param name="config">IConfiguration settings -- generally read this from appsettings.json</param>
        public static void WithSimpleConfiguration(this LoggerConfiguration loggerConfig,
            IServiceProvider provider, string applicationName, IConfiguration config)
        {
            var name = Assembly.GetEntryAssembly()?.GetName();
            loggerConfig
                .ReadFrom.Configuration(config) // minimum levels defined per project in json files 
                .Enrich.WithAspnetcoreHttpcontext(provider, AddCustomContextDetails)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Assembly", $"{name.Name}")
                .Enrich.WithProperty("Version", $"{name.Version}")
                #region UnComment if you want
                // log into file without formatter
                //.WriteTo.File(new CompactJsonFormatter(),
                //    $@"C:\temp\Logs\{applicationName}.json");

                // if you want use sql server un comment this 
                //notice:create Logging db by yourself
                //.WriteTo.Logger(lc => lc
                //    .Filter.ByIncludingOnly(Matching.WithProperty("ElapsedMilliseconds"))
                //    .WriteTo.MSSqlServer(
                //        connectionString: @"Server=DESKTOP-SEC6MPT\SQLEXPRESS;Database=Logging;Trusted_Connection=True;",
                //        tableName: "PerfLog",
                //        autoCreateSqlTable: true,
                //        columnOptions: GetSqlColumnOptions()))

            #endregion
                .WriteTo.Logger(le => le
                    .Filter.ByIncludingOnly(Matching.WithProperty("ElapsedMilliseconds"))
                    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
                        {
                            AutoRegisterTemplate = true,
                            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                            IndexFormat = "perflog-{0:yyyy.MM.dd}",
                            CustomFormatter = new CustomElasticsearchJsonFormatter(
                                inlineFields: true,
                                renderMessageTemplate: false
                            )
                        }
                    )
                )
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(Matching.WithProperty("UsageName"))
                    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
                    {
                        AutoRegisterTemplate = true,
                        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                        IndexFormat = "usage-{0:yyyy.MM.dd}",
                        CustomFormatter = new CustomElasticsearchJsonFormatter(
                            inlineFields: true,
                            renderMessageTemplate: false
                        )
                    })
                )
                .WriteTo.Logger(lc => lc
                    .Filter.ByExcluding(Matching.WithProperty("ElapsedMilliseconds"))
                    .Filter.ByExcluding(Matching.WithProperty("UsageName"))
                    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
                    {
                        AutoRegisterTemplate = true,
                        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                        IndexFormat = "error-{0:yyyy.MM.dd}",
                        CustomFormatter = new CustomElasticsearchJsonFormatter(
                            inlineFields: true,
                            renderMessageTemplate: false
                        )
                    }));
            //custom log entry formatter
            //.WriteTo.File(new CustomLogEntryFormatter(), $@"c:\temp\logs\{applicationName}-error.json"));
        }

        private static ColumnOptions GetSqlColumnOptions()
        {
            var options = new ColumnOptions();
            options.Store.Remove(StandardColumn.Message);
            options.Store.Remove(StandardColumn.MessageTemplate);
            options.Store.Remove(StandardColumn.Level);
            options.Store.Remove(StandardColumn.Exception);

            options.Store.Remove(StandardColumn.Properties);
            options.Store.Add(StandardColumn.LogEvent);
            options.LogEvent.ExcludeStandardColumns = true;
            options.LogEvent.ExcludeAdditionalProperties = true;

            options.AdditionalColumns = new Collection<SqlColumn>
            {
                new SqlColumn
                { ColumnName = "PerfItem", AllowNull = false,
                    DataType = SqlDbType.NVarChar, DataLength = 100,
                    NonClusteredIndex = true },
                new SqlColumn
                {
                    ColumnName = "ElapsedMilliseconds", AllowNull = false,
                    DataType = SqlDbType.Int
                },
                new SqlColumn
                {
                    ColumnName = "ActionName", AllowNull = false
                },
                new SqlColumn
                {
                    ColumnName = "MachineName", AllowNull = false
                }
            };

            return options;
        }

        private static UserInfo AddCustomContextDetails(IHttpContextAccessor ctx)
        {
            var excluded = new List<string> { "nbf", "exp", "auth_time", "amr", "sub" };
            const string userIdClaimType = "sub";

            var context = ctx.HttpContext;
            var user = context?.User.Identity;
            if (user == null || !user.IsAuthenticated) return null;

            var userId = context.User.Claims.FirstOrDefault(a => a.Type == userIdClaimType)?.Value;
            var userInfo = new UserInfo
            {
                UserName = user.Name,
                UserId = userId,
                UserClaims = new Dictionary<string, List<string>>()
            };
            foreach (var distinctClaimType in context.User.Claims
                .Where(a => excluded.All(ex => ex != a.Type))
                .Select(a => a.Type)
                .Distinct())
            {
                userInfo.UserClaims[distinctClaimType] = context.User.Claims
                    .Where(a => a.Type == distinctClaimType)
                    .Select(c => c.Value)
                    .ToList();
            }

            return userInfo;
        }
    }
}
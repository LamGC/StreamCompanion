﻿using StreamCompanionTypes.DataTypes;
using StreamCompanionTypes.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using StreamCompanionTypes.Interfaces.Consumers;
using StreamCompanionTypes.Interfaces.Services;

namespace FileMapDataSender
{
    [SupportedOSPlatform("windows")]
    public class FileMapDataSender : IPlugin, IMapDataConsumer, IDisposable, IHighFrequencyDataConsumer
    {
        public bool Started { get; set; }
        private readonly FileMapManager _fileMapManager;

        public string Description { get; } = "";
        public string Name { get; } = "FileMapDataSender";
        public string Author { get; } = "Piotrekol";
        public string Url { get; } = "";
        public string UpdateUrl { get; } = "";

        public FileMapDataSender(ILogger logger)
        {
            _fileMapManager = new FileMapManager(logger);
        }
        public void Save(string fileName, string content)
        {
            _fileMapManager.Write(fileName, content);
        }

        public Task SetNewMapAsync(IMapSearchResult map, CancellationToken cancellationToken)
        {
            var ingamePatterns = new List<string>();
            foreach (var s in map.OutputPatterns)
            {
                //TODO: export ingameOverlay(showInOsu) stuff out of there
                var name = s.Name;
                if (s.ShowInOsu)
                {
                    ingamePatterns.Add(name);
                }
                string valueToWrite = (s.SaveEvent & map.Action) != 0 ? s.GetFormatedPattern() : "    ";
                if (s.ShowInOsu)
                {
                    var configName = "conf-" + name;
                    var valueName = "value-" + name;
                    var config = $"{s.XPosition} {s.YPosition} {s.Color.R} {s.Color.G} {s.Color.B} {s.Color.A} {s.FontName.Replace(' ', '/')} {s.FontSize} {s.Aligment}";
                    _fileMapManager.Write(configName, config);
                    if (!s.IsMemoryFormat)
                        _fileMapManager.Write(valueName, valueToWrite.Replace("\r", ""));
                }

                if (!s.IsMemoryFormat)
                    _fileMapManager.Write(name, valueToWrite);
            }

            _fileMapManager.Write("Sc-ingamePatterns", string.Join(" ", ingamePatterns) + " ");

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _fileMapManager.Write("Sc-ingamePatterns", " ");
        }

        public void Handle(string content)
        {
            //ignored
        }

        public void Handle(string name, string content)
        {
            _fileMapManager.Write(name, content);
        }
    }
}
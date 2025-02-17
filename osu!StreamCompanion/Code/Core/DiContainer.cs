using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Windows.Forms;
using Grace.DependencyInjection;
using osu_StreamCompanion.Code.Core.Loggers;
using osu_StreamCompanion.Code.Core.Maps.Processing;
using osu_StreamCompanion.Code.Core.Savers;
using osu_StreamCompanion.Code.Helpers;
using osu_StreamCompanion.Code.Misc;
using osu_StreamCompanion.Code.Modules.Updater;
using osu_StreamCompanion.Code.Windows;
using StreamCompanionTypes.DataTypes;
using StreamCompanionTypes.Interfaces;
using StreamCompanionTypes.Enums;
using StreamCompanionTypes.Interfaces.Services;

namespace osu_StreamCompanion.Code.Core
{
    internal static class DiContainer
    {
        static DiContainer()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        private static string PluginsLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        private static string AdditionalDllsLocation = Path.Combine(PluginsLocation, "Dlls");
        private static string[] customProbingPaths = {PluginsLocation, AdditionalDllsLocation};
        public static DependencyInjectionContainer Container => LazyContainer.Value;
        private static Lazy<DependencyInjectionContainer> LazyContainer = new Lazy<DependencyInjectionContainer>(() =>
        {
            var di = new DependencyInjectionContainer();
            di.Configure(x => x.ExportFactory(() => MainLogger.Instance));
            di.Configure(x => x.ExportFactory((StaticInjectionContext context) =>
                {
                    var pluginName = context.TargetInfo.InjectionType?.Name;
                    if (pluginName == null)
                        return MainLogger.Instance;

                    return (IContextAwareLogger)new PluginLogger(MainLogger.Instance, pluginName);
                })
                    .As<ILogger>().As<IContextAwareLogger>());

            di.Configure(x => x.ExportDefault(typeof(Settings)));
            di.Configure(x => x.ExportDefault(typeof(MainWindowUpdater)));
            di.Configure(x => x.ExportDefault(typeof(MainSaver)));
            di.Configure(x => x.ExportDefault(typeof(OsuEventHandler)));
            di.Configure(x => x.ExportDefault(typeof(MainMapDataGetter)));
            di.Configure(c => c.ImportMembers<object>(MembersThat.HaveAttribute<ImportAttribute>()));
            di.Configure(x => x.ExportFuncWithContext<Delegates.Exit>((scope, context, arg3) =>
              {
                  var logger = scope.Locate<IContextAwareLogger>();
                  var isModule = context.TargetInfo.InjectionType.GetInterfaces().Contains(typeof(IModule));
                  if (isModule)
                  {
                      return reason =>
                      {
                          logger.SetContextData("exiting", "Yes - from module");
                          logger.Log("StreamCompanion is shutting down", LogLevel.Information);
                          Program.SafeQuit();
                      };
                  }

                  return o =>
                  {
                      string reason = string.Empty;
                      try
                      {
                          reason = o.ToString();
                      }
                      catch
                      {
                          reason = "Plugin provided invalid reason object";
                      }

                      logger.Log("Plugin {0} has requested StreamCompanion shutdown! due to: {1}", LogLevel.Information,
                          context.TargetInfo.InjectionType.FullName, reason);
                      logger.SetContextData("exiting", $"Yes - plugin:{context.TargetInfo.InjectionType.FullName}, with reason:{reason}");
                      Program.SafeQuit();
                  };
              }));
            di.Configure(x => x.ExportFuncWithContext<Delegates.Restart>((scope, context, arg3) =>
            {
                var logger = scope.Locate<IContextAwareLogger>();
                var isModule = context.TargetInfo.InjectionType.GetInterfaces().Contains(typeof(IModule));
                if (isModule)
                {
                    return reason =>
                    {
                        logger.SetContextData("restarting", "from module");
                        logger.Log("StreamCompanion is restarting", LogLevel.Information);
                        Process.Start(Updater.UpdaterExeName, Process.GetCurrentProcess().Id.ToString());
                        Program.SafeQuit();
                    };
                }

                return o =>
                {
                    string reason = string.Empty;
                    try
                    {
                        reason = o.ToString();
                    }
                    catch
                    {
                    }

                    logger.Log("Plugin {0} has requested StreamCompanion restart! due to: {1}", LogLevel.Information,
                        context.TargetInfo.InjectionType.FullName, reason);
                    logger.SetContextData("restarting", $"plugin:{context.TargetInfo.InjectionType.FullName}, with reason:{reason}");

                    Process.Start(Updater.UpdaterExeName, Process.GetCurrentProcess().Id.ToString());
                    Program.SafeQuit();
                };
            }));
            //Register all IModules from current assembly
            var modules = GetTypes<IModule>(Assembly.GetExecutingAssembly());
            foreach (var module in modules)
            {
                di.Configure(x => x.ExportDefault(module));
            }

            RegisterPlugins(di, di.Locate<ILogger>());

            return di;
        });
        public static void ExportDefault(this IExportRegistrationBlock e, Type type)
        {
            e.Export(type).ByInterfaces().As(type).Lifestyle.Singleton();
        }

        private static List<Assembly> GetAssemblies(IEnumerable<string> fileList, ILogger logger)
        {
            var assemblies = new List<Assembly>();
            foreach (var file in fileList)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var assemblyName = new AssemblyName(fileName);
                    assemblies.Add(AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName));
                }
                catch (BadImageFormatException e)
                {
                    e.Data.Add("PluginsLocation", PluginsLocation);
                    e.Data.Add("file", file);
                    e.Data.Add("netFramework", GetDotNetVersion.Get45PlusFromRegistry());
                    logger?.Log(e, LogLevel.Error);
                }
                catch (COMException)
                {
                    if (file.Contains("osuOverlayPlugin"))
                    {
                        var nl = Environment.NewLine;
                        MessageBox.Show("Since SC version 190426.18, osu! overlay plugin started being falsely detected as virus" + nl + nl
                                        + "If you don't use it it is advised to just remove it from SC plugins folder (search for osuOverlayPlugin.dll and osuOverlay.dll inside SC folder)." + nl + nl
                                        + "However if you do use it, add these to your antivirus exceptions." + nl + nl + nl

                                        + "osu! overlay will NOT be loaded until you resolve this manually.", "StreamCompanion - WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show(
                        "StreamCompanion could not load any of the plugins because of not enough permissions." + Environment.NewLine + Environment.NewLine
                        + "Please reinstall StreamCompanion.", "StreamCompanion - ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Program.SafeQuit();
                }
                catch (FileLoadException e)
                {
                    MessageBox.Show($"Plugin \"{fileName}\" could not get loaded. StreamCompanion will continue to work, however, some features might be missing." +
                                Environment.NewLine + Environment.NewLine + "Error:" +
                                Environment.NewLine + e,
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return assemblies;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = args.Name.Split(",", StringSplitOptions.TrimEntries)[0];

            foreach (var probingPath in customProbingPaths)
            {
                var filePath = Path.Combine(probingPath, $"{name}.dll");
                if (File.Exists(filePath))
                {
                    return Assembly.LoadFrom(filePath);
                }
            }

            return null;
        }

        private static List<Type> GetTypes<T>(Assembly asm)
        {
            List<Type> plugins = new List<Type>();
            List<Type> types = new List<Type>();

            try
            {
                types = asm.GetTypes().ToList();
            }
            catch (ReflectionTypeLoadException e)
            {
                var dllName = asm.ManifestModule.Name;
                MessageBox.Show($"Plugin \"{dllName}\" could not get loaded. StreamCompanion will continue to work, however, some features might be missing." +
                                Environment.NewLine + Environment.NewLine + "Errors:" +
                                Environment.NewLine +
                                string.Join(Environment.NewLine, e.LoaderExceptions.Select(x => $"{x.GetType()}: {x.Message}")),
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            foreach (var type in types)
            {
                if (type.GetInterfaces().Contains(typeof(T)))
                {
                    if (!type.IsAbstract)
                    {
                        plugins.Add(type);
                    }
                }
            }

            return plugins;
        }
        private static void RegisterPlugins(DependencyInjectionContainer di, ILogger logger)
        {
            var pluginAssemblies = GetAssemblies(Directory.GetFiles(PluginsLocation, "*.dll"), logger);
            foreach (var asm in pluginAssemblies)
            {
                var plugins = GetTypes<IPlugin>(asm);
                foreach (var plugin in plugins)
                {
                    di.Configure(x => x.ExportDefault(plugin));
                }
            }
        }
    }
}
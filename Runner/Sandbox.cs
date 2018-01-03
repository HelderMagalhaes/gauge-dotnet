// Copyright 2015 ThoughtWorks, Inc.
//
// This file is part of Gauge-CSharp.
//
// Gauge-CSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Gauge-CSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Gauge-CSharp.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using Gauge.CSharp.Lib;
using Gauge.CSharp.Lib.Attribute;
using Gauge.CSharp.Runner.Converters;
using Gauge.CSharp.Runner.Extensions;
using Gauge.CSharp.Runner.Models;
using Gauge.CSharp.Runner.Strategy;
using NLog;

namespace Gauge.CSharp.Runner
{
    [Serializable]
    public class Sandbox : ISandbox
    {
        private readonly IAssemblyLoader _assemblyLoader;

        private IClassInstanceManager _classInstanceManager;

        private IHookRegistry _hookRegistry;

        public Sandbox(IAssemblyLoader assemblyLoader, IHookRegistry hookRegistry)
        {
            LogConfiguration.Initialize();
            _assemblyLoader = assemblyLoader;
            _hookRegistry = hookRegistry;
            ScanCustomScreenGrabber();
            LoadClassInstanceManager();
        }

        public Sandbox() : this(new AssemblyLoader(), null)
        {
        }

        private Type ScreenGrabberType { get; set; }

        private IDictionary<string, MethodInfo> MethodMap { get; set; }

        [DebuggerStepperBoundary]
        [DebuggerHidden]
        public ExecutionResult ExecuteMethod(GaugeMethod gaugeMethod, params string[] args)
        {
            var method = MethodMap[gaugeMethod.Name];
            var executionResult = new ExecutionResult {Success = true};
            var logger = LogManager.GetLogger("Sandbox");
            try
            {
                var parameters = args.Select(o =>
                {
                    try
                    {
                        return GetTable(o);
                    }
                    catch
                    {
                        return o;
                    }
                }).ToArray();
                logger.Debug("Executing method: {0}", method.Name);
                Execute(method, StringParamConverter.TryConvertParams(method, parameters));
            }
            catch (Exception ex)
            {
                logger.Debug("Error executing {0}", method.Name);
                var innerException = ex.InnerException ?? ex;
                executionResult.ExceptionMessage = innerException.Message;
                executionResult.StackTrace = innerException is AggregateException
                    ? innerException.ToString()
                    : innerException.StackTrace;
                executionResult.Source = innerException.Source;
                executionResult.Success = false;
                executionResult.Recoverable = gaugeMethod.ContinueOnFailure;
            }

            return executionResult;
        }

        public List<GaugeMethod> GetStepMethods()
        {
            var infos = _assemblyLoader.GetMethods(typeof(Step).FullName);
            MethodMap = new Dictionary<string, MethodInfo>();
            foreach (var info in infos)
            {
                var methodId = info.FullyQuallifiedName();
                MethodMap.Add(methodId, info);
                LogManager.GetLogger("Sandbox").Debug("Scanned and caching Gauge Step: {0}, Recoverable: {1}", methodId,
                    info.IsRecoverableStep());
            }
            return MethodMap.Keys.Select(s =>
            {
                var method = MethodMap[s];
                return new GaugeMethod
                {
                    Name = s,
                    ParameterCount = method.GetParameters().Length,
                    ContinueOnFailure = method.IsRecoverableStep()
                };
            }).ToList();
        }

        public List<string> GetAllStepTexts()
        {
            return GetStepMethods().SelectMany(GetStepTexts).ToList();
        }

        public void InitializeDataStore(string dataStoreType)
        {
            switch (dataStoreType)
            {
                case "Suite":
                    DataStoreFactory.InitializeSuiteDataStore();
                    break;
                case "Spec":
                    DataStoreFactory.InitializeSpecDataStore();
                    break;
                case "Scenario":
                    DataStoreFactory.InitializeScenarioDataStore();
                    break;
            }
        }

        public IEnumerable<string> GetStepTexts(GaugeMethod gaugeMethod)
        {
            var stepMethod = MethodMap[gaugeMethod.Name];
            Step step = stepMethod.GetCustomAttributes<Step>().FirstOrDefault();
            return step?.Names;
        }

        public bool TryScreenCapture(out byte[] screenShotBytes)
        {
            try
            {
                var instance = Activator.CreateInstance(ScreenGrabberType);
                if (instance != null)
                {
                    var screenCaptureMethod = ScreenGrabberType.GetMethod("TakeScreenShot");
                    screenShotBytes = screenCaptureMethod.Invoke(instance, null) as byte[];
                    return true;
                }
            }
            catch
            {
                //do nothing, return
            }

            screenShotBytes = null;
            return false;
        }

        public void ClearObjectCache()
        {
            _classInstanceManager.ClearCache();
        }

        public void StartExecutionScope(string tag)
        {
            _classInstanceManager.StartScope(tag);
        }

        public void CloseExectionScope()
        {
            _classInstanceManager.CloseScope();
        }

        [DebuggerStepperBoundary]
        [DebuggerHidden]
        public ExecutionResult ExecuteHooks(string hookType, IHooksStrategy strategy, IList<string> applicableTags)
        {
            var methods = GetHookMethods(hookType, strategy, applicableTags);
            var executionResult = new ExecutionResult
            {
                Success = true
            };
            foreach (var method in methods)
            {
                var methodInfo = _hookRegistry.MethodFor(method);
                try
                {
                    ExecuteHook(methodInfo);
                }
                catch (Exception ex)
                {
                    LogManager.GetLogger("Sandbox").Debug("{0} Hook execution failed : {1}.{2}", hookType,
                        methodInfo.DeclaringType.FullName, methodInfo.Name);
                    var innerException = ex.InnerException ?? ex;
                    executionResult.ExceptionMessage = innerException.Message;
                    executionResult.StackTrace = innerException.StackTrace;
                    executionResult.Source = innerException.Source;
                    executionResult.Success = false;
                }
            }
            return executionResult;
        }

        public IEnumerable<string> Refactor(GaugeMethod methodInfo, IList<Tuple<int, int>> parameterPositions,
            IList<string> parametersList, string newStepValue)
        {
            return RefactorHelper.Refactor(MethodMap[methodInfo.Name], parameterPositions, parametersList,
                newStepValue);
        }

        private object GetTable(string jsonString)
        {
            var serializer = new DataContractJsonSerializer(typeof(Table));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
            {
                return serializer.ReadObject(ms);
            }
        }

        [DebuggerHidden]
        private void ExecuteHook(MethodInfo method, params object[] objects)
        {
            if (HasArguments(method, objects))
                Execute(method, objects);
            else
                Execute(method);
        }

        private static bool HasArguments(MethodInfo method, object[] args)
        {
            if (method.GetParameters().Length != args.Length)
                return false;
            for (var i = 0; i < args.Length; i++)
                if (args[i].GetType() != method.GetParameters()[i].ParameterType)
                    return false;
            return true;
        }

        private IEnumerable<string> GetHookMethods(string hookType, IHooksStrategy strategy,
            IEnumerable<string> applicableTags)
        {
            var hooksFromRegistry = GetHooksFromRegistry(hookType);
            return strategy.GetApplicableHooks(applicableTags, hooksFromRegistry);
        }

        private IEnumerable<IHookMethod> GetHooksFromRegistry(string hookType)
        {
            _hookRegistry = _hookRegistry ?? new HookRegistry(_assemblyLoader);
            switch (hookType)
            {
                case "BeforeSuite":
                    return _hookRegistry.BeforeSuiteHooks;
                case "BeforeSpec":
                    return _hookRegistry.BeforeSpecHooks;
                case "BeforeScenario":
                    return _hookRegistry.BeforeScenarioHooks;
                case "BeforeStep":
                    return _hookRegistry.BeforeStepHooks;
                case "AfterStep":
                    return _hookRegistry.AfterStepHooks;
                case "AfterScenario":
                    return _hookRegistry.AfterScenarioHooks;
                case "AfterSpec":
                    return _hookRegistry.AfterSpecHooks;
                case "AfterSuite":
                    return _hookRegistry.AfterSuiteHooks;
                default:
                    return null;
            }
        }

        private void ScanCustomScreenGrabber()
        {
            ScreenGrabberType = _assemblyLoader.ScreengrabberTypes.FirstOrDefault();
            var logger = LogManager.GetLogger("Sandbox");
            if (ScreenGrabberType != null)
            {
                logger.Debug("Custom ScreenGrabber found : {0}", ScreenGrabberType.FullName);
            }
            else
            {
                logger.Debug("No implementation of IScreenGrabber found. Using DefaultScreenGrabber");
                ScreenGrabberType = typeof(DefaultScreenGrabber);
            }
        }

        private void Execute(MethodBase method, params object[] parameters)
        {
            var typeToLoad = method.DeclaringType;
            var instance = _classInstanceManager.Get(typeToLoad);
            var logger = LogManager.GetLogger("Sandbox");
            if (instance == null)
            {
                var error = "Could not load instance type: " + typeToLoad;
                logger.Error(error);
                throw new Exception(error);
            }
            method.Invoke(instance, parameters);
        }

        private void LoadClassInstanceManager()
        {
            var instanceManagerType = _assemblyLoader.ClassInstanceManagerTypes.FirstOrDefault();

            var logger = LogManager.GetLogger("Sandbox");
            if (instanceManagerType != null)
            {
                logger.Debug("Loading : {0}", instanceManagerType.FullName);
                _classInstanceManager = Activator.CreateInstance(instanceManagerType) as IClassInstanceManager;
            }
            _classInstanceManager = _classInstanceManager ?? new DefaultClassInstanceManager();
            logger.Debug("Loaded Instance Manager of Type:" + _classInstanceManager.GetType().FullName);
            _classInstanceManager.Initialize(_assemblyLoader.AssembliesReferencingGaugeLib);
        }
    }
}
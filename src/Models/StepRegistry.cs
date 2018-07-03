﻿// Copyright 2018 ThoughtWorks, Inc.
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
using System.Linq;

namespace Gauge.Dotnet.Models
{
    [Serializable]
    public class StepRegistry : IStepRegistry
    {
        private Dictionary<string, List<GaugeMethod>> _registry;

        public StepRegistry()
        {
            _registry = new Dictionary<string, List<GaugeMethod>>();
        }

        public IEnumerable<string> GetStepTexts()
        {
            return _registry.Values.SelectMany(methods => methods.Select(method => method.StepText));
        }

        public void AddStep(string stepValue, GaugeMethod method)
        {
            if (!_registry.ContainsKey(stepValue)) _registry.Add(stepValue, new List<GaugeMethod>());
            _registry.GetValueOrDefault(stepValue).Add(method);
        }

        public void RemoveSteps(string filepath)
        {
            var newRegistry = new Dictionary<string, List<GaugeMethod>>();
            foreach (var (key, gaugeMethods) in _registry)
            {
                var methods = gaugeMethods.Where(method => !filepath.Equals(method.FileName)).ToList();
                if (methods.Count > 0) newRegistry[key] = methods;
            }
            _registry = newRegistry;
        }


        public bool ContainsStep(string parsedStepText)
        {
            return _registry.ContainsKey(parsedStepText);
        }

        public bool HasMultipleImplementations(string parsedStepText)
        {
            return _registry[parsedStepText].Count > 1;
        }

        public GaugeMethod MethodFor(string parsedStepText)
        {
            return _registry[parsedStepText][0];
        }

        public bool HasAlias(string stepValue)
        {
            return _registry.ContainsKey(stepValue) && _registry.GetValueOrDefault(stepValue).FirstOrDefault().IsAlias;
        }

        public string GetStepText(string stepValue)
        {
            return _registry.ContainsKey(stepValue) ? _registry[stepValue][0].StepText : string.Empty;
        }

        public void Clear()
        {
            _registry = new Dictionary<string, List<GaugeMethod>>();
        }


        public IEnumerable<string> AllSteps()
        {
            return _registry.Keys;
        }
    }
}
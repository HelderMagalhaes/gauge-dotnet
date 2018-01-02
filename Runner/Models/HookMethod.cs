﻿// Copyright 2015 ThoughtWorks, Inc.
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
using System.Reflection;
using Gauge.CSharp.Lib.Attribute;
using Gauge.CSharp.Runner.Extensions;

namespace Gauge.CSharp.Runner.Models
{
    [Serializable]
    public class HookMethod : IHookMethod
    {
        public HookMethod(Type hookType, MethodInfo methodInfo)
        {
            Method = methodInfo.FullyQuallifiedName();
            FilterTags = Enumerable.Empty<string>();

            if (!hookType.IsSubclassOf(typeof(FilteredHookAttribute)))
                return;

            FilteredHookAttribute filteredHookAttribute = methodInfo.GetCustomAttribute(hookType) as FilteredHookAttribute;
            if (filteredHookAttribute == null) return;

            FilterTags = filteredHookAttribute.FilterTags;
            var targetTagBehaviourType = typeof(TagAggregationBehaviourAttribute);
            TagAggregationBehaviourAttribute tagAggregationBehaviourAttribute = methodInfo.GetCustomAttribute(targetTagBehaviourType) as TagAggregationBehaviourAttribute;

            TagAggregation = tagAggregationBehaviourAttribute != null ? tagAggregationBehaviourAttribute.TagAggregation : TagAggregation.And;
        }

        public TagAggregation TagAggregation { get; }

        public IEnumerable<string> FilterTags { get; }

        public string Method { get; }
    }
}
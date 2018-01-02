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

using System.Linq;
using Gauge.CSharp.Runner.Converters;
using Gauge.Messages;
using Xunit;

namespace Gauge.CSharp.Runner.UnitTests.Converter
{
    public class StringParamConverterTests
    {
        private class TestTypeConversion
        {
            public void Int(int i)
            {
            }

            public void Float(float j)
            {
            }

            public void Bool(bool b)
            {
            }

            public void String(string s)
            {
            }
        }

        [Fact]
        public void ShouldConvertFromParameterToString()
        {
            const string expected = "Foo";
            var parameter = new Parameter
            {
                ParameterType = Parameter.Types.ParameterType.Static,
                Value = expected
            };

            var actual = new StringParamConverter().Convert(parameter);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldTryToConvertStringParameterToBool()
        {
            var type = new TestTypeConversion().GetType();
            var method = type.GetMethod("Bool");

            var getParams = StringParamConverter.TryConvertParams(method, new object[] {"false"});
            Assert.Equal(typeof(bool), getParams.First().GetType());
        }

        [Fact]
        public void ShouldTryToConvertStringParameterToFloat()
        {
            var type = new TestTypeConversion().GetType();
            var method = type.GetMethod("Float");

            var getParams = StringParamConverter.TryConvertParams(method, new object[] {"3.1412"});
            Assert.Equal(typeof(float), getParams.First().GetType());
        }

        [Fact]
        public void ShouldTryToConvertStringParameterToInt()
        {
            var type = new TestTypeConversion().GetType();
            var method = type.GetMethod("Int");

            var getParams = StringParamConverter.TryConvertParams(method, new object[] {"1"});
            Assert.Equal(typeof(int), getParams.First().GetType());
        }

        [Fact]
        public void ShouldTryToConvertStringParameterToString()
        {
            var type = new TestTypeConversion().GetType();
            var method = type.GetMethod("Int");

            var getParams = StringParamConverter.TryConvertParams(method, new object[] {"hahaha"});
            Assert.Equal(typeof(string), getParams.First().GetType());
        }
    }
}
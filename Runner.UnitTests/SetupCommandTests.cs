﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NuGet;
using Xunit;

namespace Gauge.CSharp.Runner.UnitTests
{
    public class SetupCommandTests
    {
        public SetupCommandTests()
        {
            Environment.SetEnvironmentVariable("GAUGE_PROJECT_ROOT", Directory.GetCurrentDirectory());
            _packageRepositoryFactory = new Mock<IPackageRepositoryFactory>();
            var packageRepository = new Mock<IPackageRepository>();
            var package = new Mock<IPackage>();
            package.Setup(p => p.Id).Returns("Gauge.CSharp.Lib");
            var list = new List<IPackage> {package.Object};
            package.Setup(p => p.Version).Returns(new SemanticVersion(Version));
            packageRepository.Setup(repository => repository.GetPackages()).Returns(list.AsQueryable());
            _packageRepositoryFactory.Setup(factory => factory.CreateRepository(SetupCommand.NugetEndpoint))
                .Returns(packageRepository.Object);
        }

        ~SetupCommandTests()
        {
            Environment.SetEnvironmentVariable("GAUGE_PROJECT_ROOT", null);
        }

        private const string Version = "0.5.2";
        private Mock<IPackageRepositoryFactory> _packageRepositoryFactory;

        [Fact]
        public void ShouldFetchMaxLibVersionOnlyOnce()
        {
            var setupCommand = new SetupCommand(_packageRepositoryFactory.Object);
            var maxLibVersion = setupCommand.MaxLibVersion;

            maxLibVersion = setupCommand.MaxLibVersion; // call again, just for fun!

            Assert.Equal(Version, maxLibVersion.ToString());
            _packageRepositoryFactory.Verify(factory => factory.CreateRepository(SetupCommand.NugetEndpoint),
                Times.Once);
        }
    }
}
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using SourceGenerators.Tests;
using Xunit;

namespace Microsoft.Extensions.Configuration.Binder.SourceGeneration.Tests
{
#if NETCOREAPP
    [ActiveIssue("https://github.com/dotnet/runtime/issues/52062", TestPlatforms.Browser)]
    public class ConfingurationBindingSourceGeneratorTests
    {
        private const string BindCallSampleCode = @"
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

public class Program
{
	public static void Main()
	{
		ConfigurationBuilder configurationBuilder = new();
		IConfigurationRoot config = configurationBuilder.Build();

		MyClass options = new();
		config.Bind(options);
	}
	
	public class MyClass
	{
		public string MyString { get; set; }
		public int MyInt { get; set; }
		public List<int> MyList { get; set; }
		public Dictionary<string, string> MyDictionary { get; set; }
        public Dictionary<string, MyClass2> MyComplexDictionary { get; set; }
	}

    public class MyClass2 { }
}";

        [Theory]
        [InlineData(LanguageVersion.Preview)]
        [InlineData(LanguageVersion.CSharp11)]
        public async Task TestBaseline_TestBindCallGen(LanguageVersion langVersion) =>
            await VerifyAgainstBaselineUsingFile("TestBindCallGen.generated.txt", BindCallSampleCode, langVersion);

        [Fact]
        public async Task TestBaseline_TestGetCallGen()
        {
            string testSourceCode = @"
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

public class Program
{
	public static void Main()
	{
		ConfigurationBuilder configurationBuilder = new();
		IConfigurationRoot config = configurationBuilder.Build();

		MyClass options = config.Get<MyClass>();
	}
	
	public class MyClass
	{
		public string MyString { get; set; }
		public int MyInt { get; set; }
		public List<int> MyList { get; set; }
		public Dictionary<string, string> MyDictionary { get; set; }
	}
}";

            await VerifyAgainstBaselineUsingFile("TestGetCallGen.generated.txt", testSourceCode);
        }

        [Fact]
        public async Task TestBaseline_TestConfigureCallGen()
        {
            string testSourceCode = @"
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
	public static void Main()
	{
        ConfigurationBuilder configurationBuilder = new();
		IConfiguration config = configurationBuilder.Build();
        IConfigurationSection section = config.GetSection(""MySection"");

		ServiceCollection services = new();
        services.Configure<MyClass>(section);
	}
	
	public class MyClass
	{
		public string MyString { get; set; }
		public int MyInt { get; set; }
		public List<int> MyList { get; set; }
		public Dictionary<string, string> MyDictionary { get; set; }
	}
}";

            await VerifyAgainstBaselineUsingFile("TestConfigureCallGen.generated.txt", testSourceCode);
        }

        [Fact]
        public async Task LangVersionMustBeCharp11OrHigher()
        {
            var (d, r) = await RunGenerator(BindCallSampleCode, LanguageVersion.CSharp10);
            Assert.Empty(r);

            Diagnostic diagnostic = Assert.Single(d);
            Assert.True(diagnostic.Id == "SYSLIB1102");
            Assert.Contains("C# 11", diagnostic.Descriptor.Title.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        }

        private async Task VerifyAgainstBaselineUsingFile(
            string filename,
            string testSourceCode,
            LanguageVersion languageVersion = LanguageVersion.Preview)
        {
            string baseline = LineEndingsHelper.Normalize(await File.ReadAllTextAsync(Path.Combine("Baselines", filename)).ConfigureAwait(false));
            string[] expectedLines = baseline.Replace("%VERSION%", typeof(ConfigurationBindingSourceGenerator).Assembly.GetName().Version?.ToString())
                                             .Split(Environment.NewLine);

            var (d, r) = await RunGenerator(testSourceCode, languageVersion);

            Assert.Empty(d);
            Assert.Single(r);

            Assert.True(RoslynTestUtils.CompareLines(expectedLines, r[0].SourceText,
                out string errorMessage), errorMessage);
        }

        private async Task<(ImmutableArray<Diagnostic>, ImmutableArray<GeneratedSourceResult>)> RunGenerator(
            string testSourceCode,
            LanguageVersion langVersion = LanguageVersion.CSharp11) =>
            await RoslynTestUtils.RunGenerator(
                new ConfigurationBindingSourceGenerator(),
                new[] {
                    typeof(ConfigurationBinder).Assembly,
                    typeof(IConfiguration).Assembly,
                    typeof(IServiceCollection).Assembly,
                    typeof(IDictionary).Assembly,
                    typeof(ServiceCollection).Assembly,
                    typeof(OptionsConfigurationServiceCollectionExtensions).Assembly,
                },
                new[] { testSourceCode },
                langVersion: langVersion).ConfigureAwait(false);
    }
#endif
}

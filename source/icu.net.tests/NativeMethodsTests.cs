// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Icu.Tests
{
#if NETCOREAPP2_1
	[Ignore("Platform is not supported in NUnit for .NET Core 2")]
#else
	[Platform(Exclude = "Linux",
		Reason = "These tests require ICU4C installed from NuGet packages which isn't available on Linux")]
#endif
	[TestFixture]
	public class NativeMethodsTests
	{
		private string _tmpDir;
		private string _pathEnvironmentVariable;
		private const string FullIcuLibraryVersion = "62.1";
		private const string MinIcuLibraryVersion = "59.1";
		private const string MinIcuLibraryVersionMajor = "59";

		private static void CopyFile(string srcPath, string dstDir)
		{
			var fileName = Path.GetFileName(srcPath);
			File.Copy(srcPath, Path.Combine(dstDir, fileName));
		}

		private static bool IsRunning64Bit => IntPtr.Size == 8;

		private static string GetArchSubdir(string prefix = "")
		{
			var archSubdir = IsRunning64Bit ? "x64" : "x86";
			return $"{prefix}{archSubdir}";
		}

		private static string OutputDirectory => Path.GetDirectoryName(
			new Uri(
#if NET40
				typeof(NativeMethodsTests).Assembly.CodeBase
#else
				typeof(NativeMethodsTests).GetTypeInfo().Assembly.CodeBase
#endif
				)
			.LocalPath);

		private static string IcuDirectory => Path.Combine(OutputDirectory, "lib", GetArchSubdir("win-"));

		private string RunTestHelper(string workDir, string exeDir = null)
		{
			if (string.IsNullOrEmpty(exeDir))
				exeDir = _tmpDir;

			using (var process = new Process())
			{
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.WorkingDirectory = workDir;
				var filename = Path.Combine(exeDir, "TestHelper.exe");
				if (!File.Exists(filename))
				{
					// netcore
					process.StartInfo.Arguments = Path.Combine(exeDir, "TestHelper.dll");
					filename = "dotnet";
				}

				process.StartInfo.FileName = filename;

				process.Start();
				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					Console.WriteLine(process.StandardError.ReadToEnd());
				}
				return output.TrimEnd('\r', '\n');
			}
		}

		private static void CopyMinimalIcuFiles(string targetDir)
		{
			CopyFile(Path.Combine(IcuDirectory, $"icudt{MinIcuLibraryVersionMajor}.dll"), targetDir);
			CopyFile(Path.Combine(IcuDirectory, $"icuin{MinIcuLibraryVersionMajor}.dll"), targetDir);
			CopyFile(Path.Combine(IcuDirectory, $"icuuc{MinIcuLibraryVersionMajor}.dll"), targetDir);
		}

		private static void CopyTestFiles(string sourceDir, string targetDir)
		{
			var testHelper = Path.Combine(sourceDir, "TestHelper.exe");
			if (!File.Exists(testHelper))
				testHelper = Path.Combine(sourceDir, "TestHelper.dll");
			CopyFile(testHelper, targetDir);
			CopyFile(Path.Combine(sourceDir, "icu.net.dll"), targetDir);
			var dependencyModel = Path.Combine(sourceDir, "Microsoft.Extensions.DependencyModel.dll");
			if (File.Exists(dependencyModel))
				CopyFile(dependencyModel, targetDir);
		}

		[SetUp]
		public void Setup()
		{
			_tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(_tmpDir);
			CopyTestFiles(OutputDirectory, _tmpDir);

			_pathEnvironmentVariable = Environment.GetEnvironmentVariable("PATH");
			var path = $"{IcuDirectory}{Path.PathSeparator}{_pathEnvironmentVariable}";
			Environment.SetEnvironmentVariable("PATH", path);
		}

		[TearDown]
		public void TearDown()
		{
			Wrapper.Cleanup();
			new DirectoryInfo(_tmpDir).Delete(true);
			Environment.SetEnvironmentVariable("PATH", _pathEnvironmentVariable);
		}

		[Test]
		public void LoadIcuLibrary_GetFromPath()
		{
			Assert.That(RunTestHelper(_tmpDir), Is.EqualTo(FullIcuLibraryVersion));
		}

		[Test]
		public void LoadIcuLibrary_GetFromPathDifferentDir()
		{
			Assert.That(RunTestHelper(Path.GetTempPath()), Is.EqualTo(FullIcuLibraryVersion));
		}

		[Test]
		public void LoadIcuLibrary_LoadLocalVersion()
		{
			CopyMinimalIcuFiles(_tmpDir);
			Assert.That(RunTestHelper(_tmpDir), Is.EqualTo(MinIcuLibraryVersion));
		}

		[Test]
		public void LoadIcuLibrary_LoadLocalVersionDifferentWorkDir()
		{
			CopyMinimalIcuFiles(_tmpDir);
			Assert.That(RunTestHelper(Path.GetTempPath()), Is.EqualTo(MinIcuLibraryVersion));
		}

		[TestCase("")]
		[TestCase("win-")]
		public void LoadIcuLibrary_LoadLocalVersionFromArchSubDir(string prefix)
		{
			var targetDir = Path.Combine(_tmpDir, GetArchSubdir(prefix));
			Directory.CreateDirectory(targetDir);
			CopyMinimalIcuFiles(targetDir);
			Assert.That(RunTestHelper(_tmpDir), Is.EqualTo(MinIcuLibraryVersion));
		}

		[Test]
		public void LoadIcuLibrary_LoadLocalVersion_DirectoryWithSpaces()
		{
			var subdir = Path.Combine(_tmpDir, "Dir With Spaces");
			Directory.CreateDirectory(subdir);
			CopyTestFiles(_tmpDir, subdir);
			CopyMinimalIcuFiles(subdir);
			Assert.That(RunTestHelper(subdir, subdir), Is.EqualTo(MinIcuLibraryVersion));
		}

	}
}

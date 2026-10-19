using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock;
using PKISharp.WACS.UnitTests.Mock.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace PKISharp.WACS.UnitTests.Tests.InstallationPluginTests
{
    [TestClass]
    public class ScriptClientTests
    {
        private readonly Mock.Services.LogService log;
        private FileInfo? psExit;

        public ScriptClientTests()
        {
            log = new Mock.Services.LogService(false);
        }

        [TestMethod]
        [DataRow(0, true)]
        [DataRow(-1, false)]
        [DataRow(1, false)]
        public void TestEnvironmentExit(int exit, bool expectedSuccess)
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"[Environment]::Exit({exit})");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.AreEqual(expectedSuccess, success);
        }

        [TestMethod]
        [DataRow(0, true)]
        [DataRow(-1, false)]
        [DataRow(1, false)]
        public void TestExit(int exit, bool expectedSuccess)
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"exit {exit}");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.AreEqual(expectedSuccess, success);
        }

        [TestMethod]
        public void TestException()
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"throw 'error'");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.IsFalse(success);
        }

        [TestMethod]
        public void TestExceptionCatch()
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"try {{ throw 'error' }} catch {{ }}");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.IsTrue(success);
        }

        [TestMethod]
        public void ShellScriptWithSpaceInPath()
        {
            var tempPath = Infrastructure.Directory.Temp();
            var folder = Directory.CreateDirectory(Path.Combine(tempPath.FullName, $"with space {Guid.NewGuid()}"));
            var script = new FileInfo(Path.Combine(folder.FullName, "exit.sh"));
            File.WriteAllText(script.FullName, "exit 0");
            var sc = new ScriptClient(log, new MockSettingsService());
            Assert.IsTrue(sc.RunScript(script.FullName).Result.Success);
        }

        [TestMethod]
        public void ResolveAbsolute()
        {
            var tempPath = Infrastructure.Directory.Temp();
            var script = new FileInfo(Path.Combine(tempPath.FullName, "absolute.ps1"));
            File.WriteAllText(script.FullName, "exit 0");
            var sc = new ScriptClient(log, new MockSettingsService());
            Assert.AreEqual(script.FullName, sc.ResolveScript(script.FullName));
            Assert.IsTrue(sc.ValidScript(script.FullName));
            Assert.IsNull(sc.ResolveScript(Path.Combine(tempPath.FullName, "missing.ps1")));
            Assert.IsFalse(sc.ValidScript(Path.Combine(tempPath.FullName, "missing.ps1")));
        }

        [TestMethod]
        public void ResolveRelativeDefaultScriptPath()
        {
            var tempPath = Infrastructure.Directory.Temp();
            var configRoot = Directory.CreateDirectory(Path.Combine(tempPath.FullName, Guid.NewGuid().ToString()));
            var settings = new MockSettingsService(new Settings()
            {
                Client = new ClientSettings() { ConfigurationPath = configRoot.FullName }
            });
            var sc = new ScriptClient(log, settings);
            Assert.AreEqual(Path.Combine(configRoot.FullName, "scripts"), settings.Client.ScriptPath);

            var name = $"{Guid.NewGuid()}.ps1";
            Assert.IsNull(sc.ResolveScript(name));
            Assert.IsFalse(sc.ValidScript(name));

            var scriptFolder = Directory.CreateDirectory(settings.Client.ScriptPath);
            var script = new FileInfo(Path.Combine(scriptFolder.FullName, name));
            File.WriteAllText(script.FullName, "exit 0");
            Assert.AreEqual(script.FullName, sc.ResolveScript(name));
            Assert.IsTrue(sc.ValidScript(name));
        }

        [TestMethod]
        public void ResolveRelativeCustomScriptPath()
        {
            var tempPath = Infrastructure.Directory.Temp();
            var scriptPath = Directory.CreateDirectory(Path.Combine(tempPath.FullName, Guid.NewGuid().ToString()));
            var settings = new MockSettingsService(new Settings()
            {
                Client = new ClientSettings() { ScriptPath = scriptPath.FullName }
            });
            var sc = new ScriptClient(log, settings);
            Assert.AreEqual(scriptPath.FullName, settings.Client.ScriptPath);
            var name = $"{Guid.NewGuid()}.ps1";
            var script = new FileInfo(Path.Combine(scriptPath.FullName, name));
            File.WriteAllText(script.FullName, "exit 0");
            Assert.AreEqual(script.FullName, sc.ResolveScript(name));
        }

        [TestMethod]
        public void ResolveRelativeScriptPathFirst()
        {
            var tempPath = Infrastructure.Directory.Temp();
            var configRoot = Directory.CreateDirectory(Path.Combine(tempPath.FullName, Guid.NewGuid().ToString()));
            var settings = new MockSettingsService(new Settings()
            {
                Client = new ClientSettings() { ConfigurationPath = configRoot.FullName }
            });
            var sc = new ScriptClient(log, settings);
            var name = $"{Guid.NewGuid()}.ps1";
            var exeScript = new FileInfo(Path.Combine(VersionService.ExeDirectory, name));
            var folderScript = new FileInfo(Path.Combine(Directory.CreateDirectory(settings.Client.ScriptPath).FullName, name));
            try
            {
                File.WriteAllText(exeScript.FullName, "exit 0");
                File.WriteAllText(folderScript.FullName, "exit 0");
                Assert.AreEqual(folderScript.FullName, sc.ResolveScript(name));

                // Fall back to the program folder
                folderScript.Delete();
                Assert.AreEqual(exeScript.FullName, sc.ResolveScript(name));
            }
            finally
            {
                exeScript.Delete();
            }
        }

        [TestMethod]
        public void ResolveRelativeWorkingDirectory()
        {
            var tempPath = Infrastructure.Directory.Temp();
            var configRoot = Directory.CreateDirectory(Path.Combine(tempPath.FullName, Guid.NewGuid().ToString()));
            var workDir = Directory.CreateDirectory(Path.Combine(tempPath.FullName, Guid.NewGuid().ToString()));
            var settings = new MockSettingsService(new Settings()
            {
                Client = new ClientSettings() { ConfigurationPath = configRoot.FullName }
            });
            var sc = new ScriptClient(log, settings);
            var name = $"{Guid.NewGuid()}.ps1";
            var workScript = new FileInfo(Path.Combine(workDir.FullName, name));
            File.WriteAllText(workScript.FullName, "exit 0");
            var original = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = workDir.FullName;
                Assert.AreEqual(workScript.FullName, sc.ResolveScript(name));

                // Script folder takes precedence over the working directory
                var folderScript = new FileInfo(Path.Combine(Directory.CreateDirectory(settings.Client.ScriptPath).FullName, name));
                File.WriteAllText(folderScript.FullName, "exit 0");
                Assert.AreEqual(folderScript.FullName, sc.ResolveScript(name));
            }
            finally
            {
                Environment.CurrentDirectory = original;
            }
        }

        [TestMethod]
        [DataRow("{aaa}", "bbb")]
        [DataRow("{AAA}", "bbb")]
        [DataRow("{AaA}", "bbb")]
        [DataRow("{aaa}{aaa}{aaa}", "bbbbbbbbb")]
        [DataRow("{a}{a}{a}", "bla\\bla\\blabla\\bla\\blabla\\bla\\bla")]
        [DataRow("{aaa} {aaa}", "bbb bbb")]
        [DataRow(" {aaa} {aaa} ", " bbb bbb ")]
        [DataRow(" {aaaaa} {aaa} ", " {aaaaa} bbb ")]
        [DataRow("{vault://mock/key1}", "secret1")]
        [DataRow("{vault://mock/key2}", "secret2")]
        [DataRow("{vault://mock/key1}", "{vault://mock/key1}", true)]
        [DataRow("--secret={vault://mock/key2}&bla=1", "--secret=secret2&bla=1")]
        [DataRow("{vault://mock/key3}", "{vault://mock/key3}")]
        public void TestReplace(string original, string expected, bool censor = false)
        {
            var mock = MockContainer.TestScope();
            var replaced = ScriptClient.ReplaceTokens(original, new Dictionary<string, string?> { { "aaa", "bbb" }, { "a", "bla\\bla\\bla" } }, mock.Resolve<SecretServiceManager>(), censor).Result;
            Assert.AreEqual(expected, replaced);
        }
    }
}

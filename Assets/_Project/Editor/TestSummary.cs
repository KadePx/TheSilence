using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Silence.Editor
{
    /// <summary>
    /// Runs every EditMode test and writes a short summary (counts, then each failure with its message) to
    /// Temp/test-summary.txt. Menu: Silence > Run Tests (summary). For tools that only want the verdict.
    /// </summary>
    public static class TestSummary
    {
        public const string Path = "Temp/test-summary.txt";

        [MenuItem("Silence/Run Tests (summary)")]
        public static void Run()
        {
            File.WriteAllText(Path, "RUNNING\n");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        sealed class Callbacks : ICallbacks
        {
            readonly StringBuilder failures = new StringBuilder();
            int passed, failed, skipped;

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                File.WriteAllText(Path, $"DONE passed {passed} failed {failed} skipped {skipped} in {result.Duration:F1}s\n{failures}");
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: passed++; break;
                    case TestStatus.Failed:
                        failed++;
                        failures.AppendLine($"FAIL {result.Test.FullName}\n     {result.Message?.Replace("\n", "\n     ")}");
                        break;
                    default: skipped++; break;
                }
            }
        }
    }
}

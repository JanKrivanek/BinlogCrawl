using Microsoft.Build.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Evaluation;
using System.Collections;
using System.Diagnostics;

namespace BinlogCrawl
{
    internal static class BinlogCrawler
    {
        public static void Crawl(string logPath)
        {
            BinaryLogReplayEventSource replayEventSource = new BinaryLogReplayEventSource()
            {
                AllowForwardCompatibility = true
            };

            Handler handler = new Handler();

            replayEventSource.AnyEventRaised += handler.ReplayEventSource_AnyEventRaised;
            replayEventSource.RecoverableReadError += args => { };
            replayEventSource.Replay(BinaryLogReplayEventSource.OpenReader(logPath), CancellationToken.None);

            handler.FlushMappings();
        }

        private class Handler
        {
            private readonly Dictionary<string, List<string>> _msbuildFilesPerProject = new();
            private readonly Dictionary<string, List<string>> _outputFilesPerProject = new();
            // The eval ids can be used to do a more correct mapping of project + evalid --> binary and project + evalid --> imports
            private readonly Dictionary<int, string> _projFilesPerEvalId = new();

            public void FlushMappings()
            {
                Console.WriteLine("============= Project to imports ==================");

                foreach (var outputs in _msbuildFilesPerProject)
                {
                    Console.WriteLine(outputs.Key);
                    foreach (var output in outputs.Value)
                    {
                        Console.WriteLine("\t" + output);
                    }
                }

                Console.WriteLine("==================================================");
                Console.WriteLine();
                Console.WriteLine("============= Project to binary ==================");

                foreach (var outputs in _outputFilesPerProject)
                {
                    Console.WriteLine(outputs.Key);
                    foreach (var output in outputs.Value)
                    {
                        Console.WriteLine("\t" + output);
                    }
                }

                Console.WriteLine("==================================================");
            }

            public void ReplayEventSource_AnyEventRaised(object sender, Microsoft.Build.Framework.BuildEventArgs e)
            {
                if (e is ProjectImportedEventArgs projectImportedEventArgs && !projectImportedEventArgs.ImportIgnored && !string.IsNullOrEmpty(projectImportedEventArgs.ImportedProjectFile))
                {
                    int evalId = projectImportedEventArgs.BuildEventContext?.EvaluationId ?? BuildEventContext.InvalidEvaluationId;
                    string? projectFile = projectImportedEventArgs.ProjectFile;

                    if (evalId != BuildEventContext.InvalidEvaluationId && !_projFilesPerEvalId.TryGetValue(evalId, out projectFile))
                    {
                        projectFile = projectImportedEventArgs.ProjectFile;
                        _projFilesPerEvalId[evalId] = projectFile;
                    }

                    List<string>? files;
                    if (!_msbuildFilesPerProject.TryGetValue(projectFile, out files))
                    {
                        files = new List<string>();
                        _msbuildFilesPerProject[projectFile] = files;
                    }

                    if (!files.Contains(projectImportedEventArgs.ImportedProjectFile))
                    {
                        files.Add(projectImportedEventArgs.ImportedProjectFile);
                    }
                }
                else if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs && !(projectEvaluationFinishedEventArgs.ProjectFile?.EndsWith(".metaproj", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    string? target = projectEvaluationFinishedEventArgs.Properties.Cast<System.Collections.DictionaryEntry>()
                        // or "TargetFileName" for just filename
                        .FirstOrDefault(p => p.Key.Equals("TargetPath")).Value as string;

                    if (target != null)
                    {
                        List<string>? files;
                        if (!_outputFilesPerProject.TryGetValue(projectEvaluationFinishedEventArgs.ProjectFile, out files))
                        {
                            files = new List<string>();
                            _outputFilesPerProject[projectEvaluationFinishedEventArgs.ProjectFile] = files;
                        }

                        if (!files.Contains(target))
                        {
                            files.Add(target);
                        }
                    }
                }
                // This can be detected to start of events for known tasks (csc, vbs, fsc, CL)
                else if (e is TaskStartedEventArgs taskStartedEventArgs)
                {
                    // taskStartedEventArgs.TaskName
                }
                // This can be used to get args - and possibly starting commandline
                else if (e is TaskParameterEventArgs taskParameter)
                {
                    // taskParameter.ParameterName
                    // taskParameter.Items
                }
                else if (e is TaskFinishedEventArgs taskFinishedEventArgs)
                {

                }
            }
        }

        
    }
}

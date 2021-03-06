﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICanHasDotnetCore.Output;
using Serilog;

namespace ICanHasDotnetCore.Console
{
    class Program
    {
        private static readonly HashSet<string> ExcludeDirectories = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase)
        {
            "node_modules",
            "bower_components",
            "packages"
        };

        static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .MinimumLevel.Warning()
                .CreateLogger();

            try
            {
                if (args.Length < 2)
                {
                    System.Console.Error.WriteLine("Usage: CanIHazDotnetCore.exe <output_directory> <dir_to_scan_1> [dir_to_scan_2] ... [dir_to_scan_n]");
                    return 1;
                }

                var directories = args.Skip(1).Select(Path.GetFullPath).ToArray();
                var packageFiles = FindFiles(directories).ToArray();
                var result = PackageCompatabilityInvestigator.Create()
                    .Go(packageFiles)
                    .Result;


                WriteToOutputFiles(args[0], result);

                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Something went wrong");
                return 2;
            }
        }

        private static void WriteToOutputFiles(string outputDirectory, InvestigationResult result)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllLines(Path.Combine(outputDirectory, "Flat.txt"), new[] { FlatListingOutputFormatter.Format(result) });
            File.WriteAllLines(Path.Combine(outputDirectory, "Tree.txt"), new[] { TreeOutputFormatter.Format(result) });
            File.WriteAllLines(Path.Combine(outputDirectory, "1Level.gv"), new[] { GraphVizOutputFormatter.Format(result, 1) });
            File.WriteAllLines(Path.Combine(outputDirectory, "All.gv"), new[] { GraphVizOutputFormatter.Format(result) });

            foreach (var package in result.PackageConfigResults)
                File.WriteAllLines(Path.Combine(outputDirectory, package.PackageName + ".gv"), new[] { GraphVizOutputFormatter.Format(package) });

            System.Console.ForegroundColor = ConsoleColor.Magenta;
            System.Console.WriteLine($"Output written to {outputDirectory}");
            System.Console.ResetColor();
        }


        private static IEnumerable<PackagesFileData> FindFiles(IEnumerable<string> directories)
        {
            foreach (var directory in directories.Where(d => !ExcludeDirectories.Contains(Path.GetFileName(d))))
            {
                var configFile = Path.Combine(directory, "packages.config");
                if (File.Exists(configFile))
                    yield return new PackagesFileData(new DirectoryInfo(directory).Name, File.ReadAllBytes(configFile));

                foreach (var packageFile in FindFiles(Directory.EnumerateDirectories(directory)))
                    yield return packageFile;
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using CommandLine;

namespace FindDuplicateFiles
{
    /// <summary>
    /// Model to capture various options provided at teh Command Line 
    /// </summary>
    public class Options
    {
        [Option('p', "path", Required = false, Default = ".", HelpText = "Directory which will be searched for duplicate files.")]
        public string Path { get; set; }
        [Option('f', "filter", Required = false, Default = "*", HelpText = "Filter pattern for target files. Eg. *.txt")]
        public string Filter { get; set; }
        [Option('r', "recurse", Required = false, Default = false, HelpText = "Search recursvicely through all files.")]
        public bool Recurse { get; set; }
        [Option('v', "verbose", Required = false, Default = false, HelpText = "Appliaction will be in verbose mode.")]
        public bool Verbose { get; set; }
        [Option("duplicateLog", Required = false, HelpText = "Dump only dublicates to this file. Can be used later to remove files etc")]
        public string DuplicateLog { get; set; }
    }

    class Program
    {
        /// <summary>
        /// Leverage C# delegates to facilitate output to dynamic destinations
        /// NOTE: This exists as a property outside of the scope of the <code>Run(Options o)</code> method 
        /// so that all method of the <code>Program</code> can make use of it
        /// </summary>
        private static Action<string> Output { get; set; }

        static void Main(string[] args)
        {
            // TODO: Implement logging via DependencyInjection
            // TODO: Check validity of "filter" string command line options
            // TODO: Implement Logging using delegate / event pattern

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(Run)
                .WithNotParsed(Usage);
        }

        public static void Run(Options options)
        {
            var timer = Stopwatch.StartNew();

            // Ensure that the target directory exists
            if (!Directory.Exists(options.Path))
            {
                WriteLine($"Target directory '{options.Path}' does not exist.");
                return;
            }

            var targetDirectory = new DirectoryInfo(options.Path);

            WriteLine($"Compiling list of files in {targetDirectory.FullName} ...");

            var files = targetDirectory
                .GetFiles(options.Filter, new EnumerationOptions() { 
                        RecurseSubdirectories = options.Recurse, 
                        IgnoreInaccessible = true });

            WriteLine($"Total files: {files.Length}. Searching for duplicates ... ", options.Verbose);

            // Optmizitation:
            // Create groups of files of identical length 
            var sameLengthFiles = files.AsParallel()
                .Where(fi => fi.Length > 0)
                .GroupBy(fi => fi.Length)
                .Where(grp => grp.Count() > 1);

            var duplicates = new List<IEnumerable<FileInfo>>();

            // Iterate over identical file length groups 
            foreach (var fileGroup in sameLengthFiles)
            {
                // Build hash for each file in file group of same length
                var partialDuplicates = fileGroup.AsParallel()
                    .Select(fi => new { FileInfo = fi, Hash = GetFileHash(fi.FullName) })
                    .Where(fi => fi.Hash != null)
                    .GroupBy(fi => fi.Hash)
                    .Where(grp => grp.Count() > 1);

                // Add groups that have matching hatches to duplicate collection
                duplicates.AddRange(partialDuplicates.Select(grp => grp.Select(fi => fi.FileInfo)));
            }

            WriteLine($"We found {duplicates.Count} groups of files that are duplicates :-");

            WriteToConsole(duplicates);

            WriteLine($"Execution Time: {timer.Elapsed.Seconds} seconds.", options.Verbose);
        }

        public static void WriteToConsole(IList<IEnumerable<FileInfo>> groupedDuplicates)
        {
            var totalSpace = groupedDuplicates.Count() * groupedDuplicates.Sum(grp => grp.Sum(fi => fi.Length));

            for (int i = 0; i < groupedDuplicates.Count; i++)
            {
                // TODO: Get disk space information such as total space occupied and space occuped by duplicates
                var individualFileSize = groupedDuplicates[i].First().Length;
                var totalGroupSpace = individualFileSize * groupedDuplicates[i].Count();
                var spaceSavings = totalGroupSpace - individualFileSize;

                WriteLine($"--->>> Group {i+1} | Individual File Size: {individualFileSize} | Total Space (Group): {totalGroupSpace} " +
                    $" | Potential Space Savings: {spaceSavings}");

                foreach (var fileInfo in groupedDuplicates[i])
                    WriteLine($"{fileInfo.FullName}");

                WriteLine();

                Console.ForegroundColor = i % 2 == 0 ? ConsoleColor.White : ConsoleColor.Gray;
            }

            Console.ResetColor();
        }

        public static void Usage(IEnumerable<Error> errors)
        {
            // Do nothing as Help is automatically shown by the Parser
            //throw new NotImplementedException();
        }

        static string GetFileHash(string filepath)
        {
            using(var hasher = MD5.Create())
            {
                try
                {
                    return string.Concat<byte>(
                        hasher.ComputeHash(File.OpenRead(filepath)));
                }
                catch(System.UnauthorizedAccessException)
                {
                    WriteLine($"ACCESS DENIED >>> {filepath}");
                    return null;
                }
                catch(Exception ex)
                {
                    WriteLine($"ERROR >>> {filepath}");
                    WriteLine($"    {ex.Message}");

                    return null;
                }
            }
        }

        static void WriteLine(string message = "", bool visible = true)
        {
            if(visible)
                Console.WriteLine(message);
        }

    }
}

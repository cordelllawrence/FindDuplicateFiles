using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace FindDuplicates
{
    class Program
    {
        static void Main(string[] args)
        {
            var watch = new Stopwatch();
            watch.Start();

            var paths = new List<string>();
            paths.AddRange(args);

            if (paths.Count < 1) paths.Add(@".\");

             foreach(var path in paths)
             {
                 if (Directory.Exists(path) == false) continue;

                 var duplicates =
                     Directory.GetFiles(path)
                     .AsParallel()
                     .Select(file => new { Filename = file, Hash = ComputeFileHash(file) })
                     .Where(data => data.Hash != null)
                     .GroupBy(data => data.Hash)
                     .Where(group => group.Count() > 1)
                     .ToList();

                 foreach(var group in duplicates)
                 {
                     Console.WriteLine("The following files are duplicates: ");
                     foreach (var data in group) { Console.WriteLine("\t\t{0}", data.Filename); }
                 }

                 Console.WriteLine("\n\nTime to run: {0}", watch.Elapsed);
             }
        }

        static string ComputeFileHash(string filePath)
        {
            try
            {
                using(var crypto = MD5.Create())
                    return string.Join(":", crypto.ComputeHash(File.OpenRead(filePath)).Select(b => b.ToString()));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Opening file '{0}' - {1}", filePath, e.Message);
                return null;
            }
        }
    }
}
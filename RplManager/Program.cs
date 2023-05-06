using System;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Diagnostics;


namespace RplManager
{
    internal class Program
    {
        private int[] startJointsArray;
        public Program()
        {
            const int size = 20;
            startJointsArray = new int[size];
            for (int i = 0; i < size; i++)
            {
                startJointsArray[i] = 4;
            }
        }

        static void Main(string[] args)
        {
            Program myObject = new Program();
            string path = setup();
            myObject.cutter2(path);
            Console.ReadLine();
        }
        public static string setup()
        {
            // Set the path to the JSON file
            string jsonFilePath = "appsettings.json";

            // Create the JSON file with an empty object if it doesn't exist
            if (!File.Exists(jsonFilePath))
            {
                File.WriteAllText(jsonFilePath, "{}");
            }

            // Create a ConfigurationBuilder and add the JSON file as a source
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(jsonFilePath, optional: false, reloadOnChange: true);

            // Build the configuration
            var configuration = builder.Build();

           return verifyReplayFolderPath(configuration, jsonFilePath);
        }
        public static string verifyReplayFolderPath(IConfiguration configuration, string jsonFilePath)
        {
            string rplFolderName = "replay";
            string applicationPath = AppDomain.CurrentDomain.BaseDirectory;
            string rplFolderPath = configuration["Path"] ?? string.Empty;
            string[] rplArray;
            bool rplsFound = false;
            while (rplsFound == false)
            {
                // Tarkistetaan onko sovellus replay kansiossa tai sen alakansioissa
                // Jos mahdollista, erotetaan kansion polku aplikaation polusta
                if (rplFolderPath == string.Empty && applicationPath.Contains(rplFolderName))
                {
                    string[] subStrings = applicationPath.Split(rplFolderName);
                    rplFolderPath = subStrings[0] + rplFolderName;
                }
                //testataan löytyykö rpl tiedostoja kansiosta
                try
                {
                    rplArray = Directory.GetFiles(rplFolderPath, "*.rpl", SearchOption.AllDirectories);
                    if (rplArray.Length > 0)
                    {
                        Console.WriteLine("Path is " + rplFolderPath);
                        Console.WriteLine("Found " + rplArray.Length + " replays");
                        break;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid file path");
                }
                //kun muu ei toimi pyydetään polku ja yritetään uudestaan
                rplFolderPath = setCustomPath(configuration, jsonFilePath);
            }
            return rplFolderPath;
        }
        public static string setCustomPath(IConfiguration configuration, string jsonFilePath)
        {
            Console.WriteLine("Couldn't find replays.");
            Console.WriteLine("Please enter custom path for replay folder");
            string ans;
            while (true)
            {
                ans = Console.ReadLine() ?? string.Empty;
                if (ans != string.Empty)
                {
                    configuration["Path"] = ans;

                    // Save the changes to the file
                    var root = JObject.Parse(File.ReadAllText(jsonFilePath));
                    foreach (var kvp in configuration.AsEnumerable())
                    {
                        root[kvp.Key] = kvp.Value;
                    }
                    File.WriteAllText(jsonFilePath, root.ToString((Newtonsoft.Json.Formatting)Formatting.Indented));

                    return ans;
                }
            }
        }
        public void cutter2(string path)
        {
            string src;
            int endFrame;
            int startFrame = 0;
            int currentframe = 0;
            bool firstIteration = true;
            while (true) 
            {
                Console.Write("\nEnter name of ");
                if (firstIteration)
                {
                    Console.Write("replay to cut\n");
                }
                else
                {
                    Console.Write("continuing replay\n");
                }
                src = retRplPath(path);
                Console.WriteLine("\nSelect start frame");
                startFrame = retNumb();
                Console.WriteLine("\nSelect end frame");
                endFrame = retNumb();
                currentframe = writeReplay(src, startFrame, endFrame, currentframe, firstIteration);
                firstIteration = false;
            }
        }
        //kirjoittaa pyydetyn osion tiedostoon ja palauttaa viimeisen kirjotetun framen
        public int writeReplay(string src, int startFrame, int endFrame, int previousFrame, bool firstIteration)
        {
            //objekti voisi olla tehokkaampi jointteihin
            int[] p0Joints = startJointsArray;
            int[] p1Joints = startJointsArray;
            int[] p2Joints = startJointsArray;
            int[] p3Joints = startJointsArray;

            string line;
            bool allowWrite = false;
            string[] tmpSplit = new string[50];
            int currentFrame = 0;
            int lastWrittenFrame = 0;
            bool firstFrameCatched = false;
            string newFilePath = src.Replace(".rpl", "") + "-cut.rpl";
            StreamWriter writer;
            // varmistaa että aloitetaaan tyhjästä tai luo puuttuvan tiedoston
            if (firstIteration)
            {
                writer = new StreamWriter(newFilePath);
            } 
            else
            {
                writer = File.AppendText(newFilePath);
            }
            using (writer)
            using (StreamReader reader = new StreamReader(src))
            {
                while ((line = reader.ReadLine() ?? string.Empty) != string.Empty && currentFrame < endFrame)
                {
                    if (firstIteration == true)
                    {
                        allowWrite = true;
                    }
                    if (line.Contains("FRAME 0;") && startFrame > 0)
                    {
                        firstIteration = false;
                        allowWrite = false;
                    }
                    if (line.Contains("FRAME "))
                    {
                        tmpSplit = line.Split(' ');
                        currentFrame = Convert.ToInt32(tmpSplit[1].Trim(';'));
                        if (currentFrame > endFrame)
                        {
                            allowWrite = false;
                            break; 
                        }
                        else
                        {
                            lastWrittenFrame = currentFrame;
                        }
                        if (currentFrame >= startFrame)
                        {
                            if (!firstFrameCatched)
                            {
                                startFrame = currentFrame;
                                firstFrameCatched = true;
                                allowWrite = true;
                            }
                            tmpSplit[1] = (previousFrame + currentFrame - startFrame).ToString() + ';';
                            line = "";
                            foreach (string s2 in tmpSplit)
                            {
                                line = line + s2 + " ";
                            }
                        }
                    }
                    if (line.Contains("JOINT "))
                    {
                        tmpSplit = line.Split("; ");
                        tmpSplit = tmpSplit[1].Split(" ");
                        //TODO vähennä toistoa
                        if (line.Contains("JOINT 0"))
                        {
                            for (int i = 0; i < tmpSplit.Length; i = i + 2)
                            {
                                p0Joints[int.Parse(tmpSplit[i])] = int.Parse(tmpSplit[i + 1]);
                            }
                            if (allowWrite && currentFrame == startFrame) 
                            {
                                line = (jointsToStr(p0Joints, line));
                            }
                        }
                        if (line.Contains("JOINT 1"))
                        {
                            for (int i = 0; i < tmpSplit.Length; i = i + 2)
                            {
                                p1Joints[int.Parse(tmpSplit[i])] = int.Parse(tmpSplit[i + 1]);
                            }
                            if (allowWrite && currentFrame == startFrame)
                            {
                                line = (jointsToStr(p1Joints, line));
                            }
                        }
                        if (line.Contains("JOINT 2"))
                        {
                            for (int i = 0; i < tmpSplit.Length; i = i + 2)
                            {
                                p2Joints[int.Parse(tmpSplit[i])] = int.Parse(tmpSplit[i + 1]);
                            }
                            if (allowWrite && currentFrame == startFrame)
                            {
                                line = (jointsToStr(p2Joints, line));
                            }
                        }
                        if (line.Contains("JOINT 3"))
                        {
                            for (int i = 0; i < tmpSplit.Length; i = i + 2)
                            {
                                p3Joints[int.Parse(tmpSplit[i])] = int.Parse(tmpSplit[i + 1]);
                            }
                            if (allowWrite && currentFrame == startFrame)
                            {
                                line = (jointsToStr(p3Joints, line));
                            }
                        }
                    }
                    if (allowWrite == true)
                    {
                        writer.WriteLine(line);
                    }
                }
                writer.Close();
                reader.Close();
                Console.WriteLine("last frame in replay was " + lastWrittenFrame);
                return previousFrame + lastWrittenFrame - startFrame;
            }
        }
        public static string retRplPath(string path)
        {
            string ans1;
            while (true)
            {
                ans1 = Console.ReadLine() ?? string.Empty;
                ans1 = "*" + ans1 + "*";
                string[] files = Directory.GetFiles(path, ans1, SearchOption.AllDirectories);
                int count = files.Count();
                Console.WriteLine(count + " Matches found");
                if (count == 1 && files[0].Contains(".rpl"))
                {
                    Console.WriteLine("rpl found at " + files[0]);
                    return files[0];
                }
                if (count > 1)
                {
                    Console.WriteLine("Please choose one replay");
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (files[i].Contains(".rpl"))
                        {
                            Console.WriteLine(i + 1 + ". " + files[i]);
                        }
                        else
                        {
                            files[i] = string.Empty;
                        }
                    }
                    Console.WriteLine("0. Search again");
                    int indx;
                    while (true)
                    {
                        indx = retNumb();
                        if (indx == 0)
                        {
                            break;
                        }
                        try
                        {
                            indx--;
                            if (files[indx] != null)
                            {
                                return files[indx];
                            }
                        }
                        catch
                        {
                            Console.WriteLine("given number not included in list try again");
                        }
                    }

                }
            }
        }
        public static int retNumb()
        {
            string ans1;
            while (true)
            {
                ans1 = Console.ReadLine() ?? string.Empty;
                try
                {
                    return Convert.ToInt32(ans1);
                }
                catch (Exception)
                {
                    Console.WriteLine("couldn't convert input to numbers");
                    Console.WriteLine("make sure you're entering numbers");
                }
            }
        }
        public static string jointsToStr(int[] arr, string line)
        {
            string[] tmparr = new string[2];
            string tmpstr;
            tmparr = line.Split(";");
            tmpstr = tmparr[0] + ";";
            for (int i = 0; i < arr.Length; i++)
            {
                tmpstr += " " + i + " " + arr[i];
            }
            return tmpstr;
        }

    }
}
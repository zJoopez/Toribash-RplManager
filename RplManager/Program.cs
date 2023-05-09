using System;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.ComponentModel;
using System.Data;

namespace RplManager
{
    internal class Program
    {
        private string newFilePath = string.Empty;
        private int[][] jointStatesOfPlayers;
        public Program()
        {
            //Creates array of size 20 where each value is 4.
            //Each value in array represents state of joint (default is 4)
            //There are 20 joints and in rpl files they use numbers 0-19 as labels, so index X = joint X
            jointStatesOfPlayers = new int[4][];
        }
        static void Main(string[] args)
        {
            Program myObject = new Program();
            string path = setup();
            myObject.replayManagerStart(path);
        }
        public static string setup()
        {
            //Set the path to the JSON file
            string jsonFilePath = "appsettings.json";

            //Create the JSON file with an empty object if it doesn't exist
            if (!File.Exists(jsonFilePath))
            {
                File.WriteAllText(jsonFilePath, "{}");
            }

            //Create a ConfigurationBuilder and add the JSON file as a source
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(jsonFilePath, optional: false, reloadOnChange: true);

            //Build the configuration
            var configuration = builder.Build();

           return verifyReplayFolderPath(configuration, jsonFilePath);
        }
        public static string verifyReplayFolderPath(IConfiguration configuration, string jsonFilePath)
        {
            //Includes methods for setting a path and verifys path by checking there are rpl files in it.
            string rplFolderName = "replay";
            string applicationPath = AppDomain.CurrentDomain.BaseDirectory;
            string rplFolderPath = configuration["Path"] ?? string.Empty;
            string[] rplArray;
            bool rplsFound = false;
            while (rplsFound == false)
            {
                //Checks if application is in subfolders of replay folder
                if (rplFolderPath == string.Empty && applicationPath.Contains(rplFolderName))
                {
                    //Cuts path for replay folder from app path if possible
                    string[] subStrings = applicationPath.Split(rplFolderName);
                    rplFolderPath = subStrings[0] + rplFolderName;
                }
                //Searches for .rpl files in current path.
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
                //Requests setting custom path for another attempt
                rplFolderPath = setCustomPath(configuration, jsonFilePath);
            }
            return rplFolderPath;
        }
        public static string setCustomPath(IConfiguration configuration, string jsonFilePath)
        {
            //Requests new path as input and returns given string
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
        public void replayManagerStart(string path)
        {
            //Provides options and takes inputs required for cutting and combining operations
            string src;
            int endFrame;
            int startFrame = 0;
            int currentframe = 0;
            bool firstIteration = true;
            bool proceed = true;
            int tmpInt;
            while (proceed) 
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
                currentframe = cutAndCombine(src, startFrame, endFrame, currentframe, firstIteration);
                firstIteration = false;
                Console.WriteLine("Options");
                Console.WriteLine("1. Continue replay");
                Console.WriteLine("0. Exit");
                while (true)
                {
                    tmpInt = retNumb();
                    if(tmpInt == 0)
                    {
                        proceed = false;
                        break;
                    }
                    if(tmpInt == 1)
                    {
                        break;
                    }
                }
            }
        }
        public int cutAndCombine(string src, int startFrame, int endFrame, int previousFrame, bool firstIteration)
        {
            //Writes requested segment into new file
            //Returns last written frame to allow continue writing from last frame
            jointsToDefaultState();
            string line;
            bool allowWrite = false;
            string[] tmpSplit = new string[50];
            int currentFrame = 0;
            int lastWrittenFrame = 0;
            bool firstFrameCatched = false;
            StreamWriter writer;  
            if (firstIteration)
            {
                newFilePath = src.Replace(".rpl", "") + "-cut.rpl";
                //Resets file or creates new one if it's missing
                writer = new StreamWriter(newFilePath);
            }
            //Continues writing to existing file
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
                            //Sets allow write back to false and breaks to skip other conditions
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
                        int c = (int)Char.GetNumericValue((tmpSplit[0])[(tmpSplit[0]).Length - 1]);
                        tmpSplit = tmpSplit[1].Split(" ");           
                        
                        updatePlayerJointStates(c, jointStatesOfPlayers, tmpSplit);
                        if (allowWrite && currentFrame == startFrame)
                        {
                            line = (jointsToStr(jointStatesOfPlayers[c], line));
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
        public void updatePlayerJointStates(int c, int[][] jointsOfPlayers, string[] jointChanges)
        {
            for (int i = 0; i < jointChanges.Length; i = i + 2)
            {
                jointsOfPlayers[c][int.Parse(jointChanges[i])] = int.Parse(jointChanges[i + 1]);
            }
            
        }
        public static string retRplPath(string path)
        {
            //Controls for selecting a replay file
            //Returns path of selected file
            string ans1;
            while (true)
            {
                ans1 = Console.ReadLine() ?? string.Empty;
                ans1 = "*" + ans1 + "*";
                string[] files = Directory.GetFiles(path, ans1, SearchOption.AllDirectories);
                int count = files.Count();
                Console.WriteLine(count + " Matches found");
                //if single match, returns it
                if (count == 1 && files[0].Contains(".rpl"))
                {
                    Console.WriteLine("rpl found at " + files[0]);
                    return files[0];
                }
                //Provides list of files to select from if multiple matches
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
                    //Compares input to options provided above
                    //Returns path of selected file if there's match
                    //Value 0 allows exiting to original while loop for new search
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
            //Converts and returns input as integer.
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
        public static string jointsToStr(int[] jointStates, string line)
        {
            //Creates modified string of line with state of every joint
            string[] tmparr = new string[2];
            string tmpstr;
            tmparr = line.Split(";");
            tmpstr = tmparr[0] + ";";
            for (int i = 0; i < jointStates.Length; i++)
            {
                tmpstr += " " + i + " " + jointStates[i];
            }
            return tmpstr;
        }
        public void jointsToDefaultState()
        {
            for (int i = 0; i < jointStatesOfPlayers.Length; i++)
            {
                jointStatesOfPlayers[i] = new int[20];
                for (int j = 0; j < jointStatesOfPlayers[i].Length; j++)
                {
                    jointStatesOfPlayers[i][j] = 4;
                }
            }
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WorkableCodingTask1
{
    class Error
    {
        string message;
        public string Message { get => message; set => message = value; }

        string status;
        public string Status { get => status; set => status = value; }

        public Error(string s, string m)
        {
            Message = m;
            Status = s;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //iterate through all .log files in dir
            foreach (string file in Directory.EnumerateFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.log")) 
            {
                Hashtable redirects = new Hashtable();
                List<Error> serverErrorList = new List<Error>();
                Hashtable urls = new Hashtable();

                float totalTime = 0f;
                int count = 0;
                Hashtable tables = new Hashtable();

                //iterate through all rows of file
                string[] rows = File.ReadAllLines(file);
                foreach (string row in rows)
                {
                    PopulateErrorUrls(row, ref urls);
                    GetServiceTime(row, ref totalTime, ref count);
                    PopulateTablePopularity(row, ref tables);
                    FindRedirects(row, ref redirects);
                    FindServerIssues(row, ref serverErrorList);
                }
                float avgTime = totalTime / (float)count;
                KeyValuePair<string, int> popularTable = getPopularTable(tables);

                //PRINT RESULTS FOR Q1
                Console.WriteLine("The list of URLs, followed by the number of times they were requested:");
                foreach (string key in urls.Keys)
                {
                    Console.WriteLine(String.Format("{0}: {1}", key, urls[key]));
                }
                Console.WriteLine();
                //PRINT RESULTS FOR Q2
                Console.WriteLine("The average time of service is: " + avgTime + "ms.");
                Console.WriteLine();

                //PRINT RESULTS FOR Q3
                Console.WriteLine("The most popular table was " + popularTable.Key + " at: " + popularTable.Value + " requests.");
                Console.WriteLine();

                //PRINT RESULTS FOR Q4
                Console.WriteLine("Redirection status code, followed by the number of times each code appeared: ");
                foreach (string key in redirects.Keys)
                {
                    Console.WriteLine(String.Format("{0}: {1}", key, redirects[key]));
                }
                Console.WriteLine();

                //PRINT RESULTS FOR Q5
                foreach (Error er in serverErrorList)
                {
                    Console.WriteLine("Status: " + er.Status + ". Reason: " + er.Message);
                }
                Console.Read();
            }
        }

        /// <summary>
        /// Takes in a string reference and a hashtable reference.
        /// Finds the http status code that follows the "status=" flag in the string and updates its count on the hashtable using the IncrementOrAdd method.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="redirects"></param>
        /// <param name="redList"></param>
        private static void FindRedirects(string row, ref Hashtable redirects)
        {
            if (row.Contains("status=3"))
            {
                int endIndex = row.EndIndexOf("status=");
                string trimmed = row.Substring(endIndex);
                string code = trimmed.Substring(0, 3);
                IncrementOrAdd(ref redirects, code);
            }
        }
        
        /// <summary>
        /// Takes in a string and a list of Error objects. 
        /// Scans string for the status=5 flag. This will return any server related errors (5xx).
        /// Adds all errors, error code and info, into the list.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="serverIssues"></param>
        private static void FindServerIssues(string row, ref List<Error> serverIssues)
        {
            if (row.Contains("status=5"))
            {
                int endIndex = row.EndIndexOf("status=");
                string trimmed = row.Substring(endIndex);
                string code = trimmed.Substring(0, 3);
                string errorMsg = "";

                if (row.Contains("error="))
                {
                    errorMsg = System.Text.RegularExpressions.Regex.Split(trimmed, @"\s+")[1];
                }
                serverIssues.Add(new Error(code, errorMsg));
            }
        }

        /// <summary>
        ///  Takes in a hashtable and returns the keyvaluepair with the highest value.
        /// </summary>
        /// <param name="tables"></param>
        /// <returns></returns>
        private static KeyValuePair<string, int> getPopularTable(Hashtable tables)
        {
            KeyValuePair<string, int> max = new KeyValuePair<string, int>();
            foreach (DictionaryEntry entry in tables)
            {
                if ((int)entry.Value > max.Value)
                    max = new KeyValuePair<string, int>((string)entry.Key, (int)entry.Value);
            }
            return max;
        }

        /// <summary>
        /// Takes in a string reference and a hashtable reference.
        /// Finds the names that follows the "FROM" flag in the string and updates the hashtable using the getPopularTable method.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="tables"></param>
        private static void PopulateTablePopularity(string row, ref Hashtable tables)
        {
            string tableName = "";
            if (row.Contains("FROM"))
            {
                string[] words = System.Text.RegularExpressions.Regex.Split(row, @"\s+");
                for (int i = 0; i < words.Length; i++)
                {
                    if (words[i].Contains("FROM"))
                    {
                        tableName = words[i + 1].Replace("\"", "");
                    }
                }
                IncrementOrAdd(ref tables, tableName);
            }
        }

        /// <summary>
        /// Takes a string, total time and the count of elements that have a service flag.
        /// Finds the time spent servicing and updates the count and hashtable.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="totalTime"></param>
        /// <param name="count"></param>
        public static void GetServiceTime(string row, ref float totalTime, ref int count)
        {
            float serviceTime = 0f;

            if (row.Contains("service="))
            {
                string[] words = System.Text.RegularExpressions.Regex.Split(row, @"\s+");
                foreach (string word in words)
                {
                    if (!word.Contains("service="))
                    {
                        continue;
                    }
                    string time = word.Substring(8);
                    time = time.Remove(time.Length - 2);
                    if (!float.TryParse(time, out serviceTime))
                    {
                        throw new System.ArgumentException("Time could not be parsed into float", "serviceTime");
                    }
                }
            }
            if (serviceTime > 0f)
            {
                totalTime += serviceTime;
                count++;
            }
        }

        /// <summary>
        /// Takes in a string and a hashtable, that holds URLs paired with the count of their appearences.
        /// Checks string for the message "status=404". If found, it looks for path and host. Concatenates the two, in order to get the full URL. 
        /// Finally, it updates the hashtable.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="urls"></param>
        public static void PopulateErrorUrls(string row, ref Hashtable urls)
        {
            if (row.Contains("status=404"))
            {
                string path = "";
                string host = "";
                string[] words = System.Text.RegularExpressions.Regex.Split(row, @"\s+");
                foreach (string word in words)
                {
                    if (word.Contains("path="))
                    {
                        path = word.Substring(5).Replace("\"", "");
                    }
                    if (word.Contains("host="))
                    {
                        host = word.Substring(5).Replace("\"", "");
                    }
                }
                string found = String.Concat(host, path);
                IncrementOrAdd(ref urls, found);
            }
        }

        /// <summary>
        /// Takes in a hashtable and a string.
        /// If the string exists as a key, it incrementes pairing value by one.
        /// If not, a new keypair value is created, with the value set to 1.
        /// </summary>
        /// <param name="myHash"></param>
        /// <param name="keyName"></param>
        private static void IncrementOrAdd(ref Hashtable myHash, string keyName)
        {
            if (myHash.ContainsKey(keyName))
            {
                int old = (int)myHash[keyName];
                myHash[keyName] = old + 1;
            }
            else
            {
                myHash.Add(keyName, 1);
            }
        }
    }

    public static class myExtentions
    {
        /// <summary>
        /// Extention method, returning the index of the last char of a substring, found within a string.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int EndIndexOf(this string source, string value)
        {
            int index = source.IndexOf(value);
            if (index >= 0)
            {
                index += value.Length;
            }

            return index;
        }
    }
}
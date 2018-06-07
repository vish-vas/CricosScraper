using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace CricosScraper
{
    class Program
    {
        const string folderLocation = "CricosScraperData";
        const string badCourseFileName = "badCourseIds.txt";
        const string courseDataFileName = "courseData.json";
        const string scrapedCourseFileName = "scrapedCourseIds.txt";
        const string badInstituteFileName = "badIndtituteIds.txt";
        const string instititeDataFileName = "instituteData.json";
        const string scrapedInstituteFileName = "scrapedInstituteIds.txt";
        const string courseUrl = "http://cricos.education.gov.au/Course/CourseDetails.aspx?CourseId={0}";
        const string instituteUrl = "http://cricos.education.gov.au/Institution/InstitutionDetails.aspx?ProviderID={0}";
        const int sleepTime = 1000 * 1 / 2; //1000(1sec) * 2
        const bool sleepEnabled = true;
        enum RequestType
        {
            COURSE_REQUEST,
            INSTITUTE_REQUEST
        }

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                int scrapType;
                if (!int.TryParse(args[0], out scrapType))
                {
                    Console.WriteLine("Could not parse first input - scraper type, 1 for course scraper and 2 for institute scraper.");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                if (args.Length == 2)
                {
                    int maxValue;
                    if (!int.TryParse(args[1], out maxValue))
                    {
                        Console.WriteLine("Could not parse second input - max value.");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    if (scrapType == 1)
                        scrapeStuff(RequestType.COURSE_REQUEST, maxValue);
                    else if (scrapType == 2)
                        scrapeStuff(RequestType.INSTITUTE_REQUEST, maxValue);
                    else
                    {
                        Console.WriteLine("Not a valid scraper type selected.");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    Console.WriteLine("operation completed.");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                if (scrapType == 1)
                    scrapeStuff(RequestType.COURSE_REQUEST, 100000);
                else if (scrapType == 2)
                    scrapeStuff(RequestType.INSTITUTE_REQUEST, 10000);
                else
                {
                    Console.WriteLine("Not a valid scraper type selected.");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                Console.WriteLine("operation completed.");
                Console.ReadKey();
                Environment.Exit(0);
            }
            else
            {
                scrapeStuff(RequestType.COURSE_REQUEST, 100000);
                scrapeStuff(RequestType.INSTITUTE_REQUEST, 10000);
                Console.WriteLine("operation completed.");
                Console.ReadKey();
            }
        }

        private static void scrapeStuff(RequestType requestType, int max)
        {
            var badIdsFile = requestType == RequestType.COURSE_REQUEST ? badCourseFileName : badInstituteFileName;
            var scrapedIdsFile = requestType == RequestType.COURSE_REQUEST ? scrapedCourseFileName : scrapedInstituteFileName;
            var dataFile = requestType == RequestType.COURSE_REQUEST ? courseDataFileName : instititeDataFileName;

            var badIds = readIdsFromFile(badIdsFile);
            var scrapedIds = readIdsFromFile(scrapedIdsFile);
            for (int i = 0; i < max; i++)
            {
                if (badIds.ContainsKey(i) || scrapedIds.ContainsKey(i))
                    continue;
                if (sleepEnabled)
                    Thread.Sleep(sleepTime);
                var data = getResponseForRequest(i, requestType, badIdsFile);
                if (data == null)
                    continue;
                var obj = parsePageData(data, i, requestType);
                if (appendJSONToDataFile(obj, dataFile))
                    appendToIdsFile(i, scrapedIdsFile);
            }
        }

        private static Dictionary<int, bool> readIdsFromFile(string fileName)
        {
            if (!Directory.Exists(folderLocation))
                Directory.CreateDirectory(folderLocation);

            string filePath = folderLocation + "/" + fileName;
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
                return new Dictionary<int, bool>();
            }

            Dictionary<int, bool> dictionary = new Dictionary<int, bool>();
            string text = File.ReadAllText(filePath);
            var arr = text.Split(',');
            foreach (var a in arr)
            {
                if (a != "")
                    dictionary[Int32.Parse(a)] = true;
            }
            return dictionary;
        }

        private static void appendToIdsFile(int num, string fileName)
        {
            string filePath = folderLocation + "/" + fileName;
            using (StreamWriter writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
            {
                writer.Write("{0},", num);
                writer.Close();
            }
        }

        private static string getResponseForRequest(int num, RequestType requestType, string badIdsFile)
        {
            string uri = requestType == RequestType.COURSE_REQUEST ? courseUrl : instituteUrl;
            string key = requestType == RequestType.COURSE_REQUEST ? "CourseDetails" : "InstitutionDetails";
            uri = String.Format(uri, num);
            HttpWebRequest request = WebRequest.Create(uri) as HttpWebRequest;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36";
            request.Method = "GET";
            try
            {
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    if (response.StatusCode == HttpStatusCode.OK && response.ResponseUri.ToString().Contains(key))
                    {
                        Stream receiveStream = response.GetResponseStream();
                        StreamReader readStream = null;

                        if (response.CharacterSet == null)
                        {
                            readStream = new StreamReader(receiveStream);
                        }
                        else
                        {
                            readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                        }
                        string data = readStream.ReadToEnd();
                        response.Close();
                        readStream.Close();
                        Console.WriteLine("got {0} data for {1}", requestType.ToString(), num);
                        return data;
                    }
                    else
                    {
                        response.Close();
                        appendToIdsFile(num, badIdsFile);
                        Console.Out.WriteLine(uri + " - " + response.StatusCode + " - bad id");
                        return null;
                    }
                }
            }
            catch (WebException e)
            {
                Console.Out.WriteLine("Exception occured: " + e);
            }
            return null;
        }

        private static JObject parsePageData(string html, int num, RequestType requestType)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var nodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'form-horizontal')]");
            JObject obj = new JObject();
            for (int i = 0; i < nodes.Count(); i++)
            {
                var node = nodes[i];
                var children = node.SelectNodes("div[contains(@class, 'form-group')]");
                JObject childObj = new JObject();
                foreach (var child in children)
                {
                    var label = child.SelectSingleNode("label");
                    var value = child.SelectSingleNode("div/span");
                    if (value == null)
                        value = child.SelectSingleNode("div/a");
                    if (value != null)
                        childObj[label.InnerText] = value.InnerText.Trim();
                }
                if (requestType == RequestType.COURSE_REQUEST)
                {
                    switch (i)
                    {
                        case 0:
                            obj["course"] = childObj;
                            break;
                        case 1:
                            obj["institution"] = childObj;
                            break;
                        case 2:
                            obj["contact"] = childObj;
                            break;
                        case 3:
                            obj["contact"] = childObj;
                            break;
                    }
                }
                else if (requestType == RequestType.INSTITUTE_REQUEST)
                {
                    switch (i)
                    {
                        case 0:
                            obj["institution"] = childObj;
                            break;
                        case 1:
                            obj["contact"] = childObj;
                            break;
                        case 2:
                            obj["contact"] = childObj;
                            break;
                    }
                }
            }
            JArray locations = new JArray();
            var locationItems = requestType == RequestType.COURSE_REQUEST ? htmlDoc.DocumentNode.SelectNodes("//td/span") : htmlDoc.DocumentNode.SelectSingleNode("//table").SelectNodes("tr");
            if (locationItems != null)
            {
                foreach (var locationItem in locationItems)
                {
                    if (requestType == RequestType.COURSE_REQUEST)
                    {
                        if (locationItem.InnerText != "")
                            locations.Add(locationItem.InnerText);
                    }
                    else if (requestType == RequestType.INSTITUTE_REQUEST)
                    {
                        var locationRowData = locationItem.SelectNodes("td");
                        if (locationRowData != null && locationRowData.Count == 3)
                        {
                            JObject loc = new JObject();
                            loc["locationName"] = locationRowData[0].InnerText;
                            loc["state"] = locationRowData[1].InnerText;
                            loc["noOfCourses"] = locationRowData[2].InnerText;
                            locations.Add(loc);
                        }
                    }
                }
            }
            obj["locations"] = locations;
            return obj;
        }

        private static bool appendJSONToDataFile(JObject jsonData, string fileName)
        {
            string filePath = folderLocation + "/" + fileName;
            bool newFile = false;
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
                newFile = true;
            }
            using (StreamWriter writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
            {
                if (newFile)
                    writer.Write("[" + Environment.NewLine);
                writer.Write("{0},{1}", jsonData.ToString(), Environment.NewLine);
                writer.Close();
                return true;
            }
        }
    }
}

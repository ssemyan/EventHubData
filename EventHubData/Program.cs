using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EventHubData
{
    internal class Program
    {
        private static string _serviceBusNameSpace;
        private static string _eventHubName;
        private static string _subscriptionId;
        private static string _rollupPeriod;
        private static int _howFarToLookBackInMin;

        private static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintUsage();
                return;
            }

            // Use the APIs from here: 
            // https://msdn.microsoft.com/en-us/library/azure/dn163589.aspx

            // Get config settings
            _serviceBusNameSpace = ConfigurationManager.AppSettings["ServiceBusNamespace"];
            _eventHubName = ConfigurationManager.AppSettings["EventHubName"];
            _subscriptionId = ConfigurationManager.AppSettings["SubscriptionId"];
            _rollupPeriod = ConfigurationManager.AppSettings["RollupPeriod"];
            if (!int.TryParse(ConfigurationManager.AppSettings["HowFarToLookBackInMinutes"], out _howFarToLookBackInMin))
            {
                Console.WriteLine("HowFarToLookBackInMinutes setting in config file must be a number.");
                return;
            }


            switch (args[0].ToLower())
            {
                case "showavail":
                    PrintAvailableMetrics();
                    break;

                case "getmetrics":
                    GetMetrics();
                    break;

                default:
                    PrintUsage();
                    break;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: EventHubData showavail|getmetrics");
            Console.WriteLine("   showavail -  show all metrics available for the event hub in the config");
            Console.WriteLine("   getmetrics - stream metrics every 5 min for the event hub in the config");
        }

        private static void GetMetrics()
        {
            var metrics = ConfigurationManager.AppSettings["MetricsToWatch"];
            if (string.IsNullOrEmpty(metrics))
            {
                Console.WriteLine("No metrics to watch in config file.");
                return;
            }

            var metricInfo = GetMetricsInfo();
            var metricList = metrics.Split('|');

            string fileName = DateTime.UtcNow.ToString("'EventHubMetrics_'yyyy'_'MM'_'dd'_'HH'_'mm'_'ss'.csv'");
            Console.WriteLine("Writing data to file: {0}", fileName);
            using (StreamWriter outputFile = new StreamWriter(fileName))
            {
                // Write the file header
                outputFile.WriteLine(string.Join(",", metricList) + ",timestamp");

                while (true)
                {
                    var lookbackTime =
                        DateTime.UtcNow.AddMinutes(-(_howFarToLookBackInMin))
                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK"); // 2013-02-23T13:05:09.5409008Z
                    Console.WriteLine("Getting metrics since {0}...", lookbackTime);

                    for (int i = 0; i < metricList.Length; i++)
                    {
                        var metric = metricList[i];
                        var metricValues = GetMetric(metric, lookbackTime);
                        // Only get the most recent metric

                        MetricValue value = (metricValues == null || metricValues.Length == 0)
                            ? new MetricValue()
                            : metricValues.OrderByDescending(v => v.Timestamp).FirstOrDefault();

                        // Figure out what value to use from the metric info
                        var info = metricInfo.FirstOrDefault(mi => mi.Name == metric);
                        if (info == null)
                        {
                            Console.WriteLine("Cannot find info for metric " + metric);
                            return;
                        }
                        string valToUse;
                        switch (info.PrimaryAggregation)
                        {
                            case "Total":
                                valToUse = value.Total.ToString();
                                break;
                            case "Max":
                                valToUse = value.Max.ToString();
                                break;
                            case "Average":
                                valToUse = value.Average.ToString();
                                break;

                            default:
                                Console.WriteLine("Unknown aggregation value {0} for metric {1}",
                                    info.PrimaryAggregation,
                                    metric);
                                return;
                        }
                        Console.WriteLine("{0} Value: {1} {2} At: {3}", metric, valToUse, info.Unit, value.Timestamp);
                        outputFile.Write(valToUse + ",");
                        if (i == metricList.Length - 1)
                        {
                            outputFile.WriteLine(value.Timestamp);
                            outputFile.Flush();
                        }
                    }

                    // Wait the appropriate amount of time
                    int minToWait;
                    switch (_rollupPeriod)
                    {
                        case "PT5M":
                            minToWait = 5;
                            break;
                        case "PT1H":
                            minToWait = 60;
                            break;
                        case "P1D":
                            minToWait = (24*60);
                            break;
                        case "P7D":
                            minToWait = (24*60*7);
                            break;
                        default:
                            Console.WriteLine("Unknown rollup period: " + _rollupPeriod);
                            return;
                    }

                    Console.Write("Waiting {0} minutes", minToWait);
                    for (int i = 0; i < minToWait; i++)
                    {
                        Console.Write(".");
                        Task.Delay(1000*60).Wait();
                    }

                }
            }
        }
        private static MetricValue[] GetMetric(string metricName, string lookbackTime)
        {
            var uri = GetUriBase() + metricName + "/Rollups/" + _rollupPeriod + "/Values?$filter=Timestamp%20ge%20datetime'" + lookbackTime + "'";

            var jsonStr = GetResponseString(uri);
            if (string.IsNullOrEmpty(jsonStr))
            {
                Console.WriteLine("Error getting metric {0}.", metricName);
                return null;
            }

            return JsonConvert.DeserializeObject<MetricValue[]>(jsonStr);
        }

        private static void PrintAvailableMetrics()
        {
            MetricInfo[] items = GetMetricsInfo();
            foreach (MetricInfo info in items)
            {
                Console.WriteLine(info.ToString());
                Console.WriteLine("Rollups for this metric:");
                foreach (var rollup in info.Rollups)
                {
                    Console.WriteLine(rollup.ToString());
                }
            }
        }

        private static MetricInfo[] GetMetricsInfo()
        {
            Console.WriteLine("Getting available metrics...");
            var uri = GetUriBase();

            var jsonStr = GetResponseString(uri);
            if (string.IsNullOrEmpty(jsonStr))
            {
                Console.WriteLine("Error getting metrics.");
                return null;
            }

            return JsonConvert.DeserializeObject<MetricInfo[]>(jsonStr);
        }

        private static string GetUriBase()
        {
            return "https://management.core.windows.net/" + _subscriptionId + "/services/servicebus/Namespaces/" + _serviceBusNameSpace + "/EventHubs/" + _eventHubName + "/Metrics/";
        }

        private static string GetResponseString(string uri)
        {
            var request = WebRequest.Create(uri) as HttpWebRequest;
            
            // Add Microsoft Azure subscription management Certificate to the request
            if (request != null)
            {
                request.ClientCertificates.Add(GetManagementCertificate());
                request.Accept = "application/json";
                request.Headers.Add("x-ms-version", "2012-03-01");
                request.ContentType = "application/json";
                request.Method = "GET";
                request.KeepAlive = true;

                try
                {
                    using (var response = request.GetResponse() as HttpWebResponse)
                    {
                        if (response != null && response.StatusCode == HttpStatusCode.OK)
                        {
                            using (var stream = response.GetResponseStream())
                            {
                                if (stream != null)
                                {
                                    StreamReader sr = new StreamReader(stream);
                                    return sr.ReadToEnd();
                                }
                            }
                        }
                    }
                }
                catch (WebException ex)
                {
                    Console.WriteLine("Bad request: " + ex.Message);

                    // Check for a response
                    var respStr = ex.Response.GetResponseStream();
                    if (respStr != null)
                    {
                        var respBody = new StreamReader(respStr).ReadToEnd();
                        Console.WriteLine("Response: " + respBody);
                    }
                    throw;
                }
            }
            return string.Empty;
        }

        private static X509Certificate2 GetManagementCertificate()
        {
            string certificateThumbprint = ConfigurationManager.AppSettings["CertificateThumbprint"];
            var locations = new List<StoreLocation>
            {
                StoreLocation.LocalMachine,
                StoreLocation.CurrentUser
            };

            foreach (var store in locations.Select(location => new X509Store(StoreName.My, location)))
            {
                try
                {
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint,
                        false);
                    if (certificates.Count > 0)
                    {
                        return certificates[0];
                    }
                }
                finally
                {
                    store.Close();
                }
            }

            throw new ArgumentException("Certificate Cannot Be Found:" + certificateThumbprint);
        }

        public class MetricInfo
        {
            public string DisplayName { get; set; }
            public string Name { get; set; }
            public string Unit { get; set; }
            public string PrimaryAggregation { get; set; }
            public List<Rollup> Rollups { get; set; }

            public override string ToString()
            {
                return string.Format("Display Name: {0} Name: {1} Unit: {2} Aggregation: {3}", DisplayName, Name, Unit,
                    PrimaryAggregation);
            }
        }

        public class Rollup
        {
            public TimeSpan TimeGrain { get; set; }
            public TimeSpan Retention { get; set; }
            public ICollection<MetricValue> Values { get; set; }

            public override string ToString()
            {
                return string.Format("Time Grain: {0} Retention: {1}", TimeGrain, Retention);
            }
        }

        public class MetricValue
        {
            public long Min { get; set; }
            public long Max { get; set; }
            public long Total { get; set; }
            public double Average { get; set; }
            public DateTime Timestamp { get; set; }

            public override string ToString()
            {
                return string.Format("Max: {0} Min: {1} Total: {2} Average: {3} Timestamp: {4}", Max, Min, Total,
                    Average, Timestamp);
            }
        }
    }
}

using Nfield.Infrastructure;
using Nfield.Models;
using Nfield.Services;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CommandLine;
using System.IO;

namespace NfieldDL
{
    class Program
    {
        private static void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
           Console.WriteLine($"Downloading: {e.ProgressPercentage}%");
        }

        private static void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Console.WriteLine("The download has been cancelled");
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                Console.WriteLine($"An error ocurred while trying to download file: {e.Error}");
                return;
            }

            Console.WriteLine("File succesfully downloaded");
        }

        private static Task DownloadDataFileAsync(string uri, string destinationFilename)
        {
            if (File.Exists(destinationFilename))
                throw new IOException($"File already exists: {destinationFilename}");
            using (WebClient wc = new WebClient())
            {
                wc.DownloadProgressChanged += DownloadProgressChanged;
                wc.DownloadFileCompleted += DownloadFileCompleted;

                return wc.DownloadFileTaskAsync(new System.Uri(uri), destinationFilename);
            }
        }

        public class Options
        {
            [Option("user", Required = true, HelpText = "Nfield API user name")]
            public string User { get; set; }

            [Option("password", Required = true, HelpText = "Nfeld API user password")]
            public string Password { get; set; }

            [Option("domain", Required = false, Default = "Kantar Polska", HelpText = "Nfeld API domain name")]
            public string Domain { get; set; }

            [Option("surveyid", Required = true, HelpText = "Survey Id for download ")]
            public string SurveyId { get; set; }

            [Option("successfulliveinterviewdata", Required = false, Default = true, HelpText = "Download SuccessfulLiveInterviewData")]
            public bool SuccessfulLiveInterviewData { get; set; }

            [Option("notsuccessfulliveinterviewdata", Required = false, Default = false, HelpText = "Download NotSuccessfulLiveInterviewData")]
            public bool NotSuccessfulLiveInterviewData { get; set; }

            [Option("openanswerdata", Required = false, Default = true, HelpText = "Download OpenAnswerData")]
            public bool OpenAnswerData { get; set; }

            [Option("closedanswerdata", Required = false, Default = true, HelpText = "Download ClosedAnswerData")]
            public bool ClosedAnswerData { get; set; }

            [Option("suspendedliveinterviewdata", Required = false, Default = false, HelpText = "Download SuspendedLiveInterviewData")]
            public bool SuspendedLiveInterviewData { get; set; }

            [Option("capturedmedia", Required = false, Default = true, HelpText = "Download CapturedMedia")]
            public bool CapturedMedia { get; set; }

            [Option("paradata", Required = false, Default = true, HelpText = "Download ParaData")]
            public bool ParaData { get; set; }

            [Option("testinterviewdata", Required = false, Default = false, HelpText = "Download TestInterviewData")]
            public bool TestInterviewDataData { get; set; }

            [Option("filename", Required = false, Default = "NfieldDownloadFileName.zip", HelpText = "Download FileName")]
            public string FileName { get; set; }

            [Option("startdate", Required = false, HelpText = "UTC StartDate - ex: \"2019-09-01T00:00:00Z\"  All data gets downloaded when no date is provided")]
            public string StartDate { get; set; }

            [Option("enddate", Required = false, HelpText = "UTC EndDate - ex: \"2019-12-31T23:59:59Z\"  All data gets downloaded when no date is provided")]
            public string EndDate { get; set; }

        }

        static void Main(string[] args)
        {
            Console.WriteLine("NField API survey data downloader commandline tool v.0.9 By DDI (@piachu), Copyright 2020\r\n");
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(options =>
                   {
                       try
                       {
                           if (File.Exists(options.FileName))
                               throw new IOException($"File already exists: {options.FileName}");

                           INfieldConnection connection = NfieldConnectionFactory.Create(new Uri("https://api.nfieldmr.com/v1/"));
                           connection.SignInAsync(options.Domain, options.User, options.Password).Wait();

                           INfieldSurveyDataService surveyDataService = connection.GetService<INfieldSurveyDataService>();

                           SurveyDownloadDataRequest myRequest = new SurveyDownloadDataRequest
                           {
                               DownloadSuccessfulLiveInterviewData = options.SuccessfulLiveInterviewData,
                               DownloadNotSuccessfulLiveInterviewData = options.NotSuccessfulLiveInterviewData,
                               DownloadOpenAnswerData = options.OpenAnswerData,
                               DownloadClosedAnswerData = options.ClosedAnswerData,
                               DownloadSuspendedLiveInterviewData = options.SuspendedLiveInterviewData,
                               DownloadCapturedMedia = options.CapturedMedia,
                               DownloadParaData = options.ParaData,
                               DownloadTestInterviewData = options.TestInterviewDataData,
                               DownloadFileName =  Path.GetFileNameWithoutExtension(options.FileName),
                               // UTC time start of today
                               //StartDate = DateTime.ParseExact("2019-09-01T00:00:00Z", "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                               //EndDate = DateTime.ParseExact("2019-12-31T23:59:59Z", "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                               //SurveyId = "da96674b-dd83-4656-8305-11fc28f0b206"
                               StartDate = options.StartDate,
                               EndDate = options.EndDate,
                               SurveyId = options.SurveyId
                           };

                           var task = surveyDataService.PostAsync(myRequest).Result;

                           // request the background tasks service 
                           var backgroundTasksService = connection.GetService<INfieldBackgroundTasksService>();

                           // Example of performing operations on background tasks.
                           var backgroundTaskQuery = backgroundTasksService.QueryAsync().Result.Where(s => s.Id == task.Id);
                           var mybackgroundTask = backgroundTaskQuery.FirstOrDefault();

                           var status = mybackgroundTask.Status;
                           while ((status == BackgroundTaskStatus.Running) || (status == BackgroundTaskStatus.Created))
                           {
                               Console.WriteLine($"Status: {status.ToString()}");
                               Console.WriteLine("Waiting 5s...");
                               Task.Delay(5000).Wait();
                               backgroundTaskQuery = backgroundTasksService.QueryAsync().Result.Where(s => s.Id == task.Id);
                               mybackgroundTask = backgroundTaskQuery.FirstOrDefault();

                               status = mybackgroundTask.Status;
                           }
                           if (mybackgroundTask.Status == BackgroundTaskStatus.SuccessfullyCompleted)
                           {
                               Console.WriteLine($"Status: {status.ToString()}");
                               Console.WriteLine($"Downloading file: {mybackgroundTask.ResultUrl}");
                               Console.WriteLine($"Downloading to: {myRequest.DownloadFileName}.zip");

                               DownloadDataFileAsync(mybackgroundTask.ResultUrl, $"{options.FileName}").Wait();
                           }
                           else
                           {
                               // Canceled = 2 or Faulted = 3                
                               if (mybackgroundTask.Status == BackgroundTaskStatus.Faulted)
                               {
                                   Console.WriteLine($"Faiure! Status: {status.ToString()}");
                                   Environment.Exit(-254);
                               }
                               else
                               {
                                   Console.WriteLine($"Download cancelled. Status: {status.ToString()}");
                                   Environment.Exit(-1);
                               }
                           }
                           Console.WriteLine("Finished succesfuly");
                       }
                       catch(Exception e)
                       {
                           Console.WriteLine("Exception occured");
                           Console.WriteLine(e.Message);
                       }
                       
                   });
        }
    }
}

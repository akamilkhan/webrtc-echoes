using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using EmbedIO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

namespace ARTICARES
{
    public class Options
    {
        public const string DEFAULT_WEBSERVER_LISTEN_URL = "http://*:8080/";
        public const LogEventLevel DEFAULT_VERBOSITY = LogEventLevel.Information;
        public const int TEST_TIMEOUT_SECONDS = 10;

        [Option('l', "listen", Required = false, Default = DEFAULT_WEBSERVER_LISTEN_URL,
            HelpText = "The URL the web server will listen on.")]
        public string ServerUrl { get; set; }

        [Option("timeout", Required = false, Default = TEST_TIMEOUT_SECONDS,
            HelpText = "Timeout in seconds to close the peer connection. Set to 0 for no timeout.")]
        public int TestTimeoutSeconds { get; set; }

        [Option('v', "verbosity", Required = false, Default = DEFAULT_VERBOSITY,
            HelpText = "The log level verbosity (0=Verbose, 1=Debug, 2=Info, 3=Warn...).")]
        public LogEventLevel Verbosity { get; set; }
    }

    class Program
    {
        public static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;
        private static List<IPAddress> _icePresets = new List<IPAddress>();


        static void Main(string[] args)
        {
           
            string listenUrl = Options.DEFAULT_WEBSERVER_LISTEN_URL;
            LogEventLevel verbosity = Options.DEFAULT_VERBOSITY;
            int pcTimeout = Options.TEST_TIMEOUT_SECONDS;

            if (args != null)
            {
                Options opts = null;
                var parseResult = Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(o => opts = o);

                listenUrl = opts != null && !string.IsNullOrEmpty(opts.ServerUrl) ? opts.ServerUrl : listenUrl;
                verbosity = opts != null ? opts.Verbosity : verbosity;
                pcTimeout = opts != null ? opts.TestTimeoutSeconds : pcTimeout;
            }

            logger = AddConsoleLogger(verbosity);

            logger.LogInformation($"Welcome to H-MAN - H-MAN Demo Application!");

            logger.LogInformation($"Press 1 to run application as patient OR Press 2 to run application as therapist.");
            int app_id = int.Parse(Console.ReadLine());

            if (app_id == 1)
            {
                logger.LogInformation($"Running application as patient.");
                Server patient = new Server(logger);

                patient.run_test();

            }
            else
            {
                logger.LogInformation($"Running application as therapist.");
                Client therapist = new Client(logger);
                // Create a string variable and get user input from the keyboard and store it in the variable
                therapist.JoinRoom("1");

            }

            while (true) { Task.Delay(1000); }


        }


        private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger(
            LogEventLevel logLevel = LogEventLevel.Debug)
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(logLevel)
                .WriteTo.Console()
                .CreateLogger();
            var factory = new SerilogLoggerFactory(serilogLogger);
            SIPSorcery.LogFactory.Set(factory);
            return factory.CreateLogger<Program>();
        }
    }

}

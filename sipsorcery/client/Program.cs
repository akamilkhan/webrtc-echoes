//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: Implements a WebRTC Echo Test Client suitable for interoperability
// testing as per specification at:
// https://github.com/sipsorcery/webrtc-echoes/blob/master/doc/EchoTestSpecification.md
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 19 Feb 2021	Aaron Clauson	Created, Dublin, Ireland.
// 14 Apr 2021  Aaron Clauson   Added data channel support.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorcery.Sys;
using SIPSorceryMedia.Abstractions;
using System.Diagnostics;
using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Text.Json;


namespace webrtc_echo
{
    public enum WebRTCTestTypes
    {
        PeerConnection = 0,
        DataChannelEcho = 1
    }

    public class Options
    {
        public const string DEFAULT_ECHO_SERVER_URL = "http://localhost:8080/offer";
        public const LogEventLevel DEFAULT_VERBOSITY = LogEventLevel.Information;
        public const int TEST_TIMEOUT_SECONDS = 10;

        [Option('s', "server", Required = false, Default = DEFAULT_ECHO_SERVER_URL,
            HelpText = "Perform a data channel echo test where the success condition is receiving back a data channel message.")]
        public string ServerUrl { get; set; }

        [Option('t', "test", Required = false, Default = WebRTCTestTypes.PeerConnection,
            HelpText = "The type of test to perform. Options are 'PeerConnection' or 'DataChannelEcho'.")]
        public WebRTCTestTypes TestType { get; set; }

        [Option("timeout", Required = false, Default = TEST_TIMEOUT_SECONDS,
            HelpText = "Timeout in seconds to close the peer connection. Set to 0 for no timeout.")]
        public int TestTimeoutSeconds { get; set; }

        [Option('v', "verbosity", Required = false, Default = DEFAULT_VERBOSITY,
            HelpText = "The log level verbosity (0=Verbose, 1=Debug, 2=Info, 3=Warn...).")]
        public LogEventLevel Verbosity { get; set; }
    }

    class Program
    {
        private const int SUCCESS_RESULT = 0;
        private const int FAILURE_RESULT = 1;

        private static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;

        static async Task<int> Main(string[] args)
        {
            int count = 0;
            Stopwatch sw = new Stopwatch();

            Console.WriteLine("Starting webrtc echo test client.");

            WebRTCTestTypes testType = WebRTCTestTypes.DataChannelEcho;
            string echoServerUrl = Options.DEFAULT_ECHO_SERVER_URL;
            LogEventLevel verbosity = Options.DEFAULT_VERBOSITY;
            int pcTimeout = Options.TEST_TIMEOUT_SECONDS;

            if (args?.Length == 1)
            {
                echoServerUrl = args[0];
            }
            else if (args?.Length > 1)
            {
                Options opts = null;
                var parseResult = Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(o => opts = o);

                testType = opts != null ? opts.TestType : testType;
                echoServerUrl = opts != null && !string.IsNullOrEmpty(opts.ServerUrl) ? opts.ServerUrl : echoServerUrl;
                verbosity = opts != null ? opts.Verbosity : verbosity;
                pcTimeout = opts != null ? opts.TestTimeoutSeconds : pcTimeout;
            }

            logger = AddConsoleLogger(verbosity);

            logger.LogDebug($"Performing test {testType} to {echoServerUrl}.");

            var pc = await CreatePeerConnection();
            var offer = pc.createOffer();
            await pc.setLocalDescription(offer);

            bool success = false;
            ManualResetEventSlim testComplete = new ManualResetEventSlim();

            var dc = pc.DataChannels.FirstOrDefault();

            if (dc != null)
            {
                int msg_size = 100;
                var pseudo = Crypto.GetRandomString(msg_size);

                dc.onopen += () =>
                {
                    logger.LogInformation($"Data channel {dc.label}, stream ID {dc.id} opened.");
                    logger.LogInformation($"Test Started.");

                    sw.Start();
                    dc.send(pseudo);
                    logger.LogDebug($"Data Send: {pseudo}");
                };

                dc.onmessage += (dc, proto, data) =>
                {
                    string echoMsg = Encoding.UTF8.GetString(data);
                    //sw.Stop();

                    logger.LogDebug($"data channel onmessage {proto}: {echoMsg} : {sw.ElapsedMilliseconds} ms.");

                    if (echoMsg == pseudo)
                    {
                        //logger.LogInformation($"Data channel echo test success.");

                        if (testType == WebRTCTestTypes.DataChannelEcho)
                        {
                            count++;
                            pseudo = Crypto.GetRandomString(msg_size);
                            //pseudo = pseudo; // + count.ToString();
                            dc.send(pseudo);
                            logger.LogDebug($"Data Send: {pseudo}");
                            if (count == 1000)
                            {
                                sw.Stop();
                                logger.LogInformation($"Test Complete | Number so string send: {count} | Average RTT: {sw.ElapsedMilliseconds / (float)count}");
                                success = true;
                                testComplete.Set();
                            }
                        }
                    }
                    else
                    {
                        logger.LogWarning($"Data channel echo test failed, echoed message of {echoMsg} did not match original of {pseudo}.");
                    }
                };
            }

            pc.onconnectionstatechange += (state) =>
            {
                logger.LogInformation($"Peer connection state changed to {state}.");

                if (state == RTCPeerConnectionState.disconnected || state == RTCPeerConnectionState.failed)
                {
                    pc.Close("remote disconnection");
                }
                else if (state == RTCPeerConnectionState.connected &&
                    testType == WebRTCTestTypes.PeerConnection)
                {
                    success = true;
                    testComplete.Set();
                }
            };

            logger.LogInformation($"Posting offer to Signaling Server.");
            //////////////////CREATE OFFER//////////////////////////////////////
            string projectId = "webrtc-signaling-57733";
            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
            FirestoreDb db = FirestoreDb.Create(projectId);

            var docRef = db.Collection("rooms-test").Document("1");
            Dictionary<string, string> Offer_dic = new Dictionary<string, string>
            {
                { "type","offer" },{ "sdp",offer.sdp},
            };

            Dictionary<string, Dictionary<string, string>> data = new Dictionary<string, Dictionary<string, string>>
            {
                { "offer",Offer_dic },
            };

            logger.LogDebug($"{offer.toJSON()}");

            var writeResult = await docRef.SetAsync(data);
            logger.LogInformation($"Offer Send and Room Created: {docRef.Id}");

            logger.LogDebug($"Sending ICE Candidate Handler being Registered.");
            /////////////////////////////////////////////////////////////////////
            //////////////////Send ICE Candidate//////////////////////////////////////

            var callerCandidateDocRef = docRef.Collection("callerCandidates");

            pc.onicecandidate += (candidate) =>
            {
                logger.LogInformation($"ICE Candidate Created: {candidate.toJSON()}");
                //callerCandidateDocRef.SetAsync(JsonConvert.SerializeObject(candidate.toJSON()));
                //JsonSerializer.Serialize(candidate.toJSON());
                Dictionary<string, object> ice_candidate = new Dictionary<string, object>
            {
                { "candidate", candidate.candidate},
                { "sdpMLineIndex", candidate.sdpMLineIndex },
                { "sdpMid", candidate.sdpMid },
                    {"usernameFragment",candidate.usernameFragment }
            };
                callerCandidateDocRef.AddAsync(ice_candidate).Wait();
            };
            /////////////////////////////////////////////////////////////////////
            logger.LogDebug($"Handler for Listen for ANSWER is being Registered.");
            //////////////////Listen for answer//////////////////////////////////////
            FirestoreChangeListener listener = docRef.Listen(snapshot =>
            {
                Console.WriteLine("Callback received document snapshot.");
                Console.WriteLine("Document exists? {0}", snapshot.Exists);
                if (snapshot.Exists)
                {
                    Dictionary<string, object> snapshotDic = snapshot.ToDictionary();
                    var snapshotDicJson = JsonSerializer.Serialize(snapshotDic);
                    Console.WriteLine("Document data for {0} document:{1}", snapshot.Id, snapshotDicJson);

                    //foreach (KeyValuePair<string, object> pair in snapshotDic)
                    //{
                    //    Console.WriteLine("{0}: {1}", pair.Key, pair.Value);
                    //}
                    if(snapshot.ContainsField("answer"))
                    {
                        var answer = snapshotDic["answer"];
                        var answerJson = JsonSerializer.Serialize(answer);
                        if (RTCSessionDescriptionInit.TryParse(answerJson.ToString(), out var answerInit))
                        {
                            logger.LogDebug($"SDP answer: {answerInit.sdp}");

                            var setAnswerResult = pc.setRemoteDescription(answerInit);
                            if (setAnswerResult != SetDescriptionResultEnum.OK)
                            {
                                logger.LogWarning($"Set remote description failed {setAnswerResult}.");
                            }
                        }
                        else
                        {
                            logger.LogWarning($"Failed to parse SDP answer from echo server: {snapshot}.");
                        }
                    }

                }

            });
            /////////////////////////////////////////////////////////////////////
            logger.LogDebug($"Handler for Listen for Remote ICE Candidates is being Registered.");
            //////////////////Listen for remote ICE candidates below//////////////////////////////////////
            FirestoreChangeListener listener_remote_ice = docRef.Collection("calleeCandidates").Listen(snapshot =>
            {
                foreach (DocumentChange change in snapshot.Changes)
                {
                    if (change.ChangeType.ToString() == "Added")
                    {
                        logger.LogDebug("New: {0}", change.Document.Id);

                        Dictionary<string, object> snapshotDic = change.Document.ToDictionary();
                        var CandidateJson = JsonSerializer.Serialize(snapshotDic);
                        logger.LogInformation($"ICE Candidate Created: {CandidateJson}");

                        RTCIceCandidateInit CandidateInit;
                        if (RTCIceCandidateInit.TryParse(CandidateJson, out CandidateInit) == true)
                        {
                            logger.LogDebug($"offer parsing to RTCIceCandidateInit successful");
                            pc.addIceCandidate(CandidateInit);


                        }
                        else
                        {
                            logger.LogWarning($"offer parsing to RTCIceCandidateInit failed");

                        }
                    }
                    else if (change.ChangeType.ToString() == "Modified")
                    {
                        logger.LogWarning("Modified: {0}", change.Document.Id);
                    }
                    else if (change.ChangeType.ToString() == "Removed")
                    {
                         logger.LogWarning("Removed: {0}", change.Document.Id);
                    }

                }
            });

            pc.OnClosed += testComplete.Set;

            logger.LogDebug($"Test timeout is {pcTimeout} seconds.");
            testComplete.Wait(pcTimeout * 100000);

            if (!pc.IsClosed)
            {
                pc.close();
                await Task.Delay(1000);
            }

            logger.LogInformation($"{testType} test success {success }.");
            while (true) { Task.Delay(1000); }
            return (success) ? SUCCESS_RESULT : FAILURE_RESULT;
        }

        private static async Task<RTCPeerConnection> CreatePeerConnection()
        {
            const string STUN_URL = "stun:stun1.l.google.com:19302";

       

            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = STUN_URL } },
                X_DisableExtendedMasterSecretKey = true
            };

            var pc = new RTCPeerConnection(config);

            MediaStreamTrack audioTrack = new MediaStreamTrack(SDPWellKnownMediaFormatsEnum.PCMU);
            pc.addTrack(audioTrack);

            var dc = await pc.createDataChannel("sipsocery-dc");

            pc.onicecandidateerror += (candidate, error) => logger.LogWarning($"Error adding remote ICE candidate. {error} {candidate}");
            pc.OnTimeout += (mediaType) => logger.LogWarning($"Timeout for {mediaType}.");
            pc.oniceconnectionstatechange += (state) => logger.LogInformation($"ICE connection state changed to {state}.");
            pc.onsignalingstatechange += () => logger.LogInformation($"Signaling state changed to {pc.signalingState}.");
            pc.OnReceiveReport += (re, media, rr) => logger.LogDebug($"RTCP Receive for {media} from {re}\n{rr.GetDebugSummary()}");
            pc.OnSendReport += (media, sr) => logger.LogDebug($"RTCP Send for {media}\n{sr.GetDebugSummary()}");
            pc.OnRtcpBye += (reason) => logger.LogDebug($"RTCP BYE receive, reason: {(string.IsNullOrWhiteSpace(reason) ? "<none>" : reason)}.");

            pc.onsignalingstatechange += () =>
            {
                if (pc.signalingState == RTCSignalingState.have_remote_offer)
                {
                    logger.LogTrace("Remote SDP:");
                    logger.LogTrace(pc.remoteDescription.sdp.ToString());
                }
                else if (pc.signalingState == RTCSignalingState.have_local_offer)
                {
                    logger.LogTrace("Local SDP:");
                    logger.LogTrace(pc.localDescription.sdp.ToString());
                }
            };

            return pc;
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

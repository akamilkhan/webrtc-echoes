//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: Implements a WebRTC Echo Test Server suitable for interoperability
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

namespace webrtc_demo
{
    public class Signaling
    {
        public static string projectId;
        public static void configure_signaling_server()
        {
            projectId = "webrtc-signaling-57733";
            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
        }
        public static async void create_database()
        {
            //FirebaseApp.Create(new AppOptions()
            //{
            //    Credential = GoogleCredential.FromFile("path/to/refreshToken.json"),
            //});

            FirestoreDb db = FirestoreDb.Create(projectId);
            // Create a document with a random ID in the "users" collection.
            CollectionReference collection = db.Collection("users");
            DocumentReference document = await collection.AddAsync(new { Name = new { First = "Ada", Last = "Lovelace" }, Born = 1815 });

        }
        public static async Task GetAllDocs()
        {
            FirestoreDb db = FirestoreDb.Create(webrtc_demo.Signaling.projectId);

            var RoomRef = db.Collection("rooms");
            QuerySnapshot snapshot = await RoomRef.GetSnapshotAsync();
            foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
            {
                Console.WriteLine("Document data for {0} document:", documentSnapshot.Id);
                Dictionary<string, object> city = documentSnapshot.ToDictionary();
                foreach (KeyValuePair<string, object> pair in city)
                {
                    Console.WriteLine("{0}: {1}", pair.Key, pair.Value);
                }
                Console.WriteLine("");
            }
        }

    }

    public class FirestoreSignaling
    {
        private readonly FirestoreDb _db;

        public FirestoreSignaling()
        {
            string projectId = "webrtc-signaling-57733";
            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
            _db = FirestoreDb.Create(projectId);
        }

        //    // Implement Firestore signaling methods here
        //    public async Task<string> CreateRoomAsync()
        //    {
        //        var room = new Dictionary<string, object>
        //{
        //    { "createdAt", Timestamp.GetCurrentTimestamp() },
        //};
        //        var roomRef = await _db.Collection("rooms").AddAsync(room);
        //        return roomRef.Id;
        //    }

        //    public async Task JoinRoomAsync(string roomId)
        //    {
        //        var roomRef = _db.Collection("rooms").Document(roomId);
        //        var snapshot = await roomRef.GetSnapshotAsync();

        //        if (!snapshot.Exists)
        //        {
        //            throw new InvalidOperationException($"Room '{roomId}' not found.");
        //        }
        //    }

        //    public async Task SendMessageAsync(string roomId, string senderId, string content)
        //    {
        //        var roomRef = _db.Collection("rooms").Document(roomId);
        //        var message = new Dictionary<string, object>
        //{
        //    { "senderId", senderId },
        //    { "content", content },
        //    { "sentAt", Timestamp.GetCurrentTimestamp() },
        //};
        //        await roomRef.Collection("messages").AddAsync(message);
        //    }

        //    public async Task<ICollection<DocumentSnapshot>> GetMessagesAsync(string roomId)
        //    {
        //        var roomRef = _db.Collection("rooms").Document(roomId);
        //        var querySnapshot = await roomRef.Collection("messages").OrderBy("sentAt").GetSnapshotAsync();
        //        return querySnapshot.Documents;
        //    }
        //    private async Task<RTCDataChannel> CreateDataChannelAsync()
        //    {
        //        var pc = new RTCPeerConnection();
        //        var dataChannel = pc.createDataChannel("dataChannel");

        //        // Implement SIPSorcery WebRTC data channel logic here

        //        return dataChannel;
        //    }

        //    private async Task RunAsync()
        //    {
        //        //InitializeFirebase();
        //        var signaling = new FirestoreSignaling();
        //        var roomId = await signaling.CreateRoomAsync();
        //        Console.WriteLine($"Room '{roomId}' created.");

        //        var pc = new RTCPeerConnection();
        //        var dataChannel = pc.createDataChannel("dataChannel");

        //        pc.onicecandidate += async (RTCIceCandidate candidate) =>
        //        {
        //            if (candidate != null)
        //            {
        //                await signaling.SendMessageAsync(roomId, "system", JsonConvert.SerializeObject(candidate.toJSON()));
        //            }
        //        };

        //        pc.ondatachannel += (RTCDataChannel remoteDataChannel) =>
        //        {
        //            remoteDataChannel.onmessage += (string remoteMessage) =>
        //            {
        //                Console.WriteLine($"Received message: {remoteMessage}");
        //            };
        //        };

        //        var offerOptions = new RTCOfferOptions { OfferToReceiveAudio = false, OfferToReceiveVideo = false };
        //        var offer = await pc.createOffer(offerOptions);
        //        await pc.setLocalDescription(offer);
        //        await signaling.SendMessageAsync(roomId, "system", JsonConvert.SerializeObject(offer));

        //        while (true)
        //        {
        //            var messages = await signaling.GetMessagesAsync(roomId);
        //            foreach (var message in messages)
        //            {
        //                if (message.TryGetValue("content", out object content) && content is string contentStr)
        //                {
        //                    var senderId = message.GetString("senderId");
        //                    if (senderId != "system") // Process only messages from remote peers
        //                    {
        //                        var desc = JsonConvert.DeserializeObject<RTCSessionDescriptionInit>(contentStr);
        //                        if (desc.type == RTCSdpType.answer)
        //                        {
        //                            await pc.setRemoteDescription(desc);
        //                        }
        //                        else if (desc.type == RTCSdpType.offer)
        //                        {
        //                            await pc.setRemoteDescription(desc);
        //                            var answer = await pc.createAnswer(null);
        //                            await pc.setLocalDescription(answer);
        //                            await signaling.SendMessageAsync(roomId, "system", JsonConvert.SerializeObject(answer));
        //                        }
        //                    }
        //                }
        //            }
        //            await Task.Delay(1000); // Poll messages every second
        //        }
        //    }
        //}
    }
}
namespace webrtc_echo
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
        private static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;

        private static List<IPAddress> _icePresets = new List<IPAddress>();

        static void Main(string[] args)
        {
            // Apply any command line options
            //if (args.Length > 0)
            //{
            //    url = args[0];
            //    for(int i=1; i<args.Length; i++)
            //    {
            //        if(IPAddress.TryParse(args[i], out var addr))
            //        {
            //            _icePresets.Add(addr);
            //            Console.WriteLine($"ICE candidate preset address {addr} added.");
            //        }
            //    }
            //}

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
            //FirestoreDb db = FirestoreDb.Create(webrtc_demo.Signaling.projectId);

            //var RoomRef = db.Collection("rooms");

            //logger.LogDebug($"Connected to FireStore Database: {RoomRef.Id}");
            //webrtc_demo.Signaling.GetAllDocs().Wait();

            //try
            //{
            //    var app = new webrtc_demo.FirestoreSignaling();
            //    await app.r.RunAsync();
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Error: {ex.Message}");
            //}

            //Thread.Sleep(2000);
            // Start the web server.
            //using (var server = CreateWebServer(listenUrl, pcTimeout))
            //{
            //    server.RunAsync();

            //    Console.WriteLine("ctrl-c to exit.");
            //    var mre = new ManualResetEvent(false);
            //    Console.CancelKeyPress += (sender, eventArgs) =>
            //    {
            //        // cancel the cancellation to allow the program to shutdown cleanly
            //        eventArgs.Cancel = true;
            //        mre.Set();
            //    };

            //    mre.WaitOne();
            //}
            Console.WriteLine("Enter room Id:");
            string roomId = Console.ReadLine();
            // Create a string variable and get user input from the keyboard and store it in the variable
            JoinRoom(roomId);

            while (true) { Task.Delay(1000); }
        }

        private async static void JoinRoom(string roomId)
        {

            string projectId = "webrtc-signaling-57733";
            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
            FirestoreDb db = FirestoreDb.Create(projectId);

            var RoomRef = db.Collection("rooms-test").Document(roomId);


            //////////////////Listen for offer//////////////////////////////////////
            var roomSnapshot = await RoomRef.GetSnapshotAsync();
            var offer = "";
            if (roomSnapshot.Exists)
            {
                Dictionary<string, object> roomSnapshotDic = roomSnapshot.ToDictionary();
                offer = JsonSerializer.Serialize(roomSnapshotDic["offer"]);
                logger.LogInformation($"Room Snapshot: {offer}");

                if (roomSnapshotDic.ContainsKey("offer"))
                {
                    logger.LogInformation($"Got offer!");
                }
                else
                {
                    logger.LogWarning($"No offer in the snapshot");
                }

            }
            else
            {
                logger.LogWarning($"Snapshot doesn't exist");
            }
            //string offer_key = offer.ToDictionary<string,string>().Values;

            //var offerSD_ = JsonSerializer.Deserialize<string>(offer);
            RTCSessionDescriptionInit offerSD;
            if (RTCSessionDescriptionInit.TryParse(offer, out offerSD) == true)
            {
                logger.LogInformation($"offer parsing to RTCSessionDescriptionInit successful");

            }
            else
            {
                logger.LogWarning($"offer parsing to RTCSessionDescriptionInit failed");

            }


            var echoServer = new WebRTCEchoServer(_icePresets);
            var pc = await echoServer.GotOffer(offerSD);
            if (pc != null)
            {
                //////////////////answer//////////////////////////////////////
                var answer = new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = pc.localDescription.sdp.ToString() };
                Dictionary<string, object> answerDic =

                    new Dictionary<string, object>
                    { 
                        {
                            "answer", new Dictionary<string, object>
                            {
                                { "type", RTCSdpType.answer},
                                { "sdp", pc.localDescription.sdp.ToString() }
                            }
                        }
               };

                await RoomRef.SetAsync(answerDic, SetOptions.MergeAll);

                logger.LogInformation($"Answered: {answer.toJSON()}");

                /////////////////////////////////////////////////////////////////////

                //////////////////Send ICE Candidate//////////////////////////////////////

                var calleeCandidateDocRef = RoomRef.Collection("calleeCandidates");

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
                    calleeCandidateDocRef.AddAsync(ice_candidate).Wait();
                };
                /////////////////////////////////////////////////////////////////////
                //////////////////Listen for remote ICE candidates below//////////////////////////////////////
                FirestoreChangeListener listener_remote_ice = RoomRef.Collection("callerCandidates").Listen(snapshot =>
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
                //            roomRef.collection('calleeCandidates').onSnapshot(snapshot => {
                //            snapshot.docChanges().forEach(async change => {
                //            if (change.type === 'added')
                //            {
                //                let data = change.doc.data();
                //                console.log(`Got new remote ICE candidate: ${ JSON.stringify(data)}`);
                //            await peerConnection.addIceCandidate(new RTCIceCandidate(data));
                //        }
                //});
                //});
                /////////////////////////////////////////////////////////////////////
            }
        }

        private static WebServer CreateWebServer(string url, int pcTimeout)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithCors("*", "*", "*")
                .WithAction("/offer", HttpVerbs.Post, (ctx) => Offer(ctx, pcTimeout))
                .WithStaticFolder("/", "../../html", false);
            server.StateChanged += (s, e) => Console.WriteLine($"WebServer New State - {e.NewState}");

            return server;
        }

        private async static Task Offer(IHttpContext context, int pcTimeout)
        {
            var offer = await context.GetRequestDataAsync<RTCSessionDescriptionInit>();

            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

            var echoServer = new WebRTCEchoServer(_icePresets);
            var pc = await echoServer.GotOffer(offer);

            if (pc != null)
            {
                var answer = new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = pc.localDescription.sdp.ToString() };
                context.Response.ContentType = "application/json";
                using (var responseStm = context.OpenResponseStream(false, false))
                {
                    await JsonSerializer.SerializeAsync(responseStm, answer, jsonOptions);
                }

                if (pcTimeout != 0)
                {
                    logger.LogDebug($"Setting peer connection close timeout to {pcTimeout} seconds.");

                    var timeout = new Timer((state) =>
                    {
                        if (!pc.IsClosed)
                        {
                            logger.LogWarning("Test timed out.");
                            pc.close();
                        }
                    }, null, pcTimeout * 1000, Timeout.Infinite);
                    pc.OnClosed += timeout.Dispose;
                }
            }
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

    public class WebRTCEchoServer
    {
        private const int VP8_PAYLOAD_ID = 96;

        private static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;

        private List<IPAddress> _presetIceAddresses;

        public WebRTCEchoServer(List<IPAddress> presetAddresses)
        {
            logger = SIPSorcery.LogFactory.CreateLogger<WebRTCEchoServer>();
            _presetIceAddresses = presetAddresses;
        }

        public async Task<RTCPeerConnection> GotOffer(RTCSessionDescriptionInit offer)
        {
            logger.LogTrace($"SDP offer received.");
            logger.LogTrace(offer.sdp);
            const string STUN_URL = "stun:stun1.l.google.com:19302";



            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = STUN_URL } },
                X_DisableExtendedMasterSecretKey = true
            };

            var pc = new RTCPeerConnection(config);

            if (_presetIceAddresses != null)
            {
                foreach (var addr in _presetIceAddresses)
                {
                    var rtpPort = pc.GetRtpChannel().RTPPort;
                    var publicIPv4Candidate = new RTCIceCandidate(RTCIceProtocol.udp, addr, (ushort)rtpPort, RTCIceCandidateType.host);
                    pc.addLocalIceCandidate(publicIPv4Candidate);
                }
            }

            SDP offerSDP = SDP.ParseSDPDescription(offer.sdp);

            if (offerSDP.Media.Any(x => x.Media == SDPMediaTypesEnum.audio))
            {
                MediaStreamTrack audioTrack = new MediaStreamTrack(SDPWellKnownMediaFormatsEnum.PCMU);
                pc.addTrack(audioTrack);
            }

            if (offerSDP.Media.Any(x => x.Media == SDPMediaTypesEnum.video))
            {
                MediaStreamTrack videoTrack = new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.VP8, VP8_PAYLOAD_ID));
                pc.addTrack(videoTrack);
            }

            pc.OnRtpPacketReceived += (IPEndPoint rep, SDPMediaTypesEnum media, RTPPacket rtpPkt) =>
            {
                pc.SendRtpRaw(media, rtpPkt.Payload, rtpPkt.Header.Timestamp, rtpPkt.Header.MarkerBit, rtpPkt.Header.PayloadType);
            };

            pc.OnTimeout += (mediaType) => logger.LogWarning($"Timeout for {mediaType}.");
            pc.oniceconnectionstatechange += (state) => logger.LogInformation($"ICE connection state changed to {state}.");
            pc.onsignalingstatechange += () => logger.LogInformation($"Signaling state changed to {pc.signalingState}.");
            pc.onconnectionstatechange += (state) =>
            {
                logger.LogInformation($"Peer connection state changed to {state}.");
                if (state == RTCPeerConnectionState.failed)
                {
                    pc.Close("ice failure");
                }
            };

            pc.ondatachannel += (dc) =>
            {
                logger.LogInformation($"Data channel opened for label {dc.label}, stream ID {dc.id}.");
                dc.onmessage += (rdc, proto, data) =>
                {
                    //logger.LogInformation($"Data channel got message: {Encoding.UTF8.GetString(data)}");
                    rdc.send(Encoding.UTF8.GetString(data));
                };
            };

            var setResult = pc.setRemoteDescription(offer);
            if (setResult == SetDescriptionResultEnum.OK)
            {
                var answer = pc.createAnswer();
                await pc.setLocalDescription(answer);

                logger.LogTrace($"SDP answer created.");
                logger.LogTrace(answer.sdp);

                return pc;
            }
            else
            {
                logger.LogWarning($"Failed to set remote description {setResult}.");
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



using System.Text.Json;
using System.Threading;
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


using System.Net;


namespace ARTICARES
{
    class Client
    {

        private readonly Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;
        private static List<IPAddress> _icePresets = new List<IPAddress>();
        public Client(Microsoft.Extensions.Logging.ILogger logger)
        {
            this.logger = logger;
        }

        public async void JoinRoom(string roomId)
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


        private async Task Offer(IHttpContext context, int pcTimeout)
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




//namespace ARTICARES
//{
//    public class Signaling
//    {
//        public static string projectId;
//        public static void configure_signaling_server()
//        {
//            projectId = "webrtc-signaling-57733";
//            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
//            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
//        }
//        public static async void create_database()
//        {
//            //FirebaseApp.Create(new AppOptions()
//            //{
//            //    Credential = GoogleCredential.FromFile("path/to/refreshToken.json"),
//            //});

//            FirestoreDb db = FirestoreDb.Create(projectId);
//            // Create a document with a random ID in the "users" collection.
//            CollectionReference collection = db.Collection("users");
//            DocumentReference document = await collection.AddAsync(new { Name = new { First = "Ada", Last = "Lovelace" }, Born = 1815 });

//        }
//        public static async Task GetAllDocs()
//        {
//            FirestoreDb db = FirestoreDb.Create(ARTICARES.Signaling.projectId);

//            var RoomRef = db.Collection("rooms");
//            QuerySnapshot snapshot = await RoomRef.GetSnapshotAsync();
//            foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
//            {
//                Console.WriteLine("Document data for {0} document:", documentSnapshot.Id);
//                Dictionary<string, object> city = documentSnapshot.ToDictionary();
//                foreach (KeyValuePair<string, object> pair in city)
//                {
//                    Console.WriteLine("{0}: {1}", pair.Key, pair.Value);
//                }
//                Console.WriteLine("");
//            }
//        }

//    }

//    public class FirestoreSignaling
//    {
//        private readonly FirestoreDb _db;

//        public FirestoreSignaling()
//        {
//            string projectId = "webrtc-signaling-57733";
//            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
//            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
//            _db = FirestoreDb.Create(projectId);
//        }

//    }
//}

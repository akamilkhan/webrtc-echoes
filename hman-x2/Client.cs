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
        private Signaling signaling;
        public Client(Microsoft.Extensions.Logging.ILogger logger)
        {
            this.logger = logger;
        }

        public async void JoinRoom(string roomId)
        {
            Communication.SendSetTargetParams(1, 1, 1, 1);

            signaling = new Signaling(logger, roomId);

            RTCSessionDescriptionInit offerSD = await signaling.LookForOffer(roomId);

            var echoServer = new WebRTCClient(_icePresets);
            var pc = await echoServer.GotOffer(offerSD);
            if (pc != null)
            {
                var answer = new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = pc.localDescription.sdp.ToString() };
                signaling.PublishSession(answer);
                signaling.SendICECandidates(ref pc, "calleeCandidates");
                signaling.ListenForRemoteICECandidates(pc, "callerCandidates");

            }
        }

    }

    public class WebRTCClient
    {
        private const int VP8_PAYLOAD_ID = 96;

        private static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;

        private List<IPAddress> _presetIceAddresses;

        public WebRTCClient(List<IPAddress> presetAddresses)
        {
            logger = SIPSorcery.LogFactory.CreateLogger<WebRTCClient>();
            _presetIceAddresses = presetAddresses;
        }

        public async Task<RTCPeerConnection> GotOffer(RTCSessionDescriptionInit offer)
        {
            logger.LogTrace($"SDP offer received.");
            logger.LogTrace(offer.sdp);


            var messageProtocol = new MessagingProtocol();
            MessagingProtocol.Header header;
            MessagingProtocol.SetTargetParams tparams;
            ulong last_time = 0;

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

            //if (offerSDP.Media.Any(x => x.Media == SDPMediaTypesEnum.audio))
            //{
            //    MediaStreamTrack audioTrack = new MediaStreamTrack(SDPWellKnownMediaFormatsEnum.PCMU);
            //    pc.addTrack(audioTrack);
            //}

            //if (offerSDP.Media.Any(x => x.Media == SDPMediaTypesEnum.video))
            //{
            //    MediaStreamTrack videoTrack = new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.VP8, VP8_PAYLOAD_ID));
            //    pc.addTrack(videoTrack);
            //}

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
                logger.LogInformation($"Data channel {dc.label}, stream ID {dc.id} opened.");
                var pseudo = MessagingProtocol.EncodeSetTargetParams(1, 1, 1, 1);

                dc.send(pseudo);

                dc.onopen += () =>
                {
                    logger.LogInformation($"Data channel {dc.label}, stream ID {dc.id} opened.");
                    var pseudo = MessagingProtocol.EncodeSetTargetParams(1, 1, 1, 1);

                    dc.send(pseudo);
                };
                dc.onmessage += (rdc, proto, data) =>
                {
                    //logger.LogInformation($"Data channel got message: {Encoding.UTF8.GetString(data)}");
                    //rdc.send(Encoding.UTF8.GetString(data));
                    //rdc.send(data);


                    header = MessagingProtocol.DecodeHeader(data);

                    if (header.MessageID == MessagingProtocol.MessageID.CommandMessage)
                    {
                        logger.LogWarning($"CommandMessage Recieved.");
                        if (header.CommandCode == MessagingProtocol.CommandCode.SetTargetParams)
                        {
                            logger.LogWarning($"CommandMessage - SetTargetParams Recieved.");

                            tparams = MessagingProtocol.DecodeSetTargetParams(data);

                            logger.LogDebug(tparams.ToString());

                        }

                    }
                    else if (header.MessageID == MessagingProtocol.MessageID.ResponseMessage)
                    {
                        logger.LogDebug($"ResponseMessage Recieved.");
                        if (header.CommandCode == MessagingProtocol.CommandCode.SetTargetParams)
                        {
                            logger.LogDebug($"ResponseMessage - SetTargetParams Recieved.");
                            
                            MessagingProtocol.SetTargetParamsResponse response = MessagingProtocol.SetTargetParamsResponse.FromByteArray(data);
                            logger.LogDebug(response.ToString());

                            if (last_time == 0)
                                last_time = response.MessageHeader.CommandTimestamp;
                            if (response.MessageHeader.PacketSequenceNumber % 1000 == 0)
                            {
                                logger.LogInformation($"Seq: {response.MessageHeader.PacketSequenceNumber} RTT: {(messageProtocol.Timer.TimestampInMicroSeconds() - last_time) / 1000.0 /1000}ms");


                                last_time = messageProtocol.Timer.TimestampInMicroSeconds();


                            }
                            logger.LogDebug($"Seq: {response.MessageHeader.PacketSequenceNumber} RTT: {(messageProtocol.Timer.TimestampInMicroSeconds() - response.MessageHeader.CommandTimestamp) / 1000.0}ms");

                            //Calculate Stats,
                            // Set target

                            //Command
                            var pseudo = MessagingProtocol.EncodeSetTargetParams(1, 2, 3, 4);

                            dc.send(pseudo);


                        }
                    }
                    else
                    {
                        logger.LogWarning($"ErrorMessage Recieved.");
                    }

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

        private void Dc_onopen()
        {
            throw new NotImplementedException();
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

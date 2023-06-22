using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


namespace ARTICARES
{
    public enum WebRTCTestTypes
    {
        PeerConnection = 0,
        DataChannelEcho = 1
    }
    class Server
    {
        private const int SUCCESS_RESULT = 0;
        private const int FAILURE_RESULT = 1;

        private Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;
        private Signaling signaling;


        private WebRTCTestTypes testType = WebRTCTestTypes.DataChannelEcho;

        public Server(Microsoft.Extensions.Logging.ILogger logger)
        {
            this.logger = logger;
        }


        public async void run()
        {
            Stopwatch sw = new Stopwatch();

            var pc = await CreatePeerConnection();
            var offer = pc.createOffer();
            await pc.setLocalDescription(offer);

            var messageProtocol = new MessagingProtocol();
            MessagingProtocol.Header header;
            MessagingProtocol.SetTargetParams tparams;
            MicroSecondDateTime dataTime = new MicroSecondDateTime();

            bool success = false;

            var dc = pc.DataChannels.FirstOrDefault();

            if (dc != null)
            {
                dc.onopen += () =>
                {
                    logger.LogInformation($"Data channel {dc.label}, stream ID {dc.id} opened.");
                 
                };

                dc.onmessage += (dc, proto, data) =>
                {

                    header = MessagingProtocol.DecodeHeader(data);

                    if(header.MessageID == MessagingProtocol.MessageID.CommandMessage)
                    {
                        logger.LogDebug($"CommandMessage Recieved.");
                        if(header.CommandCode == MessagingProtocol.CommandCode.SetTargetParams)
                        {
                            logger.LogDebug($"CommandMessage - SetTargetParams Recieved.");

                            tparams = MessagingProtocol.DecodeSetTargetParams(data);

                            logger.LogDebug(tparams.ToString());

                            // use target param to set the target of hman

                            // get current position
                            short xPos = 1;
                            short yPos = 2;

                            MessagingProtocol.SetTargetParamsResponse response = new MessagingProtocol.SetTargetParamsResponse
                            {
                                X = xPos,
                                Y = yPos,
                                Vx = 3,
                                Vy = 4,
                                EB = 5,
                                MS = 6,
                                AdditionalInfo = new byte[40],
                                MessageHeader = new MessagingProtocol.Header
                                {
                                    MessageID = MessagingProtocol.MessageID.ResponseMessage,
                                    CommandCode = MessagingProtocol.CommandCode.SetTargetParams,
                                    PacketSequenceNumber = header.PacketSequenceNumber,
                                    CommandTimestamp = header.CommandTimestamp,
                                    ResponseTimestamp = dataTime.TimestampInMicroSeconds(),
                                    PayloadLength = (ushort)MessagingProtocol.MessageSize.SetTargetParamsResponse
                                }
                  
                            };

                            
                            dc.send(response.ToByteArray());


                        }

                    }
                    else if (header.MessageID == MessagingProtocol.MessageID.ResponseMessage)
                    {
                        logger.LogWarning($"ResponseMessage Recieved.");
                    }
                    else
                    {
                        logger.LogWarning($"ErrorMessage Recieved.");
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
            };



            signaling = new Signaling(logger, "1");

            //////////////////Publish Offer//////////////////////////////////////
            logger.LogInformation($"Posting offer to Signaling Server.");
            signaling.PublishSession(offer);
            ////////////////////////////////////////////////////////////////////////


            //////////////////Send ICE Candidate//////////////////////////////////////
            logger.LogDebug($"Sending ICE Candidate Handler being Registered.");
            signaling.SendICECandidates(ref pc, "callerCandidates");
            /////////////////////////////////////////////////////////////////////////

            //////////////////Listen for answer//////////////////////////////////////
            logger.LogDebug($"Handler for Listen for ANSWER is being Registered.");
            signaling.ListenForAnswer(pc);
            ////////////////////////////////////////////////////////////////////////

            ////////////////////Listen for remote ICE candidates below//////////////
            logger.LogDebug($"Handler for Listen for Remote ICE Candidates is being Registered.");
            signaling.ListenForRemoteICECandidates(pc, "calleeCandidates");
            ////////////////////////////////////////////////////////////////////////



            //pc.OnClosed += testComplete.Set;

            //logger.LogDebug($"Test timeout is {pcTimeout} seconds.");

            //if (!pc.IsClosed)
            //{
            //    pc.close();
            //    await Task.Delay(1000);
            //}

            logger.LogInformation($"{testType} test success {success }.");
            while (true) { Task.Delay(1000); }
            //return (success) ? SUCCESS_RESULT : FAILURE_RESULT;
        }

        private async Task<RTCPeerConnection> CreatePeerConnection()
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
//}

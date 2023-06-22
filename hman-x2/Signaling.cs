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
    class Signaling
    {
        public static string projectId;
        DocumentReference docRef;
        DocumentReference RoomRef;
        FirestoreDb db;

        private Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;

        public Signaling(Microsoft.Extensions.Logging.ILogger logger, string roomId)
        {
            configure_signaling_server();
            this.logger = logger;
            this.RoomRef = db.Collection("rooms-test").Document(roomId);
        }
        private void configure_signaling_server()
        {
            projectId = "webrtc-signaling-57733";
            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
            db = FirestoreDb.Create(projectId);
        }

        public async void Publish(RTCSessionDescriptionInit session)
        {
            Dictionary<string, string> Offer_dic = new Dictionary<string, string>
            {
                { "type","offer" },{ "sdp",session.sdp},
            };

            Dictionary<string, Dictionary<string, string>> data = new Dictionary<string, Dictionary<string, string>>
            {
                { "offer",Offer_dic },
            };

            logger.LogDebug($"{session.toJSON()}");

            var writeResult = await docRef.SetAsync(data);
            logger.LogInformation($"Session Descriptor Send and Room Created: {docRef.Id}");

        }

        public async void PublishSession(RTCSessionDescriptionInit session)
        {
            //////////////////answer//////////////////////////////////////
            Dictionary<string, object> SessionDic =

                new Dictionary<string, object>
                {
                        {
                            session.type.ToString(), new Dictionary<string, object>
                            {
                                { "type", session.type.ToString()},
                                { "sdp", session.sdp }
                            }
                        }
           };

            await RoomRef.SetAsync(SessionDic, SetOptions.MergeAll);

            logger.LogInformation($"Send: {session.toJSON()}");

        }
        //void SendICECandidates(RTCPeerConnection pc)
        //{
        //    var callerCandidateDocRef = docRef.Collection("callerCandidates");

        //    pc.onicecandidate += (candidate) =>
        //    {
        //        logger.LogInformation($"ICE Candidate Created: {candidate.toJSON()}");
        //        //callerCandidateDocRef.SetAsync(JsonConvert.SerializeObject(candidate.toJSON()));
        //        //JsonSerializer.Serialize(candidate.toJSON());
        //        Dictionary<string, object> ice_candidate = new Dictionary<string, object>
        //    {
        //        { "candidate", candidate.candidate},
        //        { "sdpMLineIndex", candidate.sdpMLineIndex },
        //        { "sdpMid", candidate.sdpMid },
        //            {"usernameFragment",candidate.usernameFragment }
        //    };
        //        callerCandidateDocRef.AddAsync(ice_candidate).Wait();
        //    };
        //    /////////////////////////////////////////////////////////////////////
        //    logger.LogDebug($"Handler for Listen for ANSWER is being Registered.");
        //}


        public void SendICECandidates(ref RTCPeerConnection pc, string collectionName)
        {
            //////////////////Send ICE Candidate//////////////////////////////////////

            var calleeCandidateDocRef = RoomRef.Collection(collectionName);

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
        }
        public void ListenForAnswer(RTCPeerConnection pc)
        {
            //////////////////Listen for answer//////////////////////////////////////
            FirestoreChangeListener listener = RoomRef.Listen(snapshot =>
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
                    if (snapshot.ContainsField("answer"))
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
            /////////////////////////////////////////////////////////////////////
        }

        public void ListenForRemoteICECandidates(RTCPeerConnection pc, string collectionName)
        {
            //////////////////Listen for remote ICE candidates below//////////////////////////////////////
            FirestoreChangeListener listener_remote_ice = RoomRef.Collection(collectionName).Listen(snapshot =>
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
        }

        //client
        public async Task<RTCSessionDescriptionInit> LookForOffer(string roomId)
        {
            //var RoomRef = db.Collection("rooms-test").Document(roomId);

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
                return offerSD;

            }
            else
            {
                logger.LogWarning($"offer parsing to RTCSessionDescriptionInit failed");
                return null;

            }
        }


    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARTICARES
{
    public class Communication
    {
        public MicroSecondDateTime dataTime;
        public MessagingProtocol messageProtocol;
        public Communication()
        {
            dataTime = new MicroSecondDateTime();
            messageProtocol = new MessagingProtocol();

        }

        void Init()
        {

        }

        public static void SendSetTargetParams(short targetX, short targetY, float kX, float kY )
        {
            byte[] message = MessagingProtocol.EncodeSetTargetParams(targetX, targetY, kX, kY);


            string byteString = BitConverter.ToString(message).Replace("-", "");
            Console.WriteLine(byteString);

            MessagingProtocol.SetTargetParams tparams;
            tparams = MessagingProtocol.DecodeSetTargetParams(message);
            Console.WriteLine(tparams.ToString());

        }
        public static void CreateSetTargetParamsResponse(MessagingProtocol.Header commandHeader)
        {
            //// get current position
            //short xPos = 1;
            //short yPos = 2;

            //MessagingProtocol.SetTargetParamsResponse response = new MessagingProtocol.SetTargetParamsResponse
            //{
            //    X = xPos,
            //    Y = yPos,
            //    Vx = 3,
            //    Vy = 4,
            //    EB = 5,
            //    MS = 6,
            //    AdditionalInfo = new byte[40],
            //    MessageHeader = new MessagingProtocol.Header
            //    {
            //        MessageID = MessagingProtocol.MessageID.ResponseMessage,
            //        CommandCode = MessagingProtocol.CommandCode.SetTargetParams,
            //        PacketSequenceNumber = commandHeader.PacketSequenceNumber,
            //        CommandTimestamp = commandHeader.CommandTimestamp,
            //        ResponseTimestamp = dataTime.TimestampInMicroSeconds(),
            //        PayloadLength = (ushort)MessagingProtocol.MessageSize.SetTargetParamsResponse
            //    }

            //};

        }

        void listen()
        {

        }

    }
}

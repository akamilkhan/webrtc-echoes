using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ARTICARES
{
    public class MicroSecondDateTime
    {
        // Create a Stopwatch and start it
        Stopwatch stopwatch = Stopwatch.StartNew();
        // Store the current timestamp
        DateTimeOffset startTime;
        // Unix epoch
        DateTimeOffset epoch; 
        public MicroSecondDateTime()
        {
            this.stopwatch = new Stopwatch();
            this.stopwatch.Start();
            this.startTime = DateTimeOffset.UtcNow;
            this.epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }
        
       public ulong TimestampInMicroSeconds()
       {
            DateTimeOffset highPrecisionTimestamp = startTime + stopwatch.Elapsed;
            ulong timestampInMicroseconds = (ulong)(highPrecisionTimestamp - epoch).Ticks / 10;
            return timestampInMicroseconds;
       }
    }

    public class MessagingProtocol
    {
        public MicroSecondDateTime Timer;
        public MessagingProtocol()
        {
            this.Timer = new MicroSecondDateTime();
        }

        private static ulong _latestPacketSequenceNumber = 0;
        private static DateTimeOffset epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);



        /// <summary>
        /// Enum to represent the Message IDs.
        /// </summary>
        public enum MessageID : byte
        {
            /// <summary>
            /// Represents a Command Message
            /// </summary>
            CommandMessage = 0x01,  // in hexadecimal

            /// <summary>
            /// Represents a Response Message
            /// </summary>
            ResponseMessage = 0x02,  // in hexadecimal

            /// <summary>
            /// Represents an Error Message
            /// </summary>
            ErrorMessage = 0x03  // in hexadecimal
        }

        /// <summary>
        /// Enum to represent the Message IDs.
        /// </summary>
        public enum CommandCode : ushort
        {
            /// <summary>
            /// Represents a SetTargetParams Message
            /// </summary>
            SetTargetParams = 0x0008,  // in hexadecimal
        }

        public enum MessageSize : ushort
        {
            /// <summary>
            /// Represents a SetTargetParams Message
            /// </summary>
            Header = 29,
            SetTargetParams = 44,
            SetTargetParamsResponse = 57

        }

        /// <summary>
        /// The Header struct represents the header of a network message.
        /// </summary>
        public struct Header
        {
            /// <summary>
            /// Gets or sets the message ID.
            /// </summary>
            public MessageID MessageID { get; set; }

            /// <summary>
            /// Gets or sets the command code.
            /// </summary>
            public CommandCode CommandCode { get; set; }

            /// <summary>
            /// Gets or sets the packet sequence number.
            /// </summary>
            public ulong PacketSequenceNumber { get; set; }

            /// <summary>
            /// Gets or sets the command timestamp.
            /// </summary>
            public ulong CommandTimestamp { get; set; }

            /// <summary>
            /// Gets or sets the response timestamp.
            /// </summary>
            public ulong ResponseTimestamp { get; set; }

            /// <summary>
            /// Gets or sets the payload length.
            /// </summary>
            public ushort PayloadLength { get; set; }

            public override string ToString()
            {
                return $"MessageID: {MessageID}, " +
                       $"CommandCode: {CommandCode}, " +
                       $"PacketSequenceNumber: {PacketSequenceNumber}, " +
                       $"CommandTimestamp: {CommandTimestamp}, " +
                       $"ResponseTimestamp: {ResponseTimestamp}, " +
                       $"PayloadLength: {PayloadLength}";
            }
        }

        public struct SetTargetParams
        {
            /// <summary>
            /// Gets or sets the target X coordinate.
            /// </summary>
            public short TargetX { get; set; }

            /// <summary>
            /// Gets or sets the target Y coordinate.
            /// </summary>
            public short TargetY { get; set; }

            /// <summary>
            /// Gets or sets the target stiffness x-axis gain.
            /// </summary>
            public float KGainX { get; set; }

            /// <summary>
            /// Gets or sets the target stiffness y-axis gain.
            /// </summary>
            public float KGainY { get; set; }

            /// <summary>
            /// Gets or sets the target damping x-axis gain.
            /// </summary>
            public float BGainX { get; set; }

            /// <summary>
            /// Gets or sets the target damping y-axis gain.
            /// </summary>
            public float BGainY { get; set; }

            /// <summary>
            /// Gets or sets the compensation control gain.
            /// </summary>
            public float ForceGain { get; set; }

            /// <summary>
            /// Gets or sets the additional information (reserved for future usage).
            /// </summary>
            public byte[] AdditionalInfo { get; set; }  // Should be 20 bytes long

            public override string ToString()
            {
                string additionalInfo = BitConverter.ToString(AdditionalInfo).Replace("-", "");
                return $"TargetX: {TargetX}, " +
                       $"TargetY: {TargetY}, " +
                       $"KGainX: {KGainX}, " +
                       $"KGainY: {KGainY}, " +
                       $"BGainX: {BGainX}, " +
                       $"BGainY: {BGainY}, " +
                       $"ForceGain: {ForceGain}, " +
                       $"AdditionalInfo: {additionalInfo}";
            }
        }

        public struct SetTargetParamsResponse
        {
            public Header MessageHeader { get; set; } 

            /// <summary>
            /// Gets or sets the X coordinate in millimeters.
            /// </summary>
            public short X { get; set; }

            /// <summary>
            /// Gets or sets the Y coordinate in millimeters.
            /// </summary>
            public short Y { get; set; }

            /// <summary>
            /// Gets or sets the measured velocity in the X direction in meters per second.
            /// </summary>
            public float Vx { get; set; }

            /// <summary>
            /// Gets or sets the measured velocity in the Y direction in meters per second.
            /// </summary>
            public float Vy { get; set; }

            /// <summary>
            /// Gets or sets the operation state.
            /// Bit mask
            /// 0x01 = Normal state
            /// 0x02 = Emergency button
            /// 0x04 = sampling period alert
            /// 0x08 = real-time velocity limit alert
            /// </summary>
            public byte EB { get; set; }

            /// <summary>
            /// Gets or sets the timestamp for this data point in milliseconds.
            /// </summary>
            public uint MS { get; set; }

            /// <summary>
            /// Gets or sets the additional information (reserved for future usage).
            /// </summary>
            public byte[] AdditionalInfo { get; set; }  // Should be 40 bytes long
            public override string ToString()
            {
                var additionalInfoStr = BitConverter.ToString(AdditionalInfo).Replace("-", " ");
                return $"{MessageHeader.ToString()} - X: {X}, Y: {Y}, Vx: {Vx}, Vy: {Vy}, EB: {EB}, MS: {MS}, AdditionalInfo: {additionalInfoStr}";
            }

            public static SetTargetParamsResponse FromByteArray(byte[] bytes)
            {
                SetTargetParamsResponse response = new SetTargetParamsResponse();
                int currentIndex = 0;

                response.MessageHeader = DecodeHeader(bytes);
                currentIndex += (int)MessageSize.Header;

                response.X = BitConverter.ToInt16(bytes, currentIndex);
                currentIndex += sizeof(short);

                response.Y = BitConverter.ToInt16(bytes, currentIndex);
                currentIndex += sizeof(short);

                response.Vx = BitConverter.ToSingle(bytes, currentIndex);
                currentIndex += sizeof(float);

                response.Vy = BitConverter.ToSingle(bytes, currentIndex);
                currentIndex += sizeof(float);

                response.EB = bytes[currentIndex];
                currentIndex += sizeof(byte);

                response.MS = BitConverter.ToUInt32(bytes, currentIndex);
                currentIndex += sizeof(uint);

                response.AdditionalInfo = new byte[40];
                Array.Copy(bytes, currentIndex, response.AdditionalInfo, 0, 40);

                return response;
            }

            public byte[] ToByteArray()
            {
                byte[] bytes = new byte[(int)MessageSize.Header+(int)MessageSize.SetTargetParamsResponse]; // Total size of all elements
                int currentIndex = 0;

                EncodeHeader(MessageHeader).CopyTo(bytes,currentIndex);
                currentIndex += (int)MessageSize.Header;

                BitConverter.GetBytes(X).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(short);

                BitConverter.GetBytes(Y).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(short);

                BitConverter.GetBytes(Vx).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(float);

                BitConverter.GetBytes(Vy).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(float);

                bytes[currentIndex] = EB;
                currentIndex += sizeof(byte);

                BitConverter.GetBytes(MS).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(uint);

                Array.Copy(AdditionalInfo, 0, bytes, currentIndex, 40);

                return bytes;
            }

        }


        private static byte[] EncodeHeader(Header header)
        {
            var message = new byte[0];

            // Encode header
            message = message.Concat(new[] { (byte)header.MessageID }).ToArray();
            message = message.Concat(BitConverter.GetBytes((ushort)header.CommandCode)).ToArray();
            message = message.Concat(BitConverter.GetBytes(header.PacketSequenceNumber)).ToArray();
            message = message.Concat(BitConverter.GetBytes(header.CommandTimestamp)).ToArray();
            message = message.Concat(BitConverter.GetBytes(header.ResponseTimestamp)).ToArray();
            message = message.Concat(BitConverter.GetBytes(header.PayloadLength)).ToArray();

            return message;
        }

        public static Header DecodeHeader(byte[] message)
        {
            var header = new Header();
            var currentIndex = 0;

            // Decode header
            header.MessageID = (MessageID)message[currentIndex];
            currentIndex += sizeof(byte);
            header.CommandCode = (CommandCode)BitConverter.ToUInt16(message, currentIndex);
            currentIndex += sizeof(ushort);
            header.PacketSequenceNumber = BitConverter.ToUInt64(message, currentIndex);
            currentIndex += sizeof(ulong);
            header.CommandTimestamp = BitConverter.ToUInt64(message, currentIndex);
            currentIndex += sizeof(ulong);
            header.ResponseTimestamp = BitConverter.ToUInt64(message, currentIndex);
            currentIndex += sizeof(ulong);
            header.PayloadLength = BitConverter.ToUInt16(message, currentIndex);
            currentIndex += sizeof(ushort);


            return header;
        }

        public static byte[] EncodeSetTargetParams(short targetX, short targetY, float kX, float kY)
        {
            var message = new byte[0];

            Header header = new Header
            {
                MessageID = MessageID.CommandMessage/* Message ID value */,
                CommandCode = CommandCode.SetTargetParams/* Command Code value */,
                PacketSequenceNumber = _latestPacketSequenceNumber++/* Packet Sequence Number value */,
                CommandTimestamp = (ulong)(DateTimeOffset.UtcNow - epoch).Ticks / 10/* Command Timestamp value */,
                ResponseTimestamp = 0/* Response Timestamp value */,
                PayloadLength = (ushort)MessageSize.SetTargetParams//(ushort)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SetTargetParams))/* Payload Length value */
            };
            SetTargetParams body = new SetTargetParams
            {
                TargetX = targetX/* Target X value */,
                TargetY = targetY/* Target Y value */,
                KGainX = kX/* K Gain X value */,
                KGainY = kY/* K Gain Y value */,
                BGainX = 0/* B Gain X value */,
                BGainY = 0/* B Gain Y value */,
                ForceGain = 1/* Force Gain value */,
                AdditionalInfo = new byte[20]/* Additional Info value (20 bytes) */
            };

            // Encode header
            message = message.Concat(EncodeHeader(header)).ToArray();
            // Encode body
            message = message.Concat(BitConverter.GetBytes(body.TargetX)).ToArray();
            message = message.Concat(BitConverter.GetBytes(body.TargetY)).ToArray();
            message = message.Concat(BitConverter.GetBytes(body.KGainX)).ToArray();
            message = message.Concat(BitConverter.GetBytes(body.KGainY)).ToArray();
            message = message.Concat(BitConverter.GetBytes(body.BGainX)).ToArray();
            message = message.Concat(BitConverter.GetBytes(body.BGainY)).ToArray();
            message = message.Concat(BitConverter.GetBytes(body.ForceGain)).ToArray();
            message = message.Concat(body.AdditionalInfo).ToArray();

            return message;
        }

        public static (Header, SetTargetParams) DecodeSetTargetParams(byte[] message)
        {
            var header = new Header();
            var body = new SetTargetParams();
            var currentIndex = 0;

            // Decode header
            header = DecodeHeader(message);
            currentIndex += (ushort)MessageSize.Header;//System.Runtime.InteropServices.Marshal.SizeOf(typeof(Header));

            // Decode body
            body.TargetX = BitConverter.ToInt16(message, currentIndex);
            currentIndex += sizeof(short);
            body.TargetY = BitConverter.ToInt16(message, currentIndex);
            currentIndex += sizeof(short);
            body.KGainX = BitConverter.ToSingle(message, currentIndex);
            currentIndex += sizeof(float);
            body.KGainY = BitConverter.ToSingle(message, currentIndex);
            currentIndex += sizeof(float);
            body.BGainX = BitConverter.ToSingle(message, currentIndex);
            currentIndex += sizeof(float);
            body.BGainY = BitConverter.ToSingle(message, currentIndex);
            currentIndex += sizeof(float);
            body.ForceGain = BitConverter.ToSingle(message, currentIndex);
            currentIndex += sizeof(float);
            body.AdditionalInfo = message.Skip(currentIndex).Take(20).ToArray();

            return (header, body);
        }
    }
}

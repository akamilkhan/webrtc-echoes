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
        public static DateTimeOffset epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public MicroSecondDateTime()
        {
            this.stopwatch = new Stopwatch();
            this.stopwatch.Start();
            this.startTime = DateTimeOffset.UtcNow;
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

            public byte[] ToByteArray()
            {
                byte[] bytes = new byte[(int)MessageSize.Header]; // Size of all elements in bytes
                int currentIndex = 0;

                bytes[currentIndex] = (byte)MessageID;
                currentIndex += sizeof(byte);

                BitConverter.GetBytes((ushort)CommandCode).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(ushort);

                BitConverter.GetBytes(PacketSequenceNumber).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(ulong);

                BitConverter.GetBytes(CommandTimestamp).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(ulong);

                BitConverter.GetBytes(ResponseTimestamp).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(ulong);

                BitConverter.GetBytes(PayloadLength).CopyTo(bytes, currentIndex);

                return bytes;
            }

            public static Header FromByteArray(byte[] bytes)
            {
                Header result = new Header();

                int currentIndex = 0;

                result.MessageID = (MessageID)bytes[currentIndex];
                currentIndex += sizeof(byte);

                result.CommandCode = (CommandCode)BitConverter.ToUInt16(bytes, currentIndex);
                currentIndex += sizeof(ushort);

                result.PacketSequenceNumber = BitConverter.ToUInt64(bytes, currentIndex);
                currentIndex += sizeof(ulong);

                result.CommandTimestamp = BitConverter.ToUInt64(bytes, currentIndex);
                currentIndex += sizeof(ulong);

                result.ResponseTimestamp = BitConverter.ToUInt64(bytes, currentIndex);
                currentIndex += sizeof(ulong);

                result.PayloadLength = BitConverter.ToUInt16(bytes, currentIndex);

                return result;
            }
        }

        public struct SetTargetParams
        {
            public Header MessageHeader { get; set; }

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
                return $"{MessageHeader.ToString()} - TargetX: {TargetX}, " +
                       $"TargetY: {TargetY}, " +
                       $"KGainX: {KGainX}, " +
                       $"KGainY: {KGainY}, " +
                       $"BGainX: {BGainX}, " +
                       $"BGainY: {BGainY}, " +
                       $"ForceGain: {ForceGain}, " +
                       $"AdditionalInfo: {additionalInfo}";
            }

            public static SetTargetParams FromByteArray(byte[] bytes)
            {
                SetTargetParams result = new SetTargetParams();
                int currentIndex = 0;

                // Decode header
                result.MessageHeader = DecodeHeader(bytes);
                currentIndex += (int)MessageSize.Header;

                result.TargetX = BitConverter.ToInt16(bytes, currentIndex);
                currentIndex += sizeof(short);

                result.TargetY = BitConverter.ToInt16(bytes, currentIndex);
                currentIndex += sizeof(short);

                result.KGainX = BitConverter.ToSingle(bytes, currentIndex);
                currentIndex += sizeof(float);

                result.KGainY = BitConverter.ToSingle(bytes, currentIndex);
                currentIndex += sizeof(float);

                result.BGainX = BitConverter.ToSingle(bytes, currentIndex);
                currentIndex += sizeof(float);

                result.BGainY = BitConverter.ToSingle(bytes, currentIndex);
                currentIndex += sizeof(float);

                result.ForceGain = BitConverter.ToSingle(bytes, currentIndex);
                currentIndex += sizeof(float);

                result.AdditionalInfo = new byte[20];
                Array.Copy(bytes, currentIndex, result.AdditionalInfo, 0, 20);

                return result;
            }

            public byte[] ToByteArray()
            {
                byte[] bytes = new byte[(int)MessageSize.Header + (int)MessageSize.SetTargetParams]; // Total size of all elements

                int currentIndex = 0;

                // Convert header
                EncodeHeader(MessageHeader).CopyTo(bytes, currentIndex);
                currentIndex += (int)MessageSize.Header;

                BitConverter.GetBytes(TargetX).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(short);

                BitConverter.GetBytes(TargetY).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(short);

                BitConverter.GetBytes(KGainX).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(float);

                BitConverter.GetBytes(KGainY).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(float);

                BitConverter.GetBytes(BGainX).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(float);

                BitConverter.GetBytes(BGainY).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(float);

                BitConverter.GetBytes(ForceGain).CopyTo(bytes, currentIndex);
                currentIndex += sizeof(float);

                Array.Copy(AdditionalInfo, 0, bytes, currentIndex, 20);

                return bytes;
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
            return header.ToByteArray();
        }

        public static Header DecodeHeader(byte[] message)
        {
            return Header.FromByteArray(message);
        }

        public static byte[] EncodeSetTargetParams(short targetX, short targetY, float kX, float kY)
        {
            SetTargetParams command = new SetTargetParams
            {
                MessageHeader = new Header
                {
                    MessageID = MessageID.CommandMessage/* Message ID value */,
                    CommandCode = CommandCode.SetTargetParams/* Command Code value */,
                    PacketSequenceNumber = _latestPacketSequenceNumber++/* Packet Sequence Number value */,
                    CommandTimestamp = (ulong)(DateTimeOffset.UtcNow - MicroSecondDateTime.epoch).Ticks / 10/* Command Timestamp value */,
                    ResponseTimestamp = 0/* Response Timestamp value */,
                    PayloadLength = (ushort)MessageSize.SetTargetParams/* Payload Length value */
                },
                TargetX = targetX/* Target X value */,
                TargetY = targetY/* Target Y value */,
                KGainX = kX/* K Gain X value */,
                KGainY = kY/* K Gain Y value */,
                BGainX = 0/* B Gain X value */,
                BGainY = 0/* B Gain Y value */,
                ForceGain = 1/* Force Gain value */,
                AdditionalInfo = new byte[20]/* Additional Info value (20 bytes) */
            };

            return command.ToByteArray();
        }

        public static SetTargetParams DecodeSetTargetParams(byte[] message)
        {   
            return SetTargetParams.FromByteArray(message);
        }
    }
}

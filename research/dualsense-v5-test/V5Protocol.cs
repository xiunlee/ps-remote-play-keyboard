// Isolated research codec. No device creation, keyboard capture, or Sony access.
// Wire contract: hbashton/VIIPER b78e31e93e84b4cb7c4b4c15b8aace10749106d2,
// docs/devices/dualsense.md, device/dualsense/{const,ds_handler,output_writer,state}.go.
// Independently implemented from the documented fields; no upstream code copied.
using System;
using System.Diagnostics;
using System.IO;

namespace PSKeyboard.Research
{
    public static class V5Protocol
    {
        public const int HeaderSize = 16, InputSize = 33, FeedbackSize = 474;
        public const int PcmSize = 1920, AtomicSize = 2 + FeedbackSize + PcmSize;
        public const string DeviceType = "dualsensecombinedaudioduplexv5";
        public const string UpstreamCommit = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2";

        // The original Alia5 v0.9.0 keyboard state has bool touch-active fields:
        // 00 means inactive. The pinned V5 candidate decodes those same byte
        // positions as packed status: bit 7 clear means ACTIVE. Thus wrapping
        // the old 33 bytes unchanged creates two unintended touch contacts.
        // This adapter accepts only the existing keyboard converter's neutral
        // touch/motion contract, not physical touch, motion, or raw metadata.
        public static byte[] ConvertLegacyKeyboardState(byte[] legacyState)
        {
            if (legacyState == null) throw new ArgumentNullException("legacyState");
            if (legacyState.Length != InputSize)
                throw new ArgumentException("Expected exactly 33 legacy keyboard state bytes.", "legacyState");
            if ((ReadUInt32(legacyState, 4) & ~0x0003FFF0u) != 0 || (legacyState[8] & 0xF0) != 0)
                throw new ArgumentException("State contains controls outside the keyboard mapping.", "legacyState");
            for (int i = 11; i < 31; i++)
                if (legacyState[i] != 0)
                    throw new ArgumentException("Only inactive zero-position legacy touch and neutral motion can be converted.", "legacyState");
            if (legacyState[31] != 0 || legacyState[32] != 0xE0)
                throw new ArgumentException("Expected legacy keyboard gravity -8192.", "legacyState");
            byte[] candidateState = (byte[])legacyState.Clone();
            candidateState[15] = 0x80;
            candidateState[20] = 0x80;
            return candidateState;
        }

        // state must already use the candidate's native packed touch status.
        // Use ConvertLegacyKeyboardState for the application's old 33-byte state.
        // Caller owns the single per-direction sequence and socket. A new stream
        // starts at zero. Do not reuse the same sequence after a failed write.
        public static byte[] EncodeInput(byte[] state, uint sequence)
        {
            if (state == null) throw new ArgumentNullException("state");
            if (state.Length != InputSize) throw new ArgumentException("Expected exactly 33 mapped input bytes.", "state");
            if ((ReadUInt32(state, 4) & ~0x00F7FFF0u) != 0 || (state[8] & 0xF0) != 0)
                throw new ArgumentException("Input has unknown button or D-pad bits.", "state");
            var packet = new byte[HeaderSize + InputSize];
            packet[0] = 0x56; packet[1] = 0x50; packet[2] = 0x43; packet[3] = 0x4D;
            packet[4] = 5; packet[5] = 1; packet[6] = InputSize;
            WriteUInt32(packet, 8, sequence);
            Buffer.BlockCopy(state, 0, packet, HeaderSize, state.Length);
            WriteUInt32(packet, 12, FrameCrc(packet, state));
            return packet;
        }

        internal static uint ReadUInt32(byte[] bytes, int offset)
        {
            return bytes[offset] | ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24);
        }
        internal static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value; bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16); bytes[offset + 3] = (byte)(value >> 24);
        }
        internal static uint FrameCrc(byte[] header, byte[] payload)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 4; i < 12; i++) crc = AddCrcByte(crc, header[i]);
            for (int i = 0; i < payload.Length; i++) crc = AddCrcByte(crc, payload[i]);
            return ~crc;
        }
        static uint AddCrcByte(uint crc, byte value)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xEDB88320u);
            return crc;
        }
    }

    public sealed class V5Frame
    {
        public readonly byte Type;
        public readonly uint Sequence;
        public readonly byte[] Payload, Feedback, SpeakerPcm;
        public readonly bool? MicrophoneActive;
        public readonly ulong? MicrophoneGeneration;

        internal V5Frame(byte type, uint sequence, byte[] payload)
        {
            Type = type; Sequence = sequence; Payload = payload;
            if (type == 0x85)
            {
                if (payload[0] > 1) throw new InvalidDataException("Invalid microphone active flag.");
                MicrophoneActive = payload[0] == 1;
                MicrophoneGeneration = V5Protocol.ReadUInt32(payload, 1) |
                    ((ulong)V5Protocol.ReadUInt32(payload, 5) << 32);
                return;
            }
            int feedbackOffset = 0;
            if (type == 0x83)
            {
                if ((payload[0] | (payload[1] << 8)) != V5Protocol.FeedbackSize)
                    throw new InvalidDataException("Invalid atomic feedback length prefix.");
                feedbackOffset = 2;
                SpeakerPcm = new byte[V5Protocol.PcmSize];
                Buffer.BlockCopy(payload, feedbackOffset + V5Protocol.FeedbackSize, SpeakerPcm, 0, SpeakerPcm.Length);
            }
            Feedback = new byte[V5Protocol.FeedbackSize];
            Buffer.BlockCopy(payload, feedbackOffset, Feedback, 0, Feedback.Length);
        }
        byte FeedbackAt(int index)
        {
            if (Feedback == null) throw new InvalidOperationException("This event has no controller feedback.");
            return Feedback[index];
        }
        public byte RumbleSmall { get { return FeedbackAt(0); } }
        public byte RumbleLarge { get { return FeedbackAt(1); } }
        public byte LedRed { get { return FeedbackAt(2); } }
        public byte LedGreen { get { return FeedbackAt(3); } }
        public byte LedBlue { get { return FeedbackAt(4); } }
        public byte PlayerLeds { get { return FeedbackAt(5); } }
    }

    // One reader per connection; not thread-safe. Read consumes exactly one
    // frame, never bytes belonging to the next frame. Server output starts at
    // sequence zero (output_writer.go); all output types share that sequence.
    // On malformed/truncated/timed-out input the stream is closed permanently.
    public sealed class V5FeedbackReader : IDisposable
    {
        readonly Stream stream;
        readonly bool allowMicrophoneEvents;
        uint expectedSequence;
        bool closed;

        public V5FeedbackReader(Stream stream, bool allowMicrophoneEvents)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", "stream");
            // NetworkStream honours ReadTimeout. MemoryStream cannot block.
            // Reject arbitrary blocking streams that provide no timeout contract.
            if (!stream.CanTimeout && !(stream is MemoryStream))
                throw new ArgumentException("Stream must support read timeouts or be an in-memory buffer.", "stream");
            this.stream = stream; this.allowMicrophoneEvents = allowMicrophoneEvents;
        }

        public V5Frame Read(int timeoutMs)
        {
            if (closed) throw new ObjectDisposedException("V5FeedbackReader");
            if (timeoutMs < 1 || timeoutMs > 10000) throw new ArgumentOutOfRangeException("timeoutMs");
            var clock = Stopwatch.StartNew();
            try
            {
                byte[] header = ReadExact(V5Protocol.HeaderSize, timeoutMs, clock);
                if (header[0] != 0x56 || header[1] != 0x50 || header[2] != 0x43 || header[3] != 0x4D)
                    throw new InvalidDataException("Invalid VPCM magic.");
                if (header[4] != 5) throw new InvalidDataException("Expected V5 stream.");
                byte type = header[5];
                int length = header[6] | (header[7] << 8), required;
                switch (type)
                {
                    case 0x81: required = V5Protocol.FeedbackSize; break;
                    case 0x83: required = V5Protocol.AtomicSize; break;
                    // Source contract includes this lane although the pinned
                    // docs/devices/dualsense.md packet table omits it.
                    case 0x84: required = V5Protocol.FeedbackSize; break;
                    case 0x85:
                        if (!allowMicrophoneEvents) throw new InvalidDataException("Microphone events were not negotiated.");
                        required = 9; break;
                    default: throw new InvalidDataException("Unknown server V5 frame type.");
                }
                if (length != required) throw new InvalidDataException("Invalid V5 payload length.");
                uint sequence = V5Protocol.ReadUInt32(header, 8);
                if (sequence != expectedSequence) throw new InvalidDataException("V5 sequence gap, replay, or invalid start.");
                byte[] payload = ReadExact(length, timeoutMs, clock);
                if (V5Protocol.ReadUInt32(header, 12) != V5Protocol.FrameCrc(header, payload))
                    throw new InvalidDataException("V5 CRC mismatch.");
                V5Frame frame = new V5Frame(type, sequence, payload);
                expectedSequence = unchecked(expectedSequence + 1);
                return frame;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        byte[] ReadExact(int length, int timeoutMs, Stopwatch clock)
        {
            byte[] result = new byte[length]; int offset = 0;
            while (offset < length)
            {
                int remaining = timeoutMs - (int)clock.ElapsedMilliseconds;
                if (remaining <= 0) throw new TimeoutException("V5 frame deadline exceeded.");
                if (stream.CanTimeout) stream.ReadTimeout = remaining;
                int count = stream.Read(result, offset, length - offset);
                // Even a final read must not turn a slow trickle into a pass.
                if (clock.ElapsedMilliseconds >= timeoutMs) throw new TimeoutException("V5 frame deadline exceeded.");
                if (count == 0) throw new EndOfStreamException("Truncated V5 frame.");
                offset += count;
            }
            return result;
        }

        public void Dispose()
        {
            if (closed) return;
            closed = true;
            stream.Dispose();
        }
    }
}

using NUnit.Framework;
using UnityOSC;

namespace Theoriz.UnityOSC.Tests.Editor
{
    /// <summary>
    /// EditMode unit tests for OSC serialization (OSCMessage / OSCPacket).
    /// Round-trip goes through the public API (PackValue/UnpackValue are protected).
    ///
    /// NOTE: <see cref="Unpack_TruncatedMessage_IsHandledGracefully"/> is an intentional
    /// RED test — it asserts the *desired* behavior for the known unbounded-scan bug in
    /// OSCPacket.UnpackValue&lt;string&gt; (see docs/PackageAudit-2026-07-16.md, UnityOSC P0/P1).
    /// It will fail until the parser bounds-checks its offsets.
    /// </summary>
    public class OSCPacketTests
    {
        [Test]
        public void Message_RoundTrips_Int_Float_String()
        {
            var msg = new OSCMessage("/test/addr");
            msg.Append(42);
            msg.Append(3.5f);
            msg.Append("hello");

            byte[] binary = msg.BinaryData;
            var unpacked = OSCPacket.Unpack(binary);

            Assert.AreEqual("/test/addr", unpacked.Address);
            Assert.AreEqual(3, unpacked.Data.Count);
            Assert.AreEqual(42, unpacked.Data[0]);
            Assert.AreEqual(3.5f, (float)unpacked.Data[1], 1e-6f);
            Assert.AreEqual("hello", unpacked.Data[2]);
        }

        [Test]
        public void Message_RoundTrips_EmptyArgs()
        {
            var msg = new OSCMessage("/no/args");

            var unpacked = OSCPacket.Unpack(msg.BinaryData);

            Assert.AreEqual("/no/args", unpacked.Address);
            Assert.AreEqual(0, unpacked.Data.Count);
        }

        [Test]
        public void Message_RoundTrips_NegativeAndLargeNumbers()
        {
            var msg = new OSCMessage("/nums");
            msg.Append(-2147483648);   // int.MinValue
            msg.Append(-0.0001f);

            var unpacked = OSCPacket.Unpack(msg.BinaryData);

            Assert.AreEqual(-2147483648, unpacked.Data[0]);
            Assert.AreEqual(-0.0001f, (float)unpacked.Data[1], 1e-9f);
        }

        // RED (known bug): a truncated/unterminated packet must not throw and take
        // down the receive thread. Currently OSCPacket.Unpack reads past the buffer.
        [Test]
        public void Unpack_TruncatedMessage_IsHandledGracefully()
        {
            // An address with no null terminator, first byte is not '#', so it is
            // parsed as a message and the string scan runs off the end of the buffer.
            byte[] malformed = { (byte)'/', (byte)'a', (byte)'b', (byte)'c' };

            Assert.DoesNotThrow(
                () => OSCPacket.Unpack(malformed),
                "Parsing a truncated OSC packet should be handled gracefully rather than " +
                "throwing IndexOutOfRangeException (which silently kills the receive thread).");
        }
    }
}

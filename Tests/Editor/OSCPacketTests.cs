using NUnit.Framework;
using UnityOSC;

namespace Theoriz.UnityOSC.Tests.Editor
{
    /// <summary>
    /// EditMode unit tests for OSC serialization (OSCMessage / OSCPacket).
    /// Round-trip goes through the public API (PackValue/UnpackValue are protected).
    ///
    /// NOTE: <see cref="Unpack_TruncatedMessage_IsHandledGracefully"/> was written RED against
    /// the unbounded-scan bug in OSCPacket.UnpackValue&lt;string&gt; (see
    /// docs/PackageAudit-2026-07-16.md, UnityOSC P0/P1). The parser now bounds-checks its
    /// offsets, so it passes and stands as a regression guard.
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

        [Test]
        public void Message_RoundTrips_Long()
        {
            var msg = new OSCMessage("/long");
            msg.Append(long.MinValue);
            msg.Append(1234567890123L);

            var unpacked = OSCPacket.Unpack(msg.BinaryData);

            Assert.AreEqual(2, unpacked.Data.Count);
            Assert.AreEqual(long.MinValue, unpacked.Data[0]);
            Assert.AreEqual(1234567890123L, unpacked.Data[1]);
        }

        [Test]
        public void Message_RoundTrips_Double()
        {
            var msg = new OSCMessage("/double");
            msg.Append(-0.000123456789d);
            msg.Append(double.MaxValue);

            var unpacked = OSCPacket.Unpack(msg.BinaryData);

            Assert.AreEqual(2, unpacked.Data.Count);
            Assert.AreEqual(-0.000123456789d, (double)unpacked.Data[0], 1e-18d);
            Assert.AreEqual(double.MaxValue, (double)unpacked.Data[1]);
        }

        [Test]
        public void Message_RoundTrips_ByteArray()
        {
            // 5 bytes so the blob is not already 4-byte aligned and must be padded.
            var blob = new byte[] { 1, 2, 250, 0, 255 };
            var msg = new OSCMessage("/blob");
            msg.Append(blob);
            msg.Append(7);   // proves the reader resumed at the right offset after padding

            var unpacked = OSCPacket.Unpack(msg.BinaryData);

            Assert.AreEqual(2, unpacked.Data.Count);
            CollectionAssert.AreEqual(blob, (byte[])unpacked.Data[0]);
            Assert.AreEqual(7, unpacked.Data[1]);
        }

        // All six tag types in one message: guards the type-tag string against
        // losing or reordering a tag.
        [Test]
        public void Message_RoundTrips_MixedTypes()
        {
            var blob = new byte[] { 9, 8, 7 };
            var msg = new OSCMessage("/mixed");
            msg.Append(1);
            msg.Append(2.5f);
            msg.Append(3L);
            msg.Append(4.5d);
            msg.Append("five");
            msg.Append(blob);

            var unpacked = OSCPacket.Unpack(msg.BinaryData);

            Assert.AreEqual("/mixed", unpacked.Address);
            Assert.AreEqual(6, unpacked.Data.Count);
            Assert.AreEqual(1, unpacked.Data[0]);
            Assert.AreEqual(2.5f, (float)unpacked.Data[1], 1e-6f);
            Assert.AreEqual(3L, unpacked.Data[2]);
            Assert.AreEqual(4.5d, (double)unpacked.Data[3], 1e-12d);
            Assert.AreEqual("five", unpacked.Data[4]);
            CollectionAssert.AreEqual(blob, (byte[])unpacked.Data[5]);
        }

        // BinaryData caches its packed buffer; appending after a read must invalidate it.
        [Test]
        public void BinaryData_ReflectsAppendsAfterFirstGet()
        {
            var msg = new OSCMessage("/cache");
            msg.Append(1);

            byte[] first = msg.BinaryData;
            int firstLength = first.Length;

            msg.Append(2);
            byte[] second = msg.BinaryData;

            Assert.AreNotEqual(firstLength, second.Length,
                "Appending after reading BinaryData must repack, not return the stale buffer.");

            var unpacked = OSCPacket.Unpack(second);
            Assert.AreEqual(2, unpacked.Data.Count);
            Assert.AreEqual(1, unpacked.Data[0]);
            Assert.AreEqual(2, unpacked.Data[1]);
        }

        // Changing the address after a read must also invalidate the cached buffer.
        [Test]
        public void BinaryData_ReflectsAddressChangeAfterFirstGet()
        {
            var msg = new OSCMessage("/before");
            var unused = msg.BinaryData;

            msg.Address = "/after/longer";

            Assert.AreEqual("/after/longer", OSCPacket.Unpack(msg.BinaryData).Address);
        }

        // A truncated/unterminated packet must not throw and take down the receive thread.
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

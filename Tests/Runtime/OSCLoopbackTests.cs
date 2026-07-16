using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityOSC;

namespace Theoriz.UnityOSC.Tests
{
    /// <summary>
    /// PlayMode integration test: send an OSC message to loopback and confirm it is
    /// received and marshalled to the main thread via OSCMaster's pump.
    /// Exercises the real socket + background receive thread + OSCReceiver queue path.
    /// </summary>
    public class OSCLoopbackTests
    {
        const int TestPort = 18923;
        const string ReceiverId = "unityosc-test-receiver";

        [UnityTest]
        public IEnumerator Message_SentToLoopback_IsReceivedOnMainThread()
        {
            // Accessing Instance auto-creates the OSCMaster whose Update() pumps the queue.
            var _ = OSCMaster.Instance;

            var receiver = OSCMaster.CreateReceiver(ReceiverId, TestPort);
            Assert.IsNotNull(receiver,
                $"Could not open a UDP receiver on port {TestPort} — the port may be in use.");

            OSCMessage received = null;
            receiver.messageReceived += m => received = m;

            var msg = new OSCMessage("/test/value");
            msg.Append(123);
            OSCMaster.SendMessage(msg, "127.0.0.1", TestPort);

            // Wait for background receive + main-thread pump, with a timeout.
            float timeout = 3f;
            while (received == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(received, "No OSC message was received within the timeout.");
            Assert.AreEqual("/test/value", received.Address);
            Assert.AreEqual(1, received.Data.Count);
            Assert.AreEqual(123, received.Data[0]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in OSCMaster.Receivers.Values) r.Close();
            OSCMaster.Receivers.Clear();
            foreach (var c in OSCMaster.Clients.Values) c.Close();
            OSCMaster.Clients.Clear();

            var master = Object.FindFirstObjectByType<OSCMaster>();
            if (master != null) Object.DestroyImmediate(master.gameObject);
            OSCMaster.Instance = null;
        }
    }
}

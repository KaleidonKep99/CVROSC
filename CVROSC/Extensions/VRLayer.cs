using Bespoke.Osc;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Timers;
using MelonLoader;
using System;
using System.Collections.Generic;

namespace CVROSC
{
    public class VRLayer
    {
        // The endpoint is the target passed to the OSC message/bundle
        // The VRServer endpoint is the OSC server hosted by ChilloutVR
        // The VRClient endpoint is the OSC server hosted by this application

        // VR app target
        public IPEndPoint VRServer;

        // OSC app server
        public IPEndPoint VRClient;
        private OscServer VRMaster;

        private int InPort = 9000, OutPort = 9001;

        public void Init(EventHandler<OscMessageReceivedEventArgs> Message, EventHandler<OscBundleReceivedEventArgs> Bundle)
        {
            // Dummy UDP client for OSC packets
            OscPacket.UdpClient = new UdpClient();

            VRClient = new IPEndPoint(IPAddress.Loopback, OutPort);
            VRServer = new IPEndPoint(IPAddress.Loopback, InPort);
            VRMaster = new OscServer(Bespoke.Common.Net.TransportType.Udp, IPAddress.Loopback, InPort);

            // We need to receive all the methods
            VRMaster.FilterRegisteredMethods = false;

            // Attach the event handlers to the OSC server, then start it
            VRMaster.MessageReceived += Message;
            VRMaster.BundleReceived += Bundle;
            VRMaster.Start();
        }

        public void SetPorts(int? I, int? O)
        {
            if (I != null) InPort = (int)I;
            if (O != null) OutPort = (int)O;
        }

        // Since messages can have multiple variables in one, we had to do this fuckery...
        // And hey, it works, so whatever!
        private OscMessage BuildMsg(string Target, IPEndPoint NetTarget, params object[] Parameters)
        {
            OscMessage Message = new OscMessage(NetTarget, Target);

            try
            {
                foreach (object Param in Parameters)
                {
                    switch (Param)
                    {
                        case int[] iArray:
                            foreach (int i in iArray)
                                Message.Append(i);
                            break;

                        case bool[] boArray:
                            foreach (bool bo in boArray)
                                Message.Append(bo);
                            break;

                        case float[] fArray:
                            foreach (float f in fArray)
                                Message.Append(f);
                            break;

                        case byte[] byArray:
                            foreach (byte by in byArray)
                                Message.Append(by);
                            break;

                        case string[] sArray:
                            foreach (string s in sArray)
                                Message.Append(s);
                            break;

                        case int i:
                        case bool bo:
                        case float f:
                        case byte by:
                        case string s:
                            Message.Append(Param);
                            break;

                        default:
                            MelonLogger.Msg("Unsupported parameter passed to BuildMsg.", Param.GetType());
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("An error has occured.", ex.ToString());
            }

            return Message;
        }

        // Send a message to the target OSC client
        public void SendMsg(string Target, IPEndPoint NetTarget, object Value = null, bool? Silent = false)
        {
            OscMessage Message = BuildMsg(Target, NetTarget, Value);
            Message.Send(NetTarget);

            if (Silent == false) MelonLogger.Msg("OSC message sent.", Message.Address, NetTarget.Address.ToString(), NetTarget.Port.ToString());
        }

        // Send a bundle to the target OSC client
        public void SendBndl(IPEndPoint NetTarget, List<OscMessage> MsgVector = null, bool? Silent = false)
        {
            OscBundle Bundle = new OscBundle(NetTarget);

            foreach (OscMessage Msg in MsgVector)
                Bundle.Append(Msg);

            Bundle.Send(NetTarget);
            if (Silent == false) MelonLogger.Msg("OSC bundle sent.", NetTarget.Address.ToString(), NetTarget.Port.ToString());
        }
    }
}

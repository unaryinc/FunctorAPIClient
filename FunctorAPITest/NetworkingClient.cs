using FunctorAPI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FunctorAPITest
{
    public class NetworkingClient
    {
        private bool GotConnection = false;
        public HSteamNetConnection Connection;

        private int MessageCount;
        private IntPtr[] PointerBuffer;
        private byte[] Buffer;
        private int BufferSize;
        private int FillSize;
        private long MessageSentCounter;

        private RecusantProcessor Processor;

        public SteamNetworkingIdentity Owner;
        public int Port;

        private EResult SendResult;

        private Callback<SteamNetConnectionStatusChangedCallback_t> StatusChangedCallback;

        private bool GotCallbacks = false;

        private bool GotFunctorClient = false;

        byte[] TempTicket;
        byte[] RealTicket;
        uint RealTicketSize;

        HAuthTicket AuthTicket;

        private Action<string, object> EventDispatcher;

        public NetworkingClient(Action<string, object> EventDispatch, RecusantProcessor NewProcessor)
        {
            Owner = new SteamNetworkingIdentity();
            Owner.Clear();

            PointerBuffer = new IntPtr[32];
            Buffer = new byte[8192];
            BufferSize = 8192;

            TempTicket = new byte[2048];

            EventDispatcher = EventDispatch;
            Processor = NewProcessor;
        }

        public void Init()
        {
            AuthTicket = SteamUser.GetAuthSessionTicket(TempTicket, 2048, out RealTicketSize);
            RealTicket = new byte[RealTicketSize];
            Array.Copy(TempTicket, 0, RealTicket, 0, RealTicketSize);
            Processor.CreateClient(Owner.GetSteamID64(), RealTicket);
            StatusChangedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChange);
            GotCallbacks = true;
        }

        public void OnCreateClient(object Data)
        {
            bool Result = (bool)Data;
            if (Result)
            {
                GotFunctorClient = true;
                Connect();
            }
            else
            {
                SteamUser.CancelAuthTicket(AuthTicket);
                GotFunctorClient = false;
            }
        }

        public void Connect()
        {
            Connection = SteamNetworkingSockets.ConnectP2P(ref Owner, Port, 0, null);
            GotConnection = true;
        }

        ~NetworkingClient()
        {
            Clear();
        }

        public void Clear()
        {
            if (GotCallbacks)
            {
                StatusChangedCallback.Dispose();
            }
            if (GotConnection)
            {
                SteamNetworkingSockets.CloseConnection(Connection, 0, "", false);
                GotConnection = false;
            }
            if (GotFunctorClient)
            {
                Processor.CloseClient();
                SteamUser.CancelAuthTicket(AuthTicket);
                GotFunctorClient = false;
            }
        }

        public void Update()
        {
            if (GotConnection)
            {
                while (true)
                {
                    MessageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(Connection, PointerBuffer, 32);
                    if (MessageCount <= 0) { break; }
                    for (int i = 0; i < MessageCount; ++i)
                    {
                        SteamNetworkingMessage_t NewMessage = (SteamNetworkingMessage_t)Marshal.PtrToStructure(PointerBuffer[i], typeof(SteamNetworkingMessage_t));
                        if (BufferSize < NewMessage.m_cbSize) { Buffer = new byte[NewMessage.m_cbSize]; BufferSize = NewMessage.m_cbSize; }
                        Marshal.Copy(NewMessage.m_pData, Buffer, 0, NewMessage.m_cbSize);
                        FillSize = NewMessage.m_cbSize;

                        EventDispatcher.Invoke("OnChat", new ChatMessage()
                        {
                            SteamID = NewMessage.m_identityPeer.GetSteamID64(),
                            Text = Encoding.UTF8.GetString(Buffer, 0, FillSize)
                        });

                        NativeMethods.SteamAPI_SteamNetworkingMessage_t_Release(PointerBuffer[i]);
                    }
                }
            }
        }

        public void SendMessage(string Message)
        {
            byte[] Bytes = Encoding.UTF8.GetBytes(Message);
            IntPtr Ptr = Marshal.AllocHGlobal(Bytes.Length);
            Marshal.Copy(Bytes, 0, Ptr, Bytes.Length);
            SendResult = SteamNetworkingSockets.SendMessageToConnection(Connection, Ptr, (uint)Bytes.Length, (int)EP2PSend.k_EP2PSendReliable, out MessageSentCounter);
            Marshal.FreeHGlobal(Ptr);
        }

        public void OnConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t Args)
        {
            OnConnectionChanged(Args.m_info);
            switch (Args.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    OnConnecting(Args.m_info);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    OnConnected(Args.m_info);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    OnDisconnected(Args.m_info);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    OnDisconnected(Args.m_info);

                    break;
            }
        }

        public void OnConnected(SteamNetConnectionInfo_t Info)
        {
            Console.WriteLine("Connected.");
        }

        public void OnConnecting(SteamNetConnectionInfo_t Info)
        {
            Console.WriteLine("Connecting...");
        }

        public void OnConnectionChanged(SteamNetConnectionInfo_t Info)
        {
            Console.WriteLine("Changed connection status to " + Info.m_eState);
        }

        public void OnDisconnected(SteamNetConnectionInfo_t Info)
        {
            SteamNetworkingSockets.CloseConnection(Connection, 0, "", false);
            GotConnection = false;
            Console.WriteLine("Disconnected with end reason " + (ESteamNetConnectionEnd)Info.m_eEndReason);
        }

        public void Queue()
        {
            Processor.QueueGlobalMap(0);
        }
    }
}

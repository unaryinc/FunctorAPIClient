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
    public class NetworkingServer
    {
        
        private bool GotSocket = false;
        public HSteamListenSocket Socket;
        private int MessageCount;
        private IntPtr[] PointerBuffer;
        private byte[] Buffer;
        private int BufferSize;
        private int FillSize;
        private long MessageSentCounter;

        private EResult SendResult;
        private EResult AcceptResult;

        private bool GotPollGroup = false;
        private HSteamNetPollGroup PollGroup;

        private Dictionary<ulong, HSteamNetConnection> UnauthorizedConnection;
        private Dictionary<ulong, HSteamNetConnection> Connections;
        private Dictionary<ulong, string> Usernames;

        private Callback<SteamNetConnectionStatusChangedCallback_t> StatusChangedCallback;

        private bool GotCallbacks = false;

        int Port;

        bool GotFunctorServer = false;

        byte[] TempTicket;
        byte[] RealTicket;
        uint RealTicketSize;

        HAuthTicket AuthTicket;

        private Action<string, object> EventDispatcher;

        private RecusantProcessor Processor;

        public NetworkingServer(Action<string, object> EventDispatch, RecusantProcessor NewProcessor)
        {
            PointerBuffer = new IntPtr[32];
            Buffer = new byte[8192];
            BufferSize = 8192;

            EventDispatcher = EventDispatch;
            Processor = NewProcessor;

            UnauthorizedConnection = new Dictionary<ulong, HSteamNetConnection>();
            Connections = new Dictionary<ulong, HSteamNetConnection>();
            Usernames = new Dictionary<ulong, string>();

            TempTicket = new byte[2048];
        }

        public void Init()
        {
            StatusChangedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChange);
            GotCallbacks = true;
            Port = Random.Next(0, 800);
            Socket = SteamNetworkingSockets.CreateListenSocketP2P(Port, 0, null);
            GotSocket = true;
            PollGroup = SteamNetworkingSockets.CreatePollGroup();
            GotPollGroup = true;

            AuthTicket = SteamUser.GetAuthSessionTicket(TempTicket, 2048, out RealTicketSize);
            RealTicket = new byte[RealTicketSize];
            Array.Copy(TempTicket, 0, RealTicket, 0, RealTicketSize);
            Processor.CreateServer(0, (uint)Random.Next(0, 65000), (ushort)Port, RealTicket);

            Console.WriteLine("Hosting server on port " + Port);
        }

        public void OnCreateServer(object Data)
        {
            bool Result = (bool)Data;
            if (Result)
            {
                GotFunctorServer = true;
            }
            else
            {
                SteamUser.CancelAuthTicket(AuthTicket);
                GotFunctorServer = false;
            }
        }

        public void OnCheckClient(object Data)
        {
            RecusantProcessor.CheckedClient Client = (RecusantProcessor.CheckedClient)Data;
            if (Client.Valid)
            {
                HSteamNetConnection TargetConnection = UnauthorizedConnection[Client.SteamID];
                UnauthorizedConnection.Remove(Client.SteamID);
                Usernames[Client.SteamID] = Client.Username;
                Connections[Client.SteamID] = TargetConnection;
                SendMessage(Client.SteamID, "Verified");
            }
            else
            {
                SteamNetworkingSockets.CloseConnection(UnauthorizedConnection[Client.SteamID], 0, "", false);
                UnauthorizedConnection.Remove(Client.SteamID);
            }
        }

        ~NetworkingServer()
        {
            Clear();
        }

        public void Clear()
        {
            if (GotCallbacks)
            {
                StatusChangedCallback.Dispose();
            }
            foreach (var Connection in Connections)
            {
                Processor.CloseClient(Connection.Key);
                SteamNetworkingSockets.CloseConnection(Connection.Value, 0, "", false);
            }
            Connections.Clear();

            if (GotSocket)
            {
                SteamNetworkingSockets.CloseListenSocket(Socket);
                GotSocket = false;
            }
            if (GotPollGroup)
            {
                SteamNetworkingSockets.DestroyPollGroup(PollGroup);
                GotPollGroup = false;
            }
            if (GotFunctorServer)
            {
                Processor.CloseServer();
                SteamUser.CancelAuthTicket(AuthTicket);
                GotFunctorServer = false;
            }
        }

        public void Update()
        {
            if (GotSocket)
            {
                while (true)
                {
                    MessageCount = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(PollGroup, PointerBuffer, 32);
                    if (MessageCount <= 0) { break; }
                    for (int i = 0; i < MessageCount; ++i)
                    {
                        SteamNetworkingMessage_t NewMessage = (SteamNetworkingMessage_t)Marshal.PtrToStructure(PointerBuffer[i], typeof(SteamNetworkingMessage_t));
                        if (!Connections.ContainsKey(NewMessage.m_identityPeer.GetSteamID64())) { continue; }
                        if (BufferSize < NewMessage.m_cbSize) { Buffer = new byte[NewMessage.m_cbSize]; BufferSize = NewMessage.m_cbSize; }
                        Marshal.Copy(NewMessage.m_pData, Buffer, 0, NewMessage.m_cbSize);
                        FillSize = NewMessage.m_cbSize;

                        if (UnauthorizedConnection.ContainsKey(NewMessage.m_identityPeer.GetSteamID64()))
                        {
                            SteamNetworkingSockets.CloseConnection(Connections[NewMessage.m_identityPeer.GetSteamID64()], 0, "", false);
                        }
                        else
                        {
                            EventDispatcher.Invoke("OnChat", new ChatMessage()
                            {
                                SteamID = NewMessage.m_identityPeer.GetSteamID64(),
                                Text = Encoding.UTF8.GetString(Buffer, 0, FillSize)
                            });
                        }

                        NativeMethods.SteamAPI_SteamNetworkingMessage_t_Release(PointerBuffer[i]);
                    }
                }
            }
        }

        public void SendMessage(ulong SteamID, string Message)
        {
            byte[] Bytes = Encoding.UTF8.GetBytes(Message);
            IntPtr Ptr = Marshal.AllocHGlobal(Bytes.Length);
            Marshal.Copy(Bytes, 0, Ptr, Bytes.Length);
            SendResult = SteamNetworkingSockets.SendMessageToConnection(Connections[SteamID], Ptr, (uint)Bytes.Length, (int)EP2PSend.k_EP2PSendReliable, out MessageSentCounter);
            Marshal.FreeHGlobal(Ptr);
        }

        public void OnConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t Args)
        {
            OnConnectionChanged(Args.m_info);
            switch (Args.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    OnConnecting(Args.m_hConn, Args.m_info);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    OnConnected(Args.m_hConn, Args.m_info);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    OnDisconnected(Args.m_info);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    OnDisconnected(Args.m_info);
                    break;
            }
        }

        public void OnConnected(HSteamNetConnection Connection, SteamNetConnectionInfo_t Info)
        {
            SteamNetworkingSockets.SetConnectionPollGroup(Connection, PollGroup);
            UnauthorizedConnection[Info.m_identityRemote.GetSteamID64()] = Connection;
            Processor.CheckClient(Info.m_identityRemote.GetSteamID64());
        }

        public void OnConnecting(HSteamNetConnection Connection, SteamNetConnectionInfo_t Info)
        {
            // Check user inb4 accepting in real game
            AcceptResult = SteamNetworkingSockets.AcceptConnection(Connection);
        }

        public void OnConnectionChanged(SteamNetConnectionInfo_t Info)
        {
            ulong SteamID = Info.m_identityRemote.GetSteamID64();
            if (Connections.ContainsKey(SteamID))
            {
                Console.WriteLine(Usernames[SteamID] + " changed connection to " + Info.m_eState);
            }
            else
            {
                Console.WriteLine(Info.m_identityRemote.GetSteamID64() + " changed connection to " + Info.m_eState);
            }
        }

        public void OnDisconnected(SteamNetConnectionInfo_t Info)
        {
            ulong SteamID = Info.m_identityRemote.GetSteamID64();
            if (Connections.ContainsKey(SteamID))
            {
                Console.WriteLine(Usernames[SteamID] + " disconnected");
                Connections.Remove(SteamID);
                Usernames.Remove(SteamID);
                Processor.CloseClient(SteamID);
            }
            else if(UnauthorizedConnection.ContainsKey(SteamID))
            {
                Console.WriteLine(Info.m_identityRemote.GetSteamID64() + " disconnected");
                UnauthorizedConnection.Remove(SteamID);
            }
            
            Clear();
            Program.Running = false;
        }
    }
}

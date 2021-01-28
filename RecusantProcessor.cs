using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FunctorAPI
{
    public class RecusantProcessor : Processor
    {
        // 48 bytes
        public struct RecusantServer
        {
            // 8 bytes
            public ulong Owner;
            // 2 bytes
            public ushort Port;
            // 4 bytes
            public uint TileIndex;
            // 1 byte
            public byte PlayerCount;
            // 1 byte
            public byte UsernameLength;
            // 32 bytes
            public string Username;
        }

        public struct CheckedClient
        {
            public bool Valid;
            public ulong SteamID;
            public string Username;
        }

        public static int RecusantServerSize = 48;

        private string ServerToken;
        private string ClientToken;

        float UpdateThreshold = 120;
        float UpdateTimer = 0;

        #region Server

        public async void CreateServer(byte Faction, uint TargetTile, ushort Port, byte[] Ticket)
        {
            var Parameters = new Dictionary<string, string>
            {
                { "Faction", Faction.ToString() },
                { "Tile", TargetTile.ToString() },
                { "Ticket", BitConverter.ToString(Ticket).Replace("-", string.Empty) },
                { "Port", Port.ToString() }
            };

            bool Result = false;

            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/CreateServer.php", new FormUrlEncodedContent(Parameters));
                var Body = await Response.Content.ReadAsStringAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.CreateServer", Result);
                    return;
                }

                if (!Body.Contains(" "))
                {
                    ServerToken = Body;
                    Result = true;
                }
            }
            catch(Exception)
            {
            }

            SendEvent.Invoke("Functor.CreateServer", Result);
        }

        public async void CheckClient(ulong SteamID)
        {
            var Parameters = new Dictionary<string, string>
            {
                { "SteamID", SteamID.ToString() },
                { "Token", ServerToken }
            };

            CheckedClient Result = new CheckedClient() { SteamID = SteamID, Valid = false };

            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/CheckClient.php", new FormUrlEncodedContent(Parameters));
                var Body = await Response.Content.ReadAsStringAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.CheckClient", Result);
                    return;
                }

                if (Body.StartsWith("Success "))
                {
                    Result.Username = Body.Substring(8);
                    Result.Valid = true;
                }
            }
            catch (Exception)
            {
            }

            SendEvent.Invoke("Functor.CheckClient", Result);
        }

        public async void CloseServer()
        {
            if(ServerToken == null) { return; }

            var Parameters = new Dictionary<string, string>
            {
                { "Token", ServerToken }
            };

            bool Result = false;

            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/CloseServer.php", new FormUrlEncodedContent(Parameters));
                var Body = await Response.Content.ReadAsStringAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.CloseServer", Result);
                    return;
                }

                if (Body == "Success")
                {
                    Result = true;
                    ServerToken = null;
                }
            }
            catch(Exception)
            {
            }

            SendEvent.Invoke("Functor.CloseServer", Result);
        }

        private async void UpdateServer()
        {
            var Parameters = new Dictionary<string, string>
            {
                { "Token", ServerToken }
            };

            bool Result = false;
            
            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/UpdateServer.php", new FormUrlEncodedContent(Parameters));
                var Body = await Response.Content.ReadAsStringAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.UpdateServer", Result);
                    return;
                }
                
                if (Body == "Success")
                {
                    Result = true;
                }
            }
            catch(Exception)
            {
            }

            SendEvent.Invoke("Functor.UpdateServer", Result);
        }

        #endregion

        #region Client

        private async void QueueMap(byte Faction)
        {
            try
            {
                var Response = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Map.bin");
                var Body = await Response.Content.ReadAsByteArrayAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueMap", null);
                    return;
                }

                int MapCount = Body.Length / 4;

                uint[] MapIndexes = new uint[MapCount];

                for (int i = 0; i < MapCount; i++)
                {
                    MapIndexes[i] = BitConverter.ToUInt32(Body, i * 4);
                }

                SendEvent.Invoke("Functor.QueueMap", MapIndexes);
            }
            catch(Exception)
            {
                SendEvent.Invoke("Functor.QueueMap", null);
            }
        }

        private async void QueueProgress(byte Faction)
        {
            try
            {
                var Response = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Progress.bin");
                var Body = await Response.Content.ReadAsByteArrayAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueProgress", null);
                    return;
                }

                bool[] MapIndexes = new bool[Body.Length];

                for (int i = 0; i < Body.Length; i++)
                {
                    MapIndexes[i] = BitConverter.ToBoolean(Body, i);
                }

                SendEvent.Invoke("Functor.QueueProgress", MapIndexes);
            }
            catch(Exception)
            {
                SendEvent.Invoke("Functor.QueueProgress", null);
            }
        }

        private async void QueueServers(byte Faction)
        {
            try
            {
                var Response = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Servers.bin");
                var Body = await Response.Content.ReadAsByteArrayAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueServers", null);
                    return;
                }

                int ServerCount = (Body.Length - 1) / RecusantServerSize;

                RecusantServer[] Servers = new RecusantServer[ServerCount];

                int Offset = 1;

                for (int i = 0; i < ServerCount; i++)
                {
                    bool Null = Body[Offset + 8 + 2 + 4 + 1 + 1] == 0;

                    if(!Null)
                    {
                        Servers[i] = new RecusantServer()
                        {
                            Owner = BitConverter.ToUInt64(Body, Offset),
                            Port = BitConverter.ToUInt16(Body, Offset + 8),
                            TileIndex = BitConverter.ToUInt32(Body, Offset + 8 + 2),
                            PlayerCount = Body[Offset + 8 + 2 + 4],
                            UsernameLength = Body[Offset + 8 + 2 + 4 + 1],
                        };

                        Servers[i].Username = Encoding.UTF8.GetString(Body, Offset + 8 + 2 + 4 + 1 + 1, Servers[i].UsernameLength);
                    }

                    Offset += RecusantServerSize;
                }

                SendEvent.Invoke("Functor.QueueServers", Servers);
            }
            catch(Exception)
            {
                SendEvent.Invoke("Functor.QueueServers", null);
            }
        }

        public void QueueGlobalMap(byte Faction)
        {
            QueueMap(Faction);
            QueueProgress(Faction);
            QueueServers(Faction);
        }

        public async void CreateClient(ulong TargetSteamID, byte[] Ticket)
        {
            var Parameters = new Dictionary<string, string>
            {
                { "SteamID", TargetSteamID.ToString() },
                { "Ticket", BitConverter.ToString(Ticket).Replace("-", string.Empty) }
            };

            bool Result = false;

            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/CreateClient.php", new FormUrlEncodedContent(Parameters));
                var Body = await Response.Content.ReadAsStringAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.CreateClient", Result);
                    return;
                }

                if (!Body.Contains(" "))
                {
                    ClientToken = Body;
                    Result = true;
                }
            }
            catch (Exception)
            {
            }
            
            SendEvent.Invoke("Functor.CreateClient", Result);
        }

        private async void UpdateClient()
        {
            var Parameters = new Dictionary<string, string>
            {
                { "Token", ClientToken }
            };

            bool Result = false;

            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/UpdateClient.php", new FormUrlEncodedContent(Parameters));
                var Body = await Response.Content.ReadAsStringAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.UpdateClient", Result);
                    return;
                }

                if (Body == "Success")
                {
                    Result = true;
                }
            }
            catch (Exception)
            {
            }

            SendEvent.Invoke("Functor.UpdateClient", Result);
        }

        public async void CloseClient()
        {
            if (ClientToken == null) { return; }

            var Parameters = new Dictionary<string, string>
            {
                { "Token", ClientToken }
            };

            bool Result = false;

            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/CloseClient.php", new FormUrlEncodedContent(Parameters));
                var Body = await Response.Content.ReadAsStringAsync();
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.CloseClient", Result);
                    return;
                }

                if (Body == "Success")
                {
                    Result = true;
                    ClientToken = null;
                }
            }
            catch (Exception)
            {
            }

            SendEvent.Invoke("Functor.CloseClient", Result);
        }

        public void RunCallbacks(float DeltaTime)
        {
            UpdateTimer += DeltaTime;

            if(UpdateTimer >= UpdateThreshold)
            {
                UpdateTimer = 0.0f;
                if(ServerToken != null)
                {
                    UpdateServer();
                }
                else if(ClientToken != null)
                {
                    UpdateClient();
                }
            }
        }

        #endregion

        public void Close()
        {
            if (ServerToken != null)
            {
                CloseServer();
            }
            else if (ClientToken != null)
            {
                CloseClient();
            }
        }
    }
}

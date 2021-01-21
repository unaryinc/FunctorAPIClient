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

        public static int RecusantServerSize = 48;

        private string Token;

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
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.CreateServerResponse", Result);
                    return;
                }
                var Body = await Response.Content.ReadAsStringAsync();

                if (!Body.Contains(" "))
                {
                    Token = Body;
                    Result = true;
                }
            }
            catch(Exception)
            {
            }

            SendEvent.Invoke("Functor.CreateServerResponse", Result);
        }

        public async void CloseServer()
        {
            if(Token == null) { return; }

            var Parameters = new Dictionary<string, string>
            {
                { "Token", Token }
            };

            bool Result = false;

            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/CloseServer.php", new FormUrlEncodedContent(Parameters));
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.CloseServerResponse", Result);
                    return;
                }
                var Body = await Response.Content.ReadAsStringAsync();

                if (Body == "Success")
                {
                    Result = true;
                    Token = null;
                }
            }
            catch(Exception)
            {
            }

            SendEvent.Invoke("Functor.CloseServerResponse", Result);
        }

        public async void UpdateServer(byte PlayerCount)
        {
            var Parameters = new Dictionary<string, string>
            {
                { "Token", Token },
                { "PlayerCount", PlayerCount.ToString() }
            };

            bool Result = false;
            
            try
            {
                var Response = await Client.PostAsync(Endpoint + (byte)Game + "/UpdateServer.php", new FormUrlEncodedContent(Parameters));
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.UpdateServerResponse", Result);
                    return;
                }
                var Body = await Response.Content.ReadAsStringAsync();
                if (Body == "Success")
                {
                    Result = true;
                }
            }
            catch(Exception)
            {
            }

            SendEvent.Invoke("Functor.UpdateServerResponse", Result);
        }

        private async void QueueMap(byte Faction)
        {
            try
            {
                var Response = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Map.bin");
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueMapResponse", null);
                    return;
                }
                var Body = await Response.Content.ReadAsByteArrayAsync();

                int MapCount = Body.Length / 4;

                uint[] MapIndexes = new uint[MapCount];

                for (int i = 0; i < MapCount; i++)
                {
                    MapIndexes[i] = BitConverter.ToUInt32(Body, i * 4);
                }

                SendEvent.Invoke("Functor.QueueMapResponse", MapIndexes);
            }
            catch(Exception)
            {
                SendEvent.Invoke("Functor.QueueMapResponse", null);
            }
        }

        private async void QueueProgress(byte Faction)
        {
            try
            {
                var Response = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Progress.bin");
                if(!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueProgressResponse", null);
                    return;
                }
                var Body = await Response.Content.ReadAsByteArrayAsync();

                bool[] MapIndexes = new bool[Body.Length];

                for (int i = 0; i < Body.Length; i++)
                {
                    MapIndexes[i] = BitConverter.ToBoolean(Body, i);
                }

                SendEvent.Invoke("Functor.QueueProgressResponse", MapIndexes);
            }
            catch(Exception)
            {
                SendEvent.Invoke("Functor.QueueProgressResponse", null);
            }
        }

        private async void QueueServers(byte Faction)
        {
            try
            {
                var Response = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Servers.bin");
                if (!Response.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueServersResponse", null);
                    return;
                }
                var Body = await Response.Content.ReadAsByteArrayAsync();

                int ServerCount = (Body.Length - 1) / RecusantServerSize;

                RecusantServer[] Servers = new RecusantServer[ServerCount];

                int Offset = 1;

                for (int i = 0; i < ServerCount; i++)
                {
                    bool Null = Body[Offset + 8 + 2 + 4 + 1] == 0;

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

                SendEvent.Invoke("Functor.QueueServersResponse", Servers);
            }
            catch(Exception)
            {
                SendEvent.Invoke("Functor.QueueServersResponse", null);
            }
        }

        public void QueueGlobalMap(byte Faction)
        {
            QueueMap(Faction);
            QueueProgress(Faction);
            QueueServers(Faction);
        }
    }
}

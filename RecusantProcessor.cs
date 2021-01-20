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
        // 15 bytes
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
        }

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

            bool Result;

            try
            {
                var Responce = await Client.PostAsync(Endpoint + (byte)Game + "/CreateServer.php", new FormUrlEncodedContent(Parameters));
                var Body = await Responce.Content.ReadAsStringAsync();

                if (!Body.Contains(" "))
                {
                    Token = Body;
                    Result = true;
                }
                else
                {
                    Result = false;
                }
            }
            catch(Exception)
            {
                Result = false;
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

            bool Result = true;

            try
            {
                var Responce = await Client.PostAsync(Endpoint + (byte)Game + "/CloseServer.php", new FormUrlEncodedContent(Parameters));
                var Body = await Responce.Content.ReadAsStringAsync();

                if (Body != "Success")
                {
                    Result = false;
                }

                if (Result)
                {
                    Token = null;
                }
            }
            catch(Exception)
            {
                Result = false;
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

            bool Result = true;
            
            try
            {
                var Responce = await Client.PostAsync(Endpoint + (byte)Game + "/UpdateServer.php", new FormUrlEncodedContent(Parameters));
                var Body = await Responce.Content.ReadAsStringAsync();
                if (Body != "Success")
                {
                    Result = false;
                }
            }
            catch(Exception)
            {
                Result = false;
            }

            SendEvent.Invoke("Functor.UpdateServerResponse", Result);
        }

        private async void QueueMap(byte Faction)
        {
            try
            {
                var Responce = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Map.bin");
                if (!Responce.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueMapResponse", null);
                    return;
                }
                var Body = await Responce.Content.ReadAsByteArrayAsync();

                uint[] MapIndexes = new uint[Body.Length / 4];

                for (int i = 0; i < Body.Length; i++)
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
                var Responce = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Progress.bin");
                if(!Responce.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueProgressResponse", null);
                    return;
                }
                var Body = await Responce.Content.ReadAsByteArrayAsync();

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
                var Responce = await Client.GetAsync(Endpoint + (byte)Game + "/Data/" + Faction + "/Servers.bin");
                if (!Responce.IsSuccessStatusCode)
                {
                    SendEvent.Invoke("Functor.QueueServersResponse", null);
                    return;
                }
                var Body = await Responce.Content.ReadAsByteArrayAsync();

                int ServerCount = (Body.Length - 1) / 15;

                RecusantServer[] Servers = new RecusantServer[ServerCount];

                int Offset = 1;

                for (int i = 0; i < ServerCount; i++)
                {
                    bool Null = true;

                    for(int k = 0; k < 8; ++k)
                    {
                        if(Body[Offset + k] != 0)
                        {
                            Null = false;
                            break;
                        }
                    }

                    if(!Null)
                    {
                        Servers[i] = new RecusantServer()
                        {
                            Owner = BitConverter.ToUInt64(Body, Offset),
                            Port = BitConverter.ToUInt16(Body, Offset + 8),
                            TileIndex = BitConverter.ToUInt32(Body, Offset + 8 + 2),
                            PlayerCount = Body[Offset + 8 + 2 + 4]
                        };
                    }

                    Offset += 15;
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FunctorAPI;
using Steamworks;

namespace FunctorAPITest
{
	public struct ChatMessage
	{
		public string Text;
		public ulong SteamID;
	}

	class Program
    {
        public static void EventTest(string EventName, object EventData)
        {
			string Data = EventData != null ? EventData.ToString() : "null";

			Console.WriteLine("Invoked Event " + EventName + " with data " + Data);

			if (EventName == "Functor.CreateServer")
            {
				NetServer.OnCreateServer(EventData);
			}
			else if(EventName == "Functor.CheckClient")
            {
				NetServer.OnCheckClient(EventData);
            }
			else if(EventName == "Functor.QueueServers")
            {
				RecusantProcessor.RecusantServer[] Servers = (RecusantProcessor.RecusantServer[])EventData;

				if(Servers != null && Servers.Length != 0)
                {
					Console.WriteLine("Server list:");
					for (int i = 0; i < Servers.Length; ++i)
					{
						Console.WriteLine(Servers[i].Owner + " " + Servers[i].TileIndex + " " + Servers[i].Username + " " + Servers[i].PlayerCount + " " + Servers[i].Port);
					}
					Console.WriteLine("Connecting to the first one with owner " + Servers[0].Owner);

					NetClient.Owner = new SteamNetworkingIdentity();
					NetClient.Owner.SetSteamID64(Servers[0].Owner);
					NetClient.Port = Servers[0].Port;
					NetClient.Init();
				}
				else
                {
					Console.WriteLine("No available servers found.");
					Running = false;
				}
			}
			else if(EventName == "Functor.CreateClient")
            {
				NetClient.OnCreateClient(EventData);
            }

			if(EventName == "OnChat")
            {
				ChatMessage Message = (ChatMessage)EventData;

				if(Server)
                {
					Console.WriteLine("Got message from client : " + Message.Text);

					if (Message.Text == "Thank you for verification.")
					{
						NetServer.SendMessage(Message.SteamID, "You are welcome.");
					}
				}
				else
                {
					Console.WriteLine("Got message from server : " + Message.Text);

					if(Message.Text == "Verified")
                    {
						NetClient.SendMessage("Thank you for verification.");
                    }
					else
                    {
						NetClient.Clear();
						Running = false;
					}
				}
            }
		}

        public static bool Running = true;
		public static bool Server = false;

		public static API API;
		public static RecusantProcessor Processor;

		private static DateTime Time1;
		private static DateTime Time2;
		private static float TimeDelta;

		private static bool SteamInit()
        {
			if (!Packsize.Test())
			{
				Console.WriteLine("Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.");
			}

			if (!DllCheck.Test())
			{
				Console.WriteLine("DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.");
			}

			try
			{
				if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
				{
					return false;
				}
			}
			catch (DllNotFoundException)
			{
				Console.WriteLine("Could not load [lib]steam_api.dll/so/dylib.");
				return false;
			}

			if (!SteamAPI.Init())
			{
				Console.WriteLine("SteamAPI_Init() failed.");
				return false;
			}

			return true;
		}

		private static NetworkingServer NetServer;
		private static NetworkingClient NetClient;

		private static void Time()
        {
			Time2 = DateTime.Now;
			TimeDelta = (float)((Time2.Ticks - Time1.Ticks) / 10000000.0);
			Time1 = Time2;
		}

		static void Main(string[] args)
        {
			API = new API(Game.Recusant, EventTest, "https://api.unary.me/");
			//API = new API(Game.Recusant, EventTest);
			Processor = (RecusantProcessor)API.Processor;

			Time1 = DateTime.Now;
			Time2 = DateTime.Now;
			TimeDelta = 0;

			if (!SteamInit())
            {
				return;
            }

			Console.WriteLine("Select test type: Server (s) or Client (c)");

			string Selected = Console.ReadLine();

			Server = Selected.ToLower() == "s";

			if(Server)
            {
				Console.WriteLine("Selected Server.");
				NetServer = new NetworkingServer(EventTest, Processor);
				NetServer.Init();
			}
			else
            {
				Console.WriteLine("Selected Client.");
				NetClient = new NetworkingClient(EventTest, Processor);
				NetClient.Queue();
			}

			while (Running)
            {
				Thread.Sleep(16);
				Time();
				Processor.RunCallbacks(TimeDelta);
				SteamAPI.RunCallbacks();
				if(NetServer != null)
                {
					NetServer.Update();
                }
				else if(NetClient != null)
                {
					NetClient.Update();
                }
			}

			Console.WriteLine("Press any key to continue.");
			Console.ReadKey();

			SteamAPI.Shutdown();
		}
    }
}

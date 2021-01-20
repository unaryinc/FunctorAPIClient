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
    class Program
    {
        public static void EventTest(string EventName, object EventData)
        {
			string Data = EventData != null ? EventData.ToString() : "null";

			Console.WriteLine("Invoked Event " + EventName + " with data " + Data);

			if (EventName == "Functor.CreateServerResponse")
            {
				OnCreateServer(EventData);
			}
			else if(EventName == "Functor.CloseServerResponse")
            {
				OnCloseServer(EventData);
			}
			else if(EventName == "Functor.UpdateServerResponse")
            {
				OnUpdateServer(EventData);
			}
		}

        public static bool Running = true;
		public static bool RunningTest = true;

		public static API API;
		public static RecusantProcessor Processor;
		public static HAuthTicket TicketHandle;

		private static void OnCreateServer(object Data)
        {
			Thread.Sleep(1000);
			Processor.UpdateServer(6);
		}

		private static void OnUpdateServer(object Data)
		{
			Thread.Sleep(1000);
			Processor.QueueGlobalMap(0);
			Thread.Sleep(5000);
			Processor.CloseServer();
		}

		private static void OnCloseServer(object Data)
		{
			Console.WriteLine("Closed server");
			SteamUser.CancelAuthTicket(TicketHandle);
			Console.ReadKey();
			Running = false;
		}

		public static void TestRun()
        {
			Thread.Sleep(1000);
			Console.WriteLine("Requesting a ticket.");
			byte[] Ticket = new byte[2048];
			uint TicketSize;
			TicketHandle = SteamUser.GetAuthSessionTicket(Ticket, Ticket.Length, out TicketSize);

			byte[] RealTicket = new byte[TicketSize];
			Array.Copy(Ticket, RealTicket, TicketSize);

			Processor.CreateServer(0, 0, (ushort)new Random().Next(0, 65535), RealTicket);
		}

        static void Main(string[] args)
        {
			API = new API(Game.Recusant, EventTest);
			Processor = (RecusantProcessor)API.Processor;

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
					return;
				}
			}
			catch (DllNotFoundException)
			{
				Console.WriteLine("Could not load [lib]steam_api.dll/so/dylib.");
				return;
			}

			if (!SteamAPI.Init())
			{
				Console.WriteLine("SteamAPI_Init() failed.");
				return;
			}

			TestRun();

			while(Running)
            {
				Thread.Sleep(16);
            }

			SteamAPI.Shutdown();
		}
    }
}

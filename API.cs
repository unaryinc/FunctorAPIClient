using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FunctorAPI
{
    public class API
    {
        private HttpClient Client;
        private Game TargetGame;
        private Dictionary<Game, Processor> Processors;

        private Action<string, object> Event;

        string Endpoint;

        public Processor Processor { get; private set; }
        public bool Available { get; private set; }

        public API(Game Game, Action<string, object> EventDispatch, string TargetEndpoint = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            Client = new HttpClient();
            TargetGame = Game;
            Event = EventDispatch;

            if(TargetEndpoint == null)
            {
                #if DEBUG
                Endpoint = "http://localhost:8000/";
                #else
                Endpoint = "https://api.unary.me/";
                #endif
            }
            else
            {
                Endpoint = TargetEndpoint;
            }

            // Processor registration
            Processors = new Dictionary<Game, Processor>();
            Processors[Game.Recusant] = new RecusantProcessor();

            foreach (var Processor in Processors)
            {
                Processor.Value.SendEvent = Event;
                Processor.Value.Client = Client;
                Processor.Value.Endpoint = Endpoint;
                Processor.Value.Game = TargetGame;
            }

            Processor = Processors[Game];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FunctorAPI
{
    public abstract class Processor
    {
        public Action<string, object> SendEvent;
        public HttpClient Client;
        public string Endpoint;
        public Game Game;
    }
}

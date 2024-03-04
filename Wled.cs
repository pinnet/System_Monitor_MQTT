using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    internal class Wled(string id) : IClient
    {
        public string Id { get; set; } = id;
        public List<string> Filters { get; set; } = new List<string>();

        public void connect()
        {
            throw new NotImplementedException();
        }

        public void disconnect()
        {
            throw new NotImplementedException();
        }

        public void onMessageReceived(string topic, string message)
        {
            throw new NotImplementedException();
        }

        public void publish(string topic, string message)
        {
            throw new NotImplementedException();
        }

        public void subscribe(string topic)
        {
            Console.WriteLine("Subscribed to " + topic);
        }

        public void unsubscribe(string topic)
        {
            throw new NotImplementedException();
        }
    }   
}

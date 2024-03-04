using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    internal interface IClient
    {
        List<string> Filters { get; set; }
        string Id { get; set; }
        void connect();
        void disconnect();
        void publish(string topic, string message);
        void subscribe(string topic);
        void unsubscribe(string topic);
        void onMessageReceived(string topic, string message);
    }
}

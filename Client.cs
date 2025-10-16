using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    internal class Client
    {
        public string ID { get; set; }
        public bool IsAdmin { get; set; }
        public Dictionary<string, MqttQualityOfServiceLevel> Subscriptions { get; set; }

        public Client(string id, bool isAdmin = false, Dictionary<string, MqttQualityOfServiceLevel>? subscriptions = null )
        {
            ID = id;
            IsAdmin = isAdmin;
            if (subscriptions == null)
            {
                Subscriptions = new Dictionary<string, MqttQualityOfServiceLevel>();
            }
            else
            {
                Subscriptions = subscriptions;
            }
        }
     
    }
    
}

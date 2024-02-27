using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    internal class MessageCache
    {
        public List<MQTTMessage> Messages { get; set; }

        public MessageCache()
        {
            Messages = new List<MQTTMessage>();
        }

        public void AddMessage(string topic, string payload)
        {
            Messages.Add(new MQTTMessage { Topic = topic, Payload = payload });
        }

        public void ClearMessages()
        {
            Messages.Clear();
        }

        public List<MQTTMessage> GetMessages()
        {
            return Messages;
        }

        public void RemoveMessage(int index)
        {
            Messages.RemoveAt(index);
        }

        public void RemoveMessage(MQTTMessage message)
        {
            Messages.Remove(message);
        }

        public void RemoveMessage(string topic)
        {
            Messages.RemoveAll(m => m.Topic == topic);
        }

        public void RemoveMessage(string topic, string payload)
        {
            Messages.RemoveAll(m => m.Topic == topic && m.Payload == payload);
        }

        public bool ContainsMessage(string topic, string payload)
        {
            return Messages.Any(m => m.Topic == topic && m.Payload == payload);
        }

    }
}

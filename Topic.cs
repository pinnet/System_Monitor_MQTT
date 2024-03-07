using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    
    internal class Topic
    {
        string RootTopic { get; set; }
                
        public Topic(string rootTopic)
        {
            RootTopic = rootTopic;
        }

        public string AppendSubTopic(string subTopic,int index)
        {
           return RootTopic + "/" + subTopic.Replace("/"," ") + " {"+ index.ToString()+ "}";
        }
    
    }
}

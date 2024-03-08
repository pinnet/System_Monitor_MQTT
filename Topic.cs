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
        static Dictionary<string,Dictionary<int,string>> rootTopics = new Dictionary<string,Dictionary<int,string>>();
        
        public Topic(string rootTopic)
        {
            RootTopic = rootTopic;
        }

        public string AppendSubTopic(string subTopic,int index)
        {

            subTopic = subTopic.Replace("/", " ");

            if (rootTopics.TryGetValue(RootTopic, out Dictionary<int, string>? subTopics))
            {
                if (subTopics.TryGetValue(index, out string? value))
                {
                    return RootTopic + "/" + value;
                }
                else
                {

                    int countDups = 0;

                    foreach (string name in subTopics.Values)
                    {
                       if(name.Contains(subTopic))
                       {
                           countDups++;
                       }
                    }

                    if (countDups > 0)
                    {
                        subTopic = subTopic + " [" + countDups.ToString() + "]";
                    }

                    subTopics.Add(index, subTopic);
                    return RootTopic + "/" + subTopic;
                }
            }
            else
            {
                rootTopics.Add(RootTopic, new Dictionary<int, string>());
                rootTopics[RootTopic].Add(index, subTopic);
            }

            return RootTopic + "/" + subTopic;
        }
    
    }
}

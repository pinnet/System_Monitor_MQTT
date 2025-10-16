using System;
using LibreHardwareMonitor.Hardware;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Configuration;

namespace System_Monitor_MQTT
{
    public sealed class WindowsBackgroundService(HWmonitorService HWMService,ILogger<WindowsBackgroundService> logger) : BackgroundService
    {
        double updateInterval = Convert.ToDouble(Settings.UpdateInterval);
        

#if DEBUG
        int port = 1883;
#else
        int port = Convert.ToInt16(Settings.Port);
#endif


        List<Client> clients = new List<Client>();
        MessageCache messageCache = new MessageCache();
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            IList<IHardware> hardware = HWMService.Monitor();
            clients.Add(new Client(Settings.ServerName, true));
            var mqttFactory = new MqttFactory();
            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithDefaultEndpointPort(port)
                .WithDefaultEndpoint()
                .Build();
            
            try
            {
                using (var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions))
                {
                    
                    mqttServer.InterceptingSubscriptionAsync += e =>
                    {
                        Client? client = getClient(e.ClientId);
                        string topic = e.TopicFilter.Topic.StartsWith("$") ? "$" : e.TopicFilter.Topic;

                        if (client != null)
                        {
                            if (!client.IsAdmin)
                            {
                                switch (topic)
                                {
                                    case "#":
                                    case "$":
                                        e.ReasonString = "You are not authorized to subscribe to this topic.";
                                        e.ProcessSubscription = false;
                                        break;
                                    default:
                                        e.ProcessSubscription = true;
                                        break;
                                }
                            }
                        }
                        return Task.CompletedTask;
                    };
                    mqttServer.InterceptingPublishAsync += e =>
                    {
                       if (e.ClientId == Settings.ServerName)
                       {
                           e.ProcessPublish = true;
                           return Task.CompletedTask;
                       }

                       if (e.ApplicationMessage.Topic.StartsWith("$"))
                       {
                           e.ProcessPublish = false;
                       }
                       else
                       {
                           e.ProcessPublish = true;
                       }
                       return Task.CompletedTask;
                    };
                    mqttServer.ValidatingConnectionAsync += e =>
                    {   
                        if (getClient(e.ClientId) != null)
                        {
                            e.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
                            return Task.CompletedTask;
                        } 
                        if( e.UserName == Settings.AdminName)
                        {
                            if (e.Password == Settings.AdminPassword)
                            {
                                e.SessionItems.Add("IsAdmin", "true");
                                e.ReasonString = "Admin connected";
                                e.ReasonCode = MqttConnectReasonCode.Success;
                                return Task.CompletedTask;
                            }
                        }
                        if (e.UserName == Settings.UserName)
                        {
                            if (e.Password == Settings.UserPassword)
                            {
                                e.ReasonCode = MqttConnectReasonCode.Success;
                                return Task.CompletedTask;
                            }
                        }

                        e.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                        return Task.CompletedTask;
             
                    };

                    mqttServer.ClientConnectedAsync += async e =>
                    {
                        Console.WriteLine($"Client connected: {e.ClientId}" );
                        Console.WriteLine(e.SessionItems);
                        bool isAdmin = false;
                        
                        if (e.SessionItems["IsAdmin"] != null)
                        {
                            isAdmin = e.SessionItems["IsAdmin"]?.ToString() == "true";
                        }
                        clients.Add(new Client(e.ClientId, isAdmin));
                        
                        messageCache.ClearMessages();
                        await Task.CompletedTask;
                    };

                    mqttServer.ClientDisconnectedAsync += async e =>
                    {
                        Console.WriteLine("Client disconnected: {0}", e.ClientId);
                        Client? client = getClient(e.ClientId);
                        if (client != null)
                        {
                            clients.Remove(client);
                        }
                        await Task.CompletedTask;
                    };

                    mqttServer.ClientSubscribedTopicAsync += async e =>
                    { 
                        
                        Console.WriteLine("  Client '{0}' subscribed to topic '{1}'",e.ClientId, e.TopicFilter.Topic);
                        Client? client = getClient(e.ClientId);
                        if (client != null)
                        {
                            client.Subscriptions.Add(e.TopicFilter.Topic, e.TopicFilter.QualityOfServiceLevel);
                        }                       
                        await Task.CompletedTask;
                    };

                    await mqttServer.StartAsync();
                    
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (clients.Count > 0)
                        {
                            publishHWInfo(hardware, mqttServer, clients);
                        }
                        await Task.Delay(TimeSpan.FromMilliseconds(updateInterval), stoppingToken);
                    }
                    HWMService.CloseComputer();
                    await mqttServer.StopAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // When the stopping token is canceled, for example, a call made from services.msc,
                // we shouldn't exit with a non-zero exit code. In other words, this is expected...
            }
            catch (Exception ex)
            {
                Console.WriteLine("An unhandled exception occurred.");
                Console.WriteLine("Message: {0}", ex.Message);
                logger.LogError(ex, "{Message}", ex.Message);

                // Terminates this process and returns an exit code to the operating system.
                // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
                // performs one of two scenarios:
                // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
                // 2. When set to "StopHost": will cleanly stop the host, and log errors.
                //
                // In order for the Windows Service Management system to leverage configured
                // recovery options, we need to terminate the process with a non-zero exit code.
                Environment.Exit(1);
            }

        }
        private Client? getClient(string id)
        {
            foreach (Client client in clients)
            {
                if (client.ID == id)
                {
                    return client;
                }
            }
            return null;
        }
        private async void publishHWInfo(IList<IHardware> hardware, MqttServer mqttServer, List<Client> clients)
        {
            List<string> filters = new List<string>();

            clients.Sort((x, y) => x.ID.CompareTo(y.ID));
            foreach (Client client in clients)
            {
                foreach (KeyValuePair<string, MqttQualityOfServiceLevel> subscription in client.Subscriptions)
                {
                    string rootTopic = subscription.Key.Split("/")[0];
                    if (!filters.Contains(rootTopic))
                    {
                        filters.Add(rootTopic);
                    }
                }
                if (client.IsAdmin)
                {
                   await publishUsers(mqttServer,clients);
                }
            }

            for (int i = 0; i < hardware.Count; i++)
            {
                string topic = "";
                hardware[i].Update();
                switch (hardware[i].HardwareType)
                {
                    case HardwareType.Cpu:
                        if (filters.Contains("CPU"))
                        {
                            topic = new Topic("CPU").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Motherboard:
                        if (filters.Contains("MOTHERBOARD"))
                        {
                            topic = new Topic("MOTHERBOARD").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.GpuIntel:
                        if (filters.Contains("GPU"))
                        {
                            topic = new Topic("GPU/Intel").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.GpuNvidia:
                        if (filters.Contains("GPU"))
                        {
                            topic = new Topic ("GPU/Nvidia").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.GpuAmd:
                        if (filters.Contains("GPU"))
                        {
                            topic = new Topic("GPU/AMD").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Storage:
                        if (filters.Contains("STORAGE"))
                        {
                            topic = new Topic("STORAGE").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Network:
                        if (filters.Contains("NETWORK"))
                        {
                            topic = new Topic("NETWORK").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Memory:
                        if (filters.Contains("MEMORY"))
                        {
                            topic = new Topic("MEMORY").AppendSubTopic(hardware[i].Name,i);
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    default:
                        topic = "";
                        break;
                }

                if (hardware[i].Sensors.Length > 0 && topic != "")
                {
                    for (int j = 0; j < hardware[i].Sensors.Length; j++)
                    {
                        string? value = hardware[i].Sensors[j].Value.ToString();
                        string subName = hardware[i].Sensors[j].Name.Replace('/', '-');
                        string subtopic = topic + "/" + subName;
                        await publishCachedAsync(mqttServer, subtopic, value != null ? value : "");
                    }
                }
                if (hardware[i].SubHardware.Length > 0 && topic != "")
                {

                    for (int k = 0; k < hardware[i].SubHardware.Length; k++)
                    {
                        string subtopic = topic + "/" + hardware[i].SubHardware[k].Name.Replace('/', '-');


                        hardware[i].SubHardware[k].Update();
                        await publishCachedAsync(mqttServer, subtopic);
                        for (int l = 0; l < hardware[i].SubHardware[k].Sensors.Length; l++)
                        {
                            string? value = hardware[i].SubHardware[k].Sensors[l].Value.ToString();
                            string subsubtopic = subtopic + "/" + hardware[i].SubHardware[k].Sensors[l].Name.Replace('/', '-');
                            await publishCachedAsync(mqttServer, subsubtopic, value != null ? value : "");
                        }
                    }
                }
            }
        }

        private async Task publishUsers(MqttServer mqttServer, List<Client> clients)
        {
            foreach (Client client in clients)
            {
                string topic = "$SYS/USERS/" + client.ID;
                string payload = "Admin: " + client.IsAdmin;
                await publishCachedAsync(mqttServer, topic, payload);
            }
        }

        private async Task publishCachedAsync(MqttServer server, string topic, string payload = "")
        {
           
            if (messageCache.Messages.Count > 2000)
            {
                messageCache.ClearMessages();
            }

            if (!messageCache.ContainsMessage(topic, payload))
            {
                messageCache.AddMessage(topic, payload);
                await publishAsync(server, topic, payload);
            }
        }
        private async Task publishAsync(MqttServer server, string topic, string payload)
        {
            //Console.WriteLine("Publishing message '{0}' to topic '{1}'", payload, topic);
            topic = topic.Replace("#", "_");
            topic = topic.Replace("+", "");
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();
            await server.InjectApplicationMessage(
               new InjectedMqttApplicationMessage(message)
               {
                   SenderClientId = Settings.ServerName
               }) ;
        }
    }
    
}

using LibreHardwareMonitor.Hardware;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Data.SqlTypes;
using System.Runtime.CompilerServices;

namespace System_Monitor_MQTT
{
    public sealed class WindowsBackgroundService(HWmonitorService HWMService,ILogger<WindowsBackgroundService> logger) : BackgroundService
    {
        int currentCpuSpeed = 0;
        List<IClient> wledClients = new List<IClient>();
        List<IClient> monitorClients = new List<IClient>();
        MessageCache messageCache = new MessageCache();
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            List<string> hwFilters = new List<string>() { };
            IList<IHardware> hardware = HWMService.Monitor();
           
            var mqttFactory = new MqttFactory();
            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithDefaultEndpointPort(1882)
                .WithDefaultEndpoint()
                .Build();
            
            try
            {
                using (var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions))
                {
                    mqttServer.ValidatingConnectionAsync += e =>
                    {
                        if (e.UserName != "SystemUser")
                        {
                            e.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                        }

                        if (e.Password != "12345678")
                        {
                            e.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                        }

                        return Task.CompletedTask;
                    };

                    mqttServer.ClientConnectedAsync += async e =>
                    {

                        Console.WriteLine("Client connected: {0}", e.ClientId);
                        if(e.ClientId.Contains("WLED"))
                        {
                            wledClients.Add(new Wled(e.ClientId));
                        }
                        if (e.ClientId.Contains("monitor"))
                        {
                            monitorClients.Add(new Monitor(e.ClientId));
                        }
                        messageCache.ClearMessages();
                        await Task.CompletedTask;
                    };

                    mqttServer.ClientDisconnectedAsync += async e =>
                    {
                 
                        await Task.CompletedTask;
                    };

                    mqttServer.ClientSubscribedTopicAsync += async e =>
                    {

                        string id = e.ClientId;

                        Console.WriteLine("  Client '{0}' subscribed to topic '{1}'", id, e.TopicFilter.Topic);
                        
                        string rootTpoic = e.TopicFilter.Topic.Split('/')[0];

                        hwFilters.Add(rootTpoic);

                        //await publishAsync(mqttServer, e.TopicFilter.Topic,"['I9 14900k','intel GPU']");
                        
                        await Task.CompletedTask;
                    };

                    await mqttServer.StartAsync();
                    
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (monitorClients.Count > 0)
                        {
                            publishHWInfo(hardware, mqttServer, hwFilters);
                        }

                        if (wledClients.Count > 0)
                        {
                            foreach (IHardware hw in hardware)
                            {
                                if (hw.HardwareType == HardwareType.Cpu)
                                {
                                    foreach (ISensor sensor in hw.Sensors)
                                    {
                                        if (sensor.Name.Contains("CPU Total"))
                                        {
                                            hw.Update();
                                            string? val = sensor.Value.ToString();
                                            if (val == null) return;
                                            int speed = (int)(float.Parse(val) * 2.55);
                                          
                                            if (speed > currentCpuSpeed + 5 || speed < currentCpuSpeed - 5)
                                            {
                                                currentCpuSpeed = speed;
                                                await publishAsync(mqttServer, "wled/all/api", "SX=" + currentCpuSpeed.ToString());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
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
        
        private async void publishHWInfo(IList<IHardware> hardware, MqttServer mqttServer, List<string> filters)
        {
            
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
                            topic =new Topic("GPU/AMD").AppendSubTopic(hardware[i].Name,i);
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
                    SenderClientId = "server"
                });
        }
    }
}

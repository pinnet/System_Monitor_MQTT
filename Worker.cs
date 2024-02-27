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
        List<string> wledClients = new List<string>();
        List<string> monitorClients = new List<string>();
        MessageCache messageCache = new MessageCache();
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            List<string> hwFilters = new List<string> { };
            IList<IHardware> hardware = HWMService.Monitor();
            var mqttFactory = new MqttFactory();
            var mqttServerOptions = new MqttServerOptionsBuilder().WithDefaultEndpoint().Build();
            
            try
            {
                using (var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions))
                {
                    bool setupWLED = false;
                    mqttServer.ClientConnectedAsync += async e =>
                    {
                        //Console.WriteLine("Client connected: {0}", e.ClientId);
                        if(e.ClientId.Contains("WLED"))
                        {
                            wledClients.Add(e.ClientId);
                            setupWLED = true;
                        }
                        if (e.ClientId.Contains("monitor"))
                        {
                            monitorClients.Add(e.ClientId);
                            string id = e.ClientId.Remove(0,e.ClientId.IndexOf("{") + 1);
                            id = id.Remove(id.IndexOf("}"),1);
                            id = id.ToUpper();

                            string[] filters = id.Split(',');

                            foreach (string filter in filters)
                            {
                                //Console.WriteLine("Adding filter: {0}", filter);
                                hwFilters.Add(filter);
                            }

                        }
                        messageCache.ClearMessages();
                        await Task.CompletedTask;
                    };

                    mqttServer.ClientDisconnectedAsync += async e =>
                    {
                        Console.WriteLine("Client disconnected: {0}", e.ClientId);
                        if (e.ClientId.Contains("WLED"))
                        {
                            wledClients.Remove(e.ClientId);
                        }
                        if (e.ClientId.Contains("monitor"))
                        {
                            monitorClients.Remove(e.ClientId);
                            hwFilters.Clear();
                        }
                        await Task.CompletedTask;
                    };

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
        private async void publishWLEDSetup(MqttServer mqttServer)
        { 
            await publishCachedAsync(mqttServer, "wled/all/api", "FX=8");
            await publishCachedAsync(mqttServer, "wled/all/api", "SX=0");
            await publishCachedAsync(mqttServer, "wled/all/col", "#FFFFFF");
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
                            topic = "CPU/" + hardware[i].Name;
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Motherboard:
                        if (filters.Contains("MOTHERBOARD"))
                        {
                            topic = "Motherboard/" + hardware[i].Name;
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.GpuIntel:
                        if (filters.Contains("GPU"))
                        {
                            topic = "GPU/Intel/" + hardware[i].Name;
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.GpuNvidia:
                        if (filters.Contains("GPU"))
                        {
                            topic = "GPU/Nvidia/" + hardware[i].Name;
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.GpuAmd:
                        if (filters.Contains("GPU"))
                        {
                            topic = "GPU/AMD/" + hardware[i].Name;
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Storage:
                        if (filters.Contains("STORAGE"))
                        {
                            topic = "Storage/" + hardware[i].Name;
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Network:
                        if (filters.Contains("NETWORK"))
                        {
                            topic = "Network/" + hardware[i].Name;
                            await publishCachedAsync(mqttServer, topic);
                        }
                        break;
                    case HardwareType.Memory:
                        if (filters.Contains("MEMORY"))
                        {
                            topic = "Memory/" + hardware[i].Name;
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
                        string subtopic = topic + "/" + hardware[i].Sensors[j].Name.Replace('/', '-');
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

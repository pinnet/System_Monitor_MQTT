using LibreHardwareMonitor.Hardware;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    public sealed class HWmonitorService
    {
        private Computer? computer;

        public IList<IHardware> Monitor()
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };
            computer.Open();

            computer.Accept(new UpdateVisitor());

            return computer.Hardware;

        }
        public void CloseComputer()
        {
            if (computer == null) return;
            computer.Close();
        }

    }
}

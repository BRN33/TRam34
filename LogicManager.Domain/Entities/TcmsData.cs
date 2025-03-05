using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Domain.Entities
{
    public class TcmsData
    {
        public int TrainId { get; set; }
        public bool IsMaster { get; set; }

        public int ZeroSpeed { get; set; }
        public bool DoorLeftReleased { get; set; }
        public bool DoorRightReleased { get; set; }
        public bool AllDoorsReleased { get; set; }
        public bool EmergencyBrakeActive { get; set; }
        public bool ServiceBrakeActive { get; set; }
        public int BatteryVoltage { get; set; }
        public DateTime Timestamp { get; set; }

        //public bool AllDoorsReleased => DoorLeftReleased && DoorRightReleased;
        public bool IsZeroSpeed => ZeroSpeed == 0;
    }
}

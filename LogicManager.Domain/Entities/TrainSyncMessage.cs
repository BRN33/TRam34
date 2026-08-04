using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Domain.Entities
{
    public class TrainSyncMessage
    {
        public string? Type { get; set; }
        public string? TrainId { get; set; }
        public string? Ip { get; set; }
        public string? NextStation { get; set; }
        public int? RemainingDistance { get; set; }
        public int TotalDistance { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int StationIndex { get; set; }
        public int? DistanceFromStart { get; set; }
    }
}

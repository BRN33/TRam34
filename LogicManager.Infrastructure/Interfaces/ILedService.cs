using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Infrastructure.Interfaces;

public interface ILedService
{
   Task UpdateDisplay(LedDisplayType displayType, string stationName);
}

public enum LedDisplayType
{
    stationStartLed,
    stationArrivalLed,
    stationApproachLed,
    stationPrivateLed,
    stationTerminalLed,
    stationTransferLed
}

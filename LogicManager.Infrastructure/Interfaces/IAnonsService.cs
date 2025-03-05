using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Infrastructure.Interfaces;



public interface IAnonsService
{
    Task PlayAnnouncementAsync(AnnouncementType type, string stationName, string destinationName);
}


public enum AnnouncementType
{
    Start, //Baslangıc Anonsu
    Arrival,//Gelecek istasyon Anonsu
    Approaching,//Yaklaşım Anonsu
    Special,//Özel Anons
    Terminal,//Terminal Anonsu
    Transfer
}

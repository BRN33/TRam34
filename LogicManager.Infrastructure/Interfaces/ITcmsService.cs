using LogicManager.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Infrastructure.Interfaces;

public interface ITcmsService
{
    Task<TcmsData> GetTcmsDataAsync();//TCMS verileri alma
        
    Task<bool> CheckIfLeaderAsync();//Master-Slave durum kontrolü

    Task<bool> IsConnectedAsync();
   
    event EventHandler<TcmsData> OnTcmsDataReceived;
}

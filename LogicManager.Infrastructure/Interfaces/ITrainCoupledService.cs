using LogicManager.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Infrastructure.Interfaces;

public interface ITrainCoupledService
{
    Task<TrainCouplingData> GetLastTrainData();


    event Action<TrainCouplingData> OnTrainDataUpdated;

}


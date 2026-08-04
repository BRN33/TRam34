using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LogicManager.Domain.Entities;

public class TrainCouplingData
{
    [JsonPropertyName("masterTrainId")]
    public string MasterTrainId { get; set; } = string.Empty;

    [JsonPropertyName("currentTrain")]
    public CurrentTrainInfo CurrentTrain { get; set; } = new();

    [JsonPropertyName("couplingTrainsIds")]
    public List<string?> CouplingTrainsIds { get; set; } = new();
}

public class CurrentTrainInfo
{
    [JsonPropertyName("id")]
    public string ID { get; set; } = string.Empty;

    [JsonPropertyName("ip")]
    public string IP { get; set; } = string.Empty;

    [JsonPropertyName("trainCoupledOrder")]
    public int TrainCoupledOrder { get; set; }
}

using System.Text.Json.Serialization;

namespace LogicManager.Domain.Entities;

public class TakoData
{
    [JsonPropertyName("slot")]
    public int Slot { get; set; }

    [JsonPropertyName("io")]
    public IoData? Io { get; set; }
}

public class IoData
{
    [JsonPropertyName("di")]
    public List<DiData>? Di { get; set; }
}

public class DiData
{
    [JsonPropertyName("diIndex")]
    public int DiIndex { get; set; }

    [JsonPropertyName("diMode")]
    public int DiMode { get; set; }

    [JsonPropertyName("diStatus")]
    public int DiStatus { get; set; }
}

using LogicManager.Shared.DTOs.BaseDto;

namespace LogicManager.Shared.DTOs;

public class EventLogDto:BaseEntityDto
{
    public string? SourceIP { get; set; }
    public string? DestinationName { get; set; }
    public string? DestinationIP { get; set; }
}

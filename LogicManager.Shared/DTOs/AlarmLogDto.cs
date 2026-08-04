using LogicManager.Shared.DTOs.BaseDto;

namespace LogicManager.Shared.DTOs;

public class AlarmLogDto:BaseEntityDto
{
    public string? MessageSourceType { get; set; }
    public string? HardwareIP { get; set; }
}

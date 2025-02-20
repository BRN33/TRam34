using LogicManager.Shared.DTOs.BaseDto;

namespace LogicManager.Shared.DTOs;

public class AlarmLogDto:BaseEntityDto
{
    public string? AlarmType { get; set; }
    public string? HardwareIP { get; set; }
}

using LogicManager.Shared.DTOs.BaseDto;

namespace LogicManager.Shared.DTOs;

public class WarningLogDto:BaseEntityDto
{
    public string? WarningType { get; set; }
    public string? HardwareIP { get; set; }
}

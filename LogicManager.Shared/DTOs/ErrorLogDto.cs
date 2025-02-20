using LogicManager.Shared.DTOs.BaseDto;

namespace LogicManager.Shared.DTOs;

public class ErrorLogDto:BaseEntityDto
{
    public string? ErrorType { get; set; }
    public string? HardwareIP { get; set; }
}

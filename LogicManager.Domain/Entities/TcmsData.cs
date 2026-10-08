namespace LogicManager.Domain.Entities;


public class TcmsData
{
    public bool TachoMeterPulse { get; set; } // Pulse modu: her durum değişimi = PulseDistanceMeters
    public double? TachoMeterDistance { get; set; } // Meter modu: TCMS'in gönderdiği metraj (kümülatif veya istasyonda sıfırlanan)
    public bool ZeroSpeed { get; set; }
    public double TrainSpeed { get; set; }
    public Doors Doors { get; set; } = new Doors();
}

public class Doors
{
    public bool AllDoorOpen { get; set; }
    public bool AllDoorClose { get; set; }
    public bool AllDoorReleased { get; set; }
    public bool AllLeftDoorOpen { get; set; }
    public bool AllRightDoorOpen { get; set; }
    public bool AllLeftDoorClose { get; set; }
    public bool AllRightDoorClose { get; set; }
    public bool AllLeftDoorReleased { get; set; }
    public bool AllRightDoorReleased { get; set; }
}

namespace LogicManager.Domain.Entities;

public class Station
{

    public int stationSequenceId { get; set; }// İstasyonun sıralı numarası.
    public string? stationName { get; set; }//İstasyonun adı.
    public int stationDistance { get; set; }//Duraktan mesafe.
    public bool skipStationState { get; set; }// İstasyonun atlanıp atlanmayacağı.
    public int stationArrivalAnnounceDistance { get; set; }//Varış duyurusunun yapılacağı mesafe.
    public int stationApproachAnnounceDistance { get; set; }//Yaklaşma duyurusunun yapılacağı mesafe.
    public bool stationStartAnnounce { get; set; }//Durak başlangıç duyurusunun yapılıp yapılmayacağı.
    public int stationTransferAnnounce { get; set; }//Aktarma duyurusunun yapılıp yapılmayacağı.
    public int stationPrivateAnnounce { get; set; }//Özel duyuru olup olmadığı.
    public bool terminalAnnounce { get; set; }//Terminal duyurusunun yapılıp yapılmayacağı.
    public bool doorRightStatus { get; set; }//Sağ kapının durumu.
    public bool doorLeftStatus { get; set; }//Sol kapının durumu.
    public int stoppingPointDistance { get; set; }// Durak durma noktasına olan mesafe.


}
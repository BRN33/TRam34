namespace LogicManager.Infrastructure.Services;

/// <summary>
/// TCMS'den gelen tako verisini "istasyondan bu yana kat edilen metre"ye çevirir.
///
/// Pulse modu : TachoMeterPulse (bool) her durum değişiminde (true→false / false→true)
///              PulseDistanceMeters kadar mesafe eklenir. Aynı değerin tekrar gelmesi mesafe eklemez.
/// Meter modu : TachoMeterDistance (metre) okunur. Değer kümülatif (odometre) olsa da,
///              TCMS her istasyonda sıfırlasa da çalışır: istasyonda referans alınır,
///              mesafe = güncel değer - referans. Değer geriye düşerse (TCMS reset) referans 0 kabul edilir.
///
/// Thread-safe: RabbitMQ consumer thread'i yazar, 100 ms döngüsü okur.
/// </summary>
public class TakoDistanceCalculator
{
    public enum TakoMode { Pulse, Meter }

    private readonly object _lock = new();
    private readonly double _pulseDistanceMeters;
    private readonly bool _countBothEdges;

    // Pulse modu durumu
    private bool? _lastPulseState;
    private DateTime _lastPulseMessageTime = DateTime.MinValue;

    // Meter modu durumu
    private double? _lastMeterValue;
    private double _meterReference;
    private bool _referencePending = true; // ilk metre değeri gelince referans alınacak

    private double _segmentDistance;

    public TakoMode Mode { get; }

    /// <summary>Pulse modunda olası pulse kaçırma uyarısı (mesaj başına tahmini mesafe, pulse mesafesini aşarsa).</summary>
    public event Action<string>? OnWarning;

    public TakoDistanceCalculator(TakoMode mode, double pulseDistanceMeters, bool countBothEdges)
    {
        if (pulseDistanceMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(pulseDistanceMeters), "Pulse mesafesi 0'dan büyük olmalı.");

        Mode = mode;
        _pulseDistanceMeters = pulseDistanceMeters;
        _countBothEdges = countBothEdges;
    }

    public static TakoMode ParseMode(string? value) =>
        string.Equals(value, "Meter", StringComparison.OrdinalIgnoreCase) ? TakoMode.Meter : TakoMode.Pulse;

    /// <summary>İstasyondan bu yana kat edilen mesafe (metre, tam sayı).</summary>
    public int SegmentDistanceMeters
    {
        get { lock (_lock) return (int)Math.Round(_segmentDistance, MidpointRounding.AwayFromZero); }
    }

    /// <summary>Her yeni TCMS mesajında çağrılır.</summary>
    public void Process(bool pulseState, double? meterValue, double trainSpeedKmh, DateTime receivedAt)
    {
        lock (_lock)
        {
            if (Mode == TakoMode.Pulse)
                ProcessPulse(pulseState, trainSpeedKmh, receivedAt);
            else
                ProcessMeter(meterValue);
        }
    }

    private void ProcessPulse(bool pulseState, double trainSpeedKmh, DateTime receivedAt)
    {
        // İlk mesaj: sadece başlangıç durumunu öğren, mesafe ekleme
        if (_lastPulseState is null)
        {
            _lastPulseState = pulseState;
            _lastPulseMessageTime = receivedAt;
            return;
        }

        if (pulseState != _lastPulseState.Value)
        {
            // Rising edge (false→true) her zaman sayılır; falling edge sadece CountBothEdges açıksa
            if (_countBothEdges || pulseState)
                _segmentDistance += _pulseDistanceMeters;

            _lastPulseState = pulseState;
        }

        // Kaçırma kontrolü: iki mesaj arasında tren, bir pulse mesafesinden fazla gittiyse
        // aradaki durum değişimleri görülememiş olabilir.
        if (_lastPulseMessageTime != DateTime.MinValue && trainSpeedKmh > 0)
        {
            var elapsedSec = (receivedAt - _lastPulseMessageTime).TotalSeconds;
            var estimatedMeters = trainSpeedKmh / 3.6 * elapsedSec;
            var metersPerCountedEdge = _countBothEdges ? _pulseDistanceMeters : _pulseDistanceMeters * 2;
            if (elapsedSec > 0 && estimatedMeters > metersPerCountedEdge)
            {
                OnWarning?.Invoke(
                    $"Olası pulse kaçırma: {elapsedSec * 1000:n0} ms'de ~{estimatedMeters:n1} m gidildi " +
                    $"(hız {trainSpeedKmh:n1} km/h), pulse başına {metersPerCountedEdge} m.");
            }
        }
        _lastPulseMessageTime = receivedAt;
    }

    private void ProcessMeter(double? meterValue)
    {
        if (meterValue is null || double.IsNaN(meterValue.Value) || meterValue.Value < 0)
            return; // geçersiz değer: mevcut mesafeyi koru

        var value = meterValue.Value;

        if (_referencePending)
        {
            // Mevcut segment mesafesi (0 veya sync'ten gelen değer) korunacak şekilde referans al
            _meterReference = value - _segmentDistance;
            _referencePending = false;
        }
        else if (_lastMeterValue.HasValue && value < _lastMeterValue.Value)
        {
            // TCMS sayacı sıfırladı (istasyonda reset veya restart). Kaldığımız mesafeyi kaybetmemek için
            // referansı, o ana kadarki mesafe korunacak şekilde kaydır.
            _meterReference = -_segmentDistance;
        }

        _lastMeterValue = value;
        _segmentDistance = Math.Max(value - _meterReference, 0);
    }

    /// <summary>İstasyona varışta / yeni rotada çağrılır. TCMS'e reset göndermez, mantıksal sıfırlama yapar.</summary>
    public void ResetSegment()
    {
        lock (_lock)
        {
            _segmentDistance = 0;
            if (_lastMeterValue.HasValue)
                _meterReference = _lastMeterValue.Value;
            else
                _referencePending = true;
        }
    }

    /// <summary>Sync ile gelen mesafeyi uygular (kopma sonrası kaldığı yerden devam).</summary>
    public void SetSegmentDistance(int meters)
    {
        lock (_lock)
        {
            _segmentDistance = Math.Max(meters, 0);
            if (_lastMeterValue.HasValue)
                _meterReference = _lastMeterValue.Value - _segmentDistance;
            else
                _referencePending = true; // ilk metre değeri gelince _segmentDistance korunacak şekilde ayarlanır
        }
    }
}

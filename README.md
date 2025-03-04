# Metro YBS LogicManager

Metro YBS (Yolcu Bilgilendirme Sistemi) için geliştirilmiş genel amaçlı bir yapı olan LogicManager, tren rotalarının yönetimi, istasyon anonsları, LED ve LCD ekran kontrolü gibi işlemleri gerçekleştirir.

## İşlevsellik

LogicManager, aşağıdaki adımları takip ederek tren rotasını yönetir:

1.  **Kurulu Rota Bilgisi Alma:**
    * Bir API'den kurulu rota bilgilerini alır.
    * Eğer rota kurulu değilse, rota kurulana kadar bekler.
2.  **İstasyon Bilgilerini Okuma:**
    * Rota kurulduktan sonra, aşağıdaki JSON formatındaki gibi tüm istasyon bilgilerini okur:

    ```json
    {
        "istasyonId": "1",
        "istasyonAdi": "MESCIDI SELAM",
        "istasyonMesafeT1": "710",
        "istasyonMesafeT2": "450",
        "istasyonBoyT1": "100",
        "istasyonBoyT2": "100",
        "skipStationState": "False",
        "istasyonAnonsMesafesi": "200",
        "istasyonYaklasimAnonsMesafesi": "20",
        "istasyonBaslangıcAnonsu": "False",
        "istasyonAktarmaAnonsu": "250",
        "terminalAnonsu": "True",
        "kapıAcılısYönü": "True",
        "terminalAnonsu": "True"
    }
    ```

3.  **Tako Verisini Okuma ve Loglama:**
    * Tako (takometre) verisini okur ve her okuduğunda log sistemine yazar.
4.  **Başlangıç Anonsu:**
    * Rota kurulduktan sonra, eğer `istasyonBaslangıcAnonsu` değeri `True` ise, başlangıç anonsunu yapar.
    * Anons, `AnonsController` servisine `anons tipi` ve `istasyon ismi` parametreleri ile bir metot çağrısı yapılarak tetiklenir.
5.  **Gelecek İstasyon Anonsu:**
    * İlk istasyondan çıktıktan sonra, `istasyonAnonsMesafesi` değerine göre gelecek istasyon anonsunu yapar.
6.  **LED Kontrolü:**
    * LED servisine, iç veya dış LED ve istasyon ismini parametre olarak verdiği bir metot ile LED'leri kontrol eder.
7.  **LCD Ekran Kontrolü:**
    * LCD ekrana sonraki istasyon (`nextStation`) ve kalan mesafe bilgilerini gönderir.
8.  **Yaklaşım Anonsu:**
    * `istasyonYaklasimAnonsMesafesi` değerine göre yaklaşım anonsunu yapar.
9.  **İstasyona Varış:**
    * İstasyona varıldığında, eğer `ZeroSpeed` değeri 0 ise ve `AllDoorReleased` değeri `True` ise, takometre (`tako`) değerini sıfırlar.
10. **Sonraki İstasyon:**
    * Bir sonraki istasyona geçer.
11. **Rota Sonu:**
    * Eğer son istasyona gelindiyse, rotanın bittiğini bildirir.

Bu adımlar, son istasyona gelene kadar tekrarlanır.

## Bağımlılıklar

* API bağlantısı
* AnonsController servisi
* LED servisi
* LCD ekran arayüzü
* Log sistemi

## Kullanım

1.  Projeyi klonlayın.
2.  Bağımlılıkları yükleyin.
3.  Gerekli API bağlantılarını ve servis konfigürasyonlarını ayarlayın.
4.  Uygulamayı çalıştırın.

## Katkıda Bulunma

Katkılarınızı bekliyoruz! Lütfen pull request göndererek veya sorunları bildirerek projeye katkıda bulunun.

## Lisans

[Lisans Bilgisi]

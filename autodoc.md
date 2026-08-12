# Just Give Up Man! (JGUM) - Teknik Dokümantasyon

**Just Give Up Man! (JGUM)**, Mount & Blade II: Bannerlord için geliştirilmiş; savaş meydanlarındaki tarafların (Lordlar, Devriyeler), kuşatma altındaki kalelerin ve yapay zeka birliklerinin güç dengelerini, liderlik özelliklerini ve lojistik durumlarını analiz ederek teslim olma/kuşatma müzakeresi kararları vermesini sağlayan gelişmiş bir oynanış modudur. 

Bu dokümantasyon, projenin mimarisini, matematiksel hesaplama altyapısını, davranış sistemlerini ve harici mod geliştiricileri için sunulan genişleme (interop) yeteneklerini derinlemesine açıklamaktadır.

---

## 1. Mimari Genel Bakış

Proje iki ana katmandan oluşmaktadır:
1. **JGUM (Çekirdek Modül - Core Module):** Kampanya davranışlarını (Campaign Behaviors), matematiksel hesaplama motorlarını (Calculators), teslimiyet loglama sistemini ve harici olay yayınlama (Interop Events) altyapısını barındırır. Bannerlord'un temel API'leri dışında hiçbir mod bağımlılığı yoktur.
2. **JGUM.MCMBridge (Arayüz Köprüsü - Mod Configuration Menu Bridge):** Modun Bannerlord içi MCM arayüzü üzerinden dinamik olarak yapılandırılabilmesini sağlar. Çekirdek modülden tamamen soyutlanmıştır. Çekirdek modül, MCM modülünü çalışma zamanında (reflection yöntemiyle) *isteğe bağlı (optional)* olarak yükler. Eğer MCM yüklü değilse, mod hata vermeden varsayılan JSON yapılandırması üzerinden çalışmaya devam eder.

### Sistem Bileşenleri Şeması

```
[ Bannerlord Engine (Campaign System) ]
       │
       ▼
 ┌───────────┐         ┌──────────────────────────────┐
 │SubModule  │ ──────► │ JgumSettingsManager (Config) │
 └─────┬─────┘         └──────────────┬───────────────┘
       │                              ▲ (Dinamik Kayıt)
       ▼ (Ekleme)                     │
 ┌────────────────────────────────────┼───────────────┐
 │ CampaignBehaviors (Kampanya Dav.) │               │
 │  ├─ LordEncounterSurrender         │               │
 │  ├─ PatrolEncounterSurrender       │               │
 │  ├─ SiegeSurrender                 │               │
 │  ├─ SiegeNegotiationBehavior       │               │
 │  ├─ FiefPurchaseOfferBehavior      │               │
 │  ├─ VoluntarySurrenderBehavior     │               │
 │  ├─ AILordEncounterSurrender       │               │
 │  └─ AISiegeSurrender               │               │
 └─────┬──────────────────────────────┼───────────────┘
       │                              │
       ▼ (Matematiksel Analiz)        │
 ┌──────────────────────────────┐     │   ┌─────────────────────┐
 │ Calculators (Hesaplayıcılar) │     └── │   JGUM.MCMBridge    │
 │  ├─ LordSurrenderCalculator  │         │ (Dinamik Konfig.)   │
 │  ├─ SiegeSurrenderCalculator │         └─────────────────────┘
 │  ├─ SiegePressureCalculator  │
 │  └─ NegotiationCalculator    │
 └──────────────────────────────┘
```

---

## 2. Çalışma Zamanı Başlatma ve Köprü Tasarımı (Bootstrap)

Modun Bannerlord yaşam döngüsüne entegrasyonu `JGUMSubModule` üzerinden gerçekleştirilir:

- **`OnSubModuleLoad`**: Modül yüklendiğinde ilk olarak `TryActivateOptionalMcmBridge` fonksiyonu çalıştırılır. Bu fonksiyon, oyun dizininde `JGUM.MCMBridge.dll` dosyasını arar. Eğer MCM yüklüyse ve dosya mevcutsa, yansıtma (Reflection) yöntemleriyle `BridgeBootstrap.TryRegister` metodunu çağırarak MCM'deki ayarları çekirdek modun `JgumSettingsManager` sınıfına kaydeder.
- **`OnBeforeInitialModuleScreenSetAsRoot`**: Mod menü ekranına ulaşmadan hemen önce `JgumSettingsManager.Initialize` çağrılarak temel yapılandırma dosyası (`config.json`) okunur veya yoksa varsayılan değerlerle oluşturulur.
- **`OnGameStart`**: Eğer oyun tipi bir senaryo modu (Campaign) ise, tüm davranış sınıfları (Campaign Behaviors) oyun starter nesnesine eklenir.

---

## 3. Matematiksel Teslimiyet Modelleri (Calculators)

Teslimiyet mekanizması, rastgele olasılıklardan uzak, fiziksel ve psikolojik etkenlere dayanan matematiksel formüllere dayanır. Formüllerdeki en kritik bileşenler **Güç Oranı (Power Ratio)**, **Kuşatma Baskısı (Siege Pressure)** ve **Karakter Özellikleri Etkisi (Trait Effect)**'dir.

### 3.1 Lord ve Devriye Teslimiyet Algoritması (`LordSurrenderCalculator`)

Savaş öncesi karşılaşmalarda düşman liderinin teslim olup olmayacağını hesaplar.

#### Adım 1: Temel Güç Oranının Hesapunması
Güç oranı, oyuncu tarafının toplam askeri gücünün, düşman tarafının toplam askeri gücüne bölünmesiyle elde edilir:

$$\text{Power Ratio} = \frac{\text{Oyuncu Gücü}}{\text{Düşman Gücü}}$$

#### Adım 2: Karakter Özellikleri Etkisi (Trait Effect)
Düşman liderlerinin (veya liderler grubunun ortalama) Bannerlord karakter özellikleri (`DefaultTraits`) hesaba katılır. Liderlerin her birinin karakter seviyeleri `JgumSettingsManager` çarpanlarıyla ağırlıklandırılır:

$$\text{Trait Effect} = \text{Oyuncu Merhameti} + \sum \left( \text{Düşman Hesapçılığı} + \text{Düşman Merhameti} - \text{Düşman Cesareti} - \text{Düşman Onuru} \right)$$

*Detaylı Kapsam:*
- **Valor (Cesaret):** Yüksek cesaret, teslim olma eğilimini azaltır.
- **Honor (Onur):** Yüksek onur, teslim olma eğilimini azaltır.
- **Calculating (Hesapçılık):** Mantıklı kararlar almayı sağlar; ezici güç farkı varsa teslimiyeti kolaylaştırır.
- **Mercy (Merhamet):** Teslimiyeti rasyonel ve insancıl bir seçenek olarak görür.

#### Adım 3: Toplam Oran ve Karar Verme Durumu
$$\text{Total Ratio} = \text{Power Ratio} + \text{Trait Effect}$$

Elde edilen `Total Ratio` değeri belirlenmiş eşiklerle (`Threshold`) karşılaştırılır. Eşik değerleri, kullanıcı ayarlarındaki `SurrenderTendencyMultiplier` ile modifiye edilir:

$$\text{Base Threshold} = \frac{\text{Config.BaseSurrenderThreshold}}{\text{Config.SurrenderTendencyMultiplier}}$$

$$\text{Guaranteed Threshold} = \frac{\text{Config.GuaranteedSurrenderThreshold}}{\text{Config.SurrenderTendencyMultiplier}}$$

#### Rastgelelik Modları (RNG Modes)
1. **Off (RNG Yok):** `Total Ratio >= Base Threshold` ise düşman doğrudan teslim olur.
2. **Thresholded (Eşikli Rastgelelik):**
   - `Total Ratio >= Guaranteed Threshold` ise kesin teslimiyet (%100).
   - `Total Ratio < Base Threshold` ise kesin savaş (%0).
   - Aradaki değerler için olasılık çizgisel (lineer) olarak hesaplanır:
     $$P(\text{Surrender}) = \frac{\text{Total Ratio} - \text{Base Threshold}}{\text{Guaranteed Threshold} - \text{Base Threshold}}$$
3. **Unbound (Sınırsız Rastgelelik):** Kesin sınırlar yoktur. Çok yüksek güçlerde dahi ufak da olsa bir savaşma olasılığı (%5) veya çok düşük güçlerde dahi umutsuz bir teslim olma olasılığı vardır.

---

### 3.2 Kuşatma Baskısı Hesaplama Modeli (`SiegePressureCalculator`)

Kuşatma altındaki bir kalenin teslim olmaya ikna edilmesinde, tarafların inşa ettiği kuşatma silahları ve surların fiziksel durumu dinamik olarak baskı oluşturur.

#### 1. Kuşatma Silahı Baskısı (Engine Pressure)
İnşa edilen aktif kuşatma silahlarının (`SiegeEngineType`) ilerleme durumları (`Progress`) ve inşa edilip edilmedikleri kontrol edilerek bir ağırlık hesaplanır.

$$\text{Engine Weight} = \text{Base Weight} \times \text{ConfigMultiplier}$$

*   **Siege Tower / Heavy Siege Tower:** Savunmacı için temel ağırlık `0.16`, Saldırgan için `0.12`.
*   **Battering Ram / Improved Ram:** Savunmacı için temel ağırlık `0.18`, Saldırgan için `0.14`.
*   **Catapult / Ballista varyasyonları:** Savunmacı için temel ağırlık `0.14`, Saldırgan için `0.10`.
*   **Trebuchet varyasyonları:** Savunmacı için temel ağırlık `0.16`, Saldırgan için `0.12`.

Kuşatma silahlarının oluşturduğu toplam güç oranı farkı şu şekilde formüle edilir:

$$\text{Engine Power Ratio} = \frac{\text{Saldırgan Silah Baskısı} + 1.0}{\text{Savunmacı Silah Baskısı} + 1.0}$$

$$\text{Engine Pressure Effect} = (\text{Engine Power Ratio} - 1.0) \times \frac{\text{Config.SiegeEnginePowerRatioMultiplier}}{100}$$

#### 2. Duvar Hasarı Baskısı (Wall Damage Pressure)
Kalenin her bir duvar bölümünün can yüzdesi (`ratio`) analiz edilir:

$$\text{Wall Section Damage} = 1.0 - \text{HP Ratio}$$

Bu hasar değeri, hasarın şiddetini ölçekleyen `SiegeWallDamageCurveExponent` üssü ile çarpılır. Eğer bir duvar tamamen yıkıldıysa (`HP Ratio <= 0`), ekstra bir bonus hasar eklenir:

$$\text{Softened Damage Pressure} = \left( \frac{\sum (\text{Wall Section Damage})^{\text{Exponent}}}{\text{Duvar Bölümü Sayısı}} \right) \times \text{Hasar Baskı Çarpanı}$$

$$\text{Destroyed Wall Pressure} = \text{Yıkılan Duvar Sayısı} \times \text{Config.SiegeWallDestroyedBonus}$$

$$\text{Total Wall Pressure} = \text{Softened Damage Pressure} + \text{Destroyed Wall Pressure}$$

---

### 3.3 Kalelerin Teslimiyet Algoritması (`SiegeSurrenderCalculator`)

Bir kalenin yapay zeka tarafından teslim edilip edilmeyceği aşağıdaki şartlar zincirine bağlıdır:

1. **Gıda Kontrolü:** Kale gıda stoğuna sahip olmamalı (`FoodStocks <= 0`) ve kıtlık çekiyor olmalıdır (`IsStarving`). Gıdası olan kaleler asla teslim olmaz.
2. **Güç Hesaplama:** Kaleyi kuşatanların toplam gücü (`Attacker Power`) ile garnizon, milis ve kaledeki müttefik lordların toplam gücü (`Defender Power`) hesaplanır. Ayrıca kaleye yakın mesafede bulunan dost ve düşman lordların askeri güçleri de tespit edilerek algoritmaya dahil edilir.
3. **Yakındaki Lordların Etkisi:** Belirli bir tespit yarıçapındaki (`NearbyEnemyLordDetectionRange`) düşman lordların güçleri, savunmacıların psikolojik durumunu güçlendirmek için belirlenen bir yüzdeyle (`NearbyEnemyLordStrengthPercentage`) kaledeki savunma gücüne eklenir:
   $$\text{Effective Defender Power} = \text{Defender Power} + \left( \text{Yakındaki Düşman Lord Gücü} \times \text{Katsayı} \right)$$
4. **Formül Birleşimi:**
   $$\text{Total Siege Ratio} = \frac{\text{Attacker Power}}{\text{Effective Defender Power}} + \text{Total Wall Pressure} + \text{Engine Pressure Effect} - (\text{Kaledeki Lord Sayısı} \times 0.1) + \text{Trait Effect}$$

Elde edilen `Total Siege Ratio`, seçilen RNG moduna göre `Base Threshold` and `Guaranteed Threshold` değerlerine tabi tutularak nihai teslimiyet kararını üretir.

---

## 4. Kampanya Davranışları ve Mod Sistemleri (Systems)

### 4.1 Kuşatma Müzakereleri Sistemi (`SiegeNegotiationBehavior`)

Kuşatmayı gerçekleştiren oyuncu, kaleyi saldırmadan teslim almaya ikna etmek için **Persuasion (İkna Etme)** tabanlı diplomatik bir parley (müzakere) başlatabilir.

#### Müzakere Başlatma Koşulları
- Oyuncu kuşatan taraf olmalıdır.
- Kaleye gönderilen elçinin cevabı bekleniyor olmamalıdır (Müzakere talebi gönderildikten `1 saat` sonra döner).
- Kuşatılan kale için son parley başarısızlığından sonra `72 saat` geçmelidir.
- Oyuncunun askeri güç oranı kaleyi teslim almaya yetecek bir asgari sınıra (Güç Oranı >= 1.0) ulaşmalıdır.

#### Müzakere Mekaniği ve Persuasion Task Yapısı
Müzakere başlatıldığında, kalenin tipi (Şehirler için 4 raunt, Kaleler için 3 raunt) ve güç oranı analiz edilerek gerekli başarı puanları belirlenir. Oyuncunun önüne her rauntta 3 farklı argüman seçeneği gelir. Bu argümanlar `PersuasionRoundLinePools` listesinden dinamik olarak seçilir:

*   **Gereksinimler ve Kilitler (Locks):**
    *   **Karakter Kilidi (Trait Lock):** Eğer oyuncunun ilgili karakter özelliği (örneğin *Honor* veya *Mercy*) negatif seviyedeyse, o argüman kilitlenir ve seçilemez. Örneğin: *"A person of your reputation cannot speak of honor."*
    *   **Koşul Kilidi (Condition Lock):** Duvar hasarı gerektiren argümanlar (örneğin *Engineering* temelli seçenekler), kalenin en az bir duvarının canı %50 veya altına inmediği sürece kilitlidir.

*   **Başarısızlık Durumunda Aşınma (Decay):** Müzakere başarısızlıkla sonuçlanırsa, oyuncunun o ana kadar elde ettiği başarılı raunt sayısı `1` düşürülerek kaydedilir. Oyuncu tekrar müzakere başlattığında (72 saat sonra), kaldığı yerden (aşınmış puanla) devam eder.

---

### 4.2 Fief Satın Alma Teklifi (`FiefPurchaseOfferBehavior`)

Eğer oyuncu bir kalede savunma konumundaysa ve kaledeki kuşatmacı liderin **Trade (Ticaret)** yeteneği yüksekse (`MinTradeSkillForFiefOffer`, Varsayılan: 100), kuşatmacı kaleyi parayla satın almak isteyebilir.

#### Fiyat Teklifi Formülü
Satın alma fiyatı kalenin refahı, askeri güç dengesi ve kuşatma süresine göre dinamik olarak hesaplanır:

$$\text{Base Price} = \text{Kalenin Refahı} \times 10.0$$

$$\text{Power Discount} = \max\left( \frac{\text{Savunma Gücü}}{\text{Saldırı Gücü}}, 0.1 \right)$$

$$\text{Time Bonus} = 1.0 + (\text{Geçen Kuşatma Günü} \times 0.05)$$

$$\text{Final Price} = \text{Base Price} \times \text{Power Discount} \times \text{Time Bonus} \times \text{Config.FiefPriceOfferMultiplier}$$

Eğer oyuncu teklifi kabul ederse, altın oyuncunun envanterine geçer, kalenin mülkiyeti kuşatmacı lorda aktarılır ve kuşatma kansız bir şekilde sona erer.

---

### 4.3 Gönüllü Teslimiyet Teklifi (`VoluntarySurrenderBehavior`)

Oyuncu, kuşatılan kalesini daha fazla askeri kayıp vermeden düşmana gönüllü olarak teslim etmeyi teklif edebilir. 

#### Koşullar ve Kısıtlamalar
Gönüllü teslimiyet (yerleşimi terk etme) teklifinin yapılabilmesi için aşağıdaki koşulların her ikisinin de sağlanması gerekir (kod seviyesinde `IsPlayerDefender` fonksiyonu ile doğrulanır):
1. Kuşatılan yerleşim doğrudan oyuncunun klanına ait olmalıdır (`settlement.OwnerClan == Hero.MainHero.Clan`).
2. Oyuncunun ana birliği, o esnada yerleşimdeki aktif savunma tarafında yer almalıdır (`settlement.SiegeEvent.GetSiegeEventSide(BattleSideEnum.Defender).HasInvolvedPartyForEventType(playerParty)`).

#### Teklif Mekaniği ve Karakter Özellikleri Etkisi
- Kuşatmacı liderin **Mercy (Merhamet)** özelliğine bağlı olarak teklif kabul edilir veya reddedilir:
  $$\text{Acceptance Chance} = \text{Clamp}(100 + (\text{Kuşatmacı Merhamet Seviyesi} \times 30), 0, 100)$$
- **Teklif Kabul Edilirse:** Kaledeki garnizonun canı bağışlanır, mülkiyet teslim edilir. Ancak, bu kansız ama onur kırıcı teslimiyet oyuncunun karakter özelliklerini dinamik olarak etkiler:
  - **Onur (Honor) ve Cesaret (Valor) Azalması:** Oyuncunun Onur ve Cesaret özellikleri yapılandırılan değerlere göre ciddi oranda düşer (`VoluntarySurrenderHonorPenalty` ve `VoluntarySurrenderValorPenalty`).
  - **Merhamet (Mercy) Artışı:** Garnizonun canını kurtardığı için oyuncu Merhamet ödülü alır (`VoluntarySurrenderMercyReward`, Varsayılan: 50).
  - **Hesapçılık (Calculating) Artışı:** Ezici bir kuşatma karşısında mantıklı ve rasyonel bir karar verdiği için oyuncu Hesapçılık ödülü alır (`VoluntarySurrenderCalculatingReward`, Varsayılan: 30).
- **Teklif Reddedilirse:** Kuşatma devam eder. Her 3 reddetmede bir kuşatmacı liderin Onur ve Merhamet seviyeleri düşer (kuşatmacı gaddarlaşır).

---

### 4.4 Yapay Zeka Arası Otomasyon (AI vs AI)

Mod, dünyadaki yapay zeka klanlarının kendi aralarındaki savaşları da simüle eder:
- **`AILordEncounterSurrenderBehavior`:** Yapay zeka orduları meydan savaşlarında karşılaştığında, güç dengesi ezici ise zayıf olan ordu doğrudan harita ekranında teslim olur.
- **`AISiegeSurrenderBehavior`:** Kuşatma altındaki yapay zeka kaleleri, ezici güç farkı ve açlık durumunda doğrudan kuşatmacı yapay zeka ordusuna teslim olur. Harita genelinde aşırı hızlı toprak değişimini (snowballing) engellemek amacıyla günlük teslim olan kale sayısı `AiVsAiSiegeDailySurrenderLimit` (Varsayılan: 2) ile sınırlandırılmıştır.

---

## 5. Veri Kalıcılığı ve Kayıt Güvenliği (`JGUMSaveDefiner`)

Mod, Bannerlord save dosyalarının bozulmasını (save corruption) engellemek için kendi `SaveableTypeDefiner` sınıfını tanımlar.

- **`JGUMSaveDefiner` ID:** `987650` (Çakışmaları önlemek için benzersiz bir ID).
- **Kayıt Edilen Sınıflar:** `AiSurrenderLogEntry` (Yapay zekanın teslimiyet günlük girdileri).
- **Konteyner Tanımları:** `Dictionary<string, CampaignTime>` (Müzakere ve teslimiyet bekleme süreleri), `Dictionary<string, int>` (Müzakere başarı rauntları).

---

## 6. Genişletilebilirlik ve Telemetri (Interop API)

JGUM, diğer Bannerlord mod geliştiricilerinin teslimiyet olaylarını dinleyebilmesi ve telemetri verisi toplayabilmesi için gelişmiş bir olay tabanlı Interop API sunar.

### `JgumInteropEvents` Sınıfı

Geliştiriciler, modlarındaki kodlardan `JgumInteropEvents.SurrenderResolved` olayına abone olabilirler. Teslimiyet türü ne olursa olsun (Lord karşılaşması, devriye karşılaşması, kuşatma otomasyonu, müzakere sonucu teslimiyet) bu olay tetiklenir.

```csharp
namespace JGUM.Interop
{
    public enum JgumSurrenderKind
    {
        LordEncounter,
        PatrolEncounter,
        SiegeAutoSurrender,
        SiegeNegotiatedSurrender
    }

    public enum JgumSurrenderOutcome
    {
        Accepted,
        Rejected
    }

    public sealed class JgumSurrenderRecord
    {
        public JgumSurrenderKind Kind { get; set; }
        public string? SettlementId { get; set; }
        public string? SurrenderingHeroId { get; set; }
        public string? SurrenderingPartyName { get; set; }
        public string? WinnerHeroId { get; set; }
        public string? WinnerClanId { get; set; }
        public string? LoserFactionId { get; set; }
        public float CampaignTimeDays { get; set; }
        public bool AcceptedByPlayer { get; set; }
        public JgumSurrenderOutcome Outcome { get; set; }
    }

    public static class JgumInteropEvents
    {
        public static event Action<JgumSurrenderRecord>? SurrenderResolved;
    }
}
```

#### Örnek Entegrasyon Kodu

Harici bir mod içinde JGUM teslimiyet olaylarını dinlemek için şu yapıyı kullanabilirsiniz:

```csharp
using JGUM.Interop;

public class MyExternalModSystem
{
    public void OnInitialize()
    {
        // JGUM olayına abone ol
        JgumInteropEvents.SurrenderResolved += OnSurrenderResolved;
    }

    private void OnSurrenderResolved(JgumSurrenderRecord record)
    {
        if (record.Outcome == JgumSurrenderOutcome.Accepted)
        {
            FileLog.Log($"Kahraman {record.SurrenderingHeroId}, {record.WinnerHeroId} karşısında teslim oldu!");
        }
    }
}
```

---

## 7. Geliştirici Konsol Komutları

Çalışma zamanında konfigürasyon yönetimini kolaylaştırmak için aşağıdaki konsol komutu tanımlanmıştır:

- **`jgum.reload_config`**: Oyunu kapatıp açmaya gerek kalmadan `config.json` dosyasındaki ayarları yeniden yükler ve güncel değerleri anında oyuna yansıtır.
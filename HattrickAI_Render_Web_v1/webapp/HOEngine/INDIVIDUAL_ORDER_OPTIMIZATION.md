# HattrickAI — Individual Order Optimization Design

## Amaç

Oyuncunun tek başına rating'ine bakıp `Ofansif` veya `Defansif` seçmek yerine, bireysel talimatı bütün XI'nin rating dengesi üzerinden seçmek.

Karar sırası:

1. Oyuncuyu önce doğal pozisyonuna göre seç.
2. Varsayılan referans olarak `Normal` talimatı hesapla.
3. Pozisyon için Hattrick'te gerçekten mevcut talimatları üret.
4. Her aday talimatı takımın tamamına uygula.
5. Midfield, sol/merkez/sağ savunma ve sol/merkez/sağ hücum ratinglerini yeniden hesapla.
6. Orta saha kaybı, savunma kaybı ve hücum kazancını birlikte değerlendir.
7. `Towards Wing` yalnızca oyuncunun Winger + Passing profili bunu hak ediyorsa güçlü aday olsun.
8. Kanada doğru kararında hangi tarafın güçlendiği ayrıca korunmalı; merkez katkısındaki kayıp göz ardı edilmemeli.
9. Rakip ratingleri mevcut olduğunda rakibin zayıf savunma tarafı ve güçlü hücum tarafı skora dahil edilecek.
10. Eşit veya çok yakın sonuçlarda `Normal` tercih edilecek.

## Hattrick kurallarından çıkarılan temel prensipler

- Inner midfielder için ana beceriler Playmaking ve Passing'dir; Defending, Stamina ve Scoring de önemlidir.
- Midfield maçta üretilen fırsat sayısını belirleyen ana sektördür.
- IM Offensive: daha fazla Passing/merkezi hücum, daha az Defending ve bir miktar daha az Playmaking.
- IM Defensive: daha fazla Defending, daha az Passing ve bir miktar daha az Playmaking.
- IM Towards Wing: Winger ve yan Passing katkısını artırır; midfield ve merkezi katkıyı azaltır.
- İki veya üç IM aynı merkezi sektörde olduğunda contribution loss uygulanır. Bu nedenle bir oyuncunun talimatı diğer oyuncuların etkisini de dolaylı olarak değiştirir.
- Individual order oyuncunun pozisyonunu değiştirmez; formasyonu değiştiren işlem repositioning'dir.

Kaynaklar:
- Hattrick Manual: https://wiki.hattrick.org/wiki/Manual
- Hattrick Individual Orders: https://wiki.hattrick.org/wiki/Individual_order
- Hattrick Inner Midfielder: https://wiki.hattrick.org/wiki/Inner_midfielder
- Hattrick Match Order: https://wiki.hattrick.org/wiki/Match_order
- Hattrick Contribution: https://wiki.hattrick.org/wiki/Contribution

## Neden Normal varsayılan?

`Normal`, standart pozisyon katkısını korur. Bu yüzden motor önce Normal ile referans rating üretir. Bir özel talimat ancak toplam takım skorunu anlamlı şekilde iyileştiriyorsa seçilir. Çok küçük farklarda Normal korunur.

## Takım bazlı optimizasyon

Eski yaklaşımın problemi:

`Oyuncunun hücum ratingi yüksek -> Ofansif`

Yeni yaklaşım:

`Aday talimat -> bütün XI ratingi -> toplam değer -> güvenlik/balance kontrolleri -> karar`

Böylece örneğin üç IM'nin tamamının Ofansif olması sadece hücumu yükselttiği için seçilemez; bunun karşılığında kaybedilen savunma ve midfield de skora girer.

## Kanada doğru kararının mantığı

Bir IM'nin Winger becerisi yüksek olması tek başına yeterli değildir. Adayın:

- Winger
- Passing
- Playmaking
- Defending
- Stamina

profiline bakılır. Winger + Passing profili belirgin şekilde iyiyse Towards Wing adayının değeri yükselir. Ancak aynı oyuncunun midfield/defence kaybı ayrıca hesaplanır.

Rakip verisi geldiğinde:

`Bizim yan hücum kazancı + rakibin o taraftaki savunma zayıflığı - bizim merkez midfield kaybımız - savunma kaybımız`

birlikte değerlendirilir.

## Sol / sağ konusu

Hattrick'teki IM Towards Wing gerçek bir yan hücum emri olduğundan, uygulama tarafında `hangi taraf` bilgisinin kaybolmaması gerekir. Mevcut rating tablosunda merkezi IM'nin normal katkısı iki yana bölündüğü için optimizer bunu doğrudan merkez kaybı ile yan katkı takası olarak değerlendirir. Rakip ratingleri bağlandığında sol ve sağ ayrı ayrı optimize edilecek.

## Güvenlik sınırları

Bir rating artışı uğruna başka sektörü anlamsız biçimde çökertmek yasaktır. Özellikle:

- Güçlü rakibe karşı gereksiz savunma kaybı
- Orta saha dengeliyken gereksiz midfield kaybı
- Zaten güçlü olan bir kanadı daha da güçlendirirken diğer tarafı boş bırakmak

cezalandırılacak.

## Arama yöntemi

Tek oyunculu greedy/coordinate-ascent yerine beam search kullanılır. Her slot için bütün geçerli bireysel talimatlar denenir; takım ratingi ile en iyi ara durumlar tutulur. Böylece `A ofansif seçildiği için B'nin en iyi kararı değişti` gibi etkileşimler hesaba katılır.

Bu özellikle 3-4-3 ve 3-5-2 gibi birden fazla orta saha oyuncusunun bulunduğu formasyonlarda önemlidir.

## Gelecek rakip entegrasyonu

`TeamMatchContext` içine rakip ratingleri bağlandığında optimizer ayrıca:

- rakibin en zayıf savunma sektörüne saldırı bonusu,
- rakibin en güçlü hücum sektörüne karşı savunma koruması,
- sol/sağ hücum dengesizliğini azaltma,
- midfield üstünlüğünün kaybedilmemesi

gibi karşılaştırmalı faktörleri kullanacak.

Bu katman oyuncu seçimi ile bireysel talimat seçimini birbirinden ayırır.

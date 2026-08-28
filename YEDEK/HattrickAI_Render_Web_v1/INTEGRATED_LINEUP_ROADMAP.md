# Entegre Kadro Planlama Yol Haritası

## Kullanıcıdan alınacak girdiler

1. Lig maçı seçimi.
2. Lig rakibinin baz alınacak geçmiş maçı.
3. Lig formasyonu.
4. Kupa maçı seçimi.
5. Kupa rakibinin baz alınacak geçmiş maçı.
6. Kupa formasyonu.
7. Aktif antrenmanın onayı.

## Hesaplama sırası

### 1. Lig kadrosu
- Seçilen lig rakibinin seçilen geçmiş maç ratingleri kullanılır.
- Seçilen lig formasyonu sabitlenir.
- En iyi 11 bu formasyona yerleştirilir.
- Bireysel davranışlar/taktik tercihleri HO Engine tarafından seçilir.
- Oyuncu seçim skoru yalnızca motor içinde kullanılır.
- Kart üzerinde oyuncunun gerçek pozisyon gücünden türetilen 0–10 rating gösterilir.

### 2. Kupa kadrosu
- Önce seçilen kupa formasyonunun slotları oluşturulur.
- Aktif antrenmanın hangi mevkileri kapsadığı çıkarılır.
- Antrenman önceliği yüksek oyuncular uygun antrenman slotlarına yerleştirilir.
- Kupa rakibinin seçilen geçmiş maç ratingleri seçimde yardımcı sinyal olarak kullanılır.
- Antrenman almayan slotlar mümkün olduğunca tamamlanmış lig 11'indeki oyunculardan doldurulur.
- Lig 11'indeki bir oyuncu antrenman slotuna taşınırsa aynı oyuncu ikinci kez kullanılamaz.
- Oyuncu seçim puanı kartta gösterilmez.
- Kupa kartlarında da lig kartıyla aynı oyuncu rating ölçeği kullanılır.

## Sonuç

Tek bir plan nesnesi içinde lig ve kupa kadrosu, seçilen rakip geçmişleri, formasyonlar ve antrenman bilgisi tutulur. Kullanıcı Lig Kadrosu/Kupa Kadrosu arasında geçiş yaptığında yeniden seçim yapılmadan aynı hesaplanmış plan gösterilir.

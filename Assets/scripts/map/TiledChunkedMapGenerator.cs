using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Segment (chunk) bazlı, DETERMİNİSTİK map üreticisi.
/// - Her segment kendi index'ine göre seed'lenir => aynı index her zaman aynı layout'u üretir.
///   Bu sayede oyuncu geri dönüp temizlenmiş bir chunk'a geldiğinde birebir aynı parkur yeniden üretilir.
/// - Zemin ARTIK düz değil: her sütun için bir yüzey yüksekliği (heightmap) hesaplanır. Böylece
///   rastgele YÜKSELTİLER (tepeler) ve BOŞLUKLAR (çukurlar) oluşur.
/// - Yüzeydeki her dikey adım oyuncunun zıplama menzilinden (reachUp) küçük tutulur => hiçbir yerde
///   tırmanılamayacak bir duvar oluşmaz, oyuncu takılıp kalamaz.
/// - Yüzeyin altı DAİMA katı doldurulur => void'e (sonsuz düşüşe) düşmek imkânsız. "Boşluklar" tabanı
///   olan çukurlardır; içine düşülse bile duvar yüksekliği reachUp'tan küçük olduğundan geri çıkılır.
/// - Segment sınırları ortak deterministik anchor'lara bağlanır => sınırlarda ani uçurum olmaz.
/// - Havada süzülen platformlar YEREL zemin yüzeyinin en az 'platformClearanceTiles' kadar üstüne
///   yerleştirilir => tepelerle çakışıp duvar oluşturmaz, oyuncunun yolu asla kesilmez.
/// - Yükseklik/boşluk sınırları oyuncunun gerçek zıplama fiziğinden otomatik hesaplanır.
/// </summary>
public class TiledChunkedMapGenerator : MonoBehaviour
{
    public struct SegmentResult
    {
        public BoundsInt bounds; // Bu segmentin temizleyip yazdığı tam tile dikdörtgeni (cleanup için).
    }

    [Header("Map Generator")]
    [SerializeField] private Tilemap targetTilemap;
    [Tooltip("Normal Tile veya Rule Tile atanabilir (ikisi de TileBase'den türer).")]
    [SerializeField] private TileBase groundTile;

    [Header("Dünya")]
    [Tooltip("Aynı seed => aynı dünya. Değiştirince tüm parkur değişir.")]
    [SerializeField] private int worldSeed = 12345;
    [Tooltip("Segment temizlerken kullanılacak dikey alan (tile).")]
    [SerializeField] private int height = 30;
    [Tooltip("Zemin taban satırının (en alçak yüzey) world tile Y'si.")]
    [SerializeField] private int groundY = -2;
    [Tooltip("Zemin gövdesinin kaç satır kalın olacağı (collider/görsel + çukur tabanı garantisi).")]
    [SerializeField] private int groundThickness = 3;
    [Tooltip("Açık (true) iken çukurların (boşlukların) tabanı katı kalır => void'e düşülmez. " +
             "Kapatılırsa boşluk üretimi de tamamen devre dışı kalır (tümüyle katı zemin).")]
    [SerializeField] private bool solidGroundFloor = true;

    [Header("Zemin Yükseltileri (Tepeler)")]
    [Tooltip("Zemin yüzeyinin taban (groundY) üzerinden en fazla kaç tile yükselebileceği. " +
             "Oyuncunun zıplama menzilini (reachUp) aşamaz, otomatik kısılır.")]
    [SerializeField] private int maxTerrainRise = 5;
    [Tooltip("Komşu iki sütun arasında yüzeyin en fazla kaç tile değişebileceği. " +
             "reachUp'ı aşamaz => her adım tırmanılabilir kalır.")]
    [SerializeField] private int terrainStepMaxTiles = 2;
    [Tooltip("Yüzeyin aynı yükseklikte kalacağı EN AZ sütun sayısı (plato genişliği). " +
             "Tek-tile genişliğinde tepe/çukur oluşmasını (uygun köşe sprite'ı olmayan) engeller. " +
             "En az 2 => her yükselti/iniş en az 2 tile geniş olur.")]
    [SerializeField] private int terrainMinRunTiles = 2;

    [Header("Boşluklar (Çukurlar)")]
    [Tooltip("Zeminde atlanabilir çukurlar (boşluklar) oluşturulsun mu?")]
    [SerializeField] private bool enableGaps = true;
    [Tooltip("Çukurun kenar zemininden ne kadar aşağı ineceği (tile). reachUp'ı aşamaz => " +
             "içine düşülse bile geri tırmanılır.")]
    [SerializeField] private int gapDepthTiles = 4;
    [Tooltip("Bir çukurun görsel genişlik üst sınırı (tile). Ayrıca zıplama menzili maxGapTiles'ı da aşamaz.")]
    [SerializeField] private int gapMaxWidthCap = 5;
    [Tooltip("İki çukur arasında bırakılacak en az düz zemin (tile).")]
    [SerializeField] private int gapMinSpacingTiles = 8;
    [Tooltip("Dünya orijini (x=0) etrafında bu yarıçap kadar çukur açılmaz => oyuncu boşlukta doğmaz.")]
    [SerializeField] private int spawnSafeRadiusTiles = 6;

    [Header("Platformlar (havada süzülen)")]
    [SerializeField] private bool enableFloatingPlatforms = true;
    [SerializeField] private int minGapTiles = 1;
    [SerializeField] private int maxGapTiles = 3;
    [SerializeField] private int platformMinRunTiles = 3;
    [SerializeField] private int platformMaxRunTiles = 6;
    [Tooltip("Platformun ALT yüzeyi ile yerel zemin yüzeyi arasında bırakılacak en az boşluk (tile). " +
             "Bu sayede platform tepelerle çakışıp duvar oluşturamaz ve oyuncu altından geçebilir.")]
    [SerializeField] private int platformClearanceTiles = 3;
    [Tooltip("Platformların görsel kalınlığı (tile). Rule Tile üst/yan/alt kenar parçalarını doğru " +
             "seçebilsin diye en az 2 önerilir.")]
    [SerializeField] private int platformThickness = 2;

    [Header("Reachability (oyuncu fiziğinden otomatik)")]
    [Tooltip("Boşluk/yükseklik bu oyuncunun hareket değerlerinden hesaplanır. Boşsa elle ayarlanan değerler kullanılır.")]
    [SerializeField] private playermovement playerMovement;
    [SerializeField] private bool autoComputeReachability = true;
    [SerializeField, Range(0.5f, 1f)] private float gapSafetyMargin = 0.75f;
    [SerializeField, Range(0.5f, 1f)] private float stepUpSafetyMargin = 0.85f;
    [SerializeField] private bool logReachabilityOnAwake = true;

    // Oyuncunun tek zıplamada çıkabileceği maksimum tile yüksekliği (fizikten hesaplanır).
    // Tüm dikey sınırlar (tepe adımı, çukur derinliği, platform yüksekliği) bunun altında tutulur.
    private int reachUpTiles = 4;

    // Oyuncuyu başlangıçta zemine oturtmak için: segment index -> (segmentin sol X'i, sütun başına dolu-üst Y).
    private readonly Dictionary<int, SurfaceCache> segmentSurfaces = new Dictionary<int, SurfaceCache>();

    private struct SurfaceCache
    {
        public int startXTile;
        public int[] topY; // her sütunun en üst KATI hücresinin Y'si (çukurda: çukur tabanı).
    }

    private void Awake()
    {
        if (targetTilemap == null)
            targetTilemap = GetComponentInChildren<Tilemap>();

        RecomputeReachabilityFromPlayer();
        ClampDesignParams();
    }

    private void RecomputeReachabilityFromPlayer()
    {
        if (!autoComputeReachability || playerMovement == null)
            return;

        float v = playerMovement.JumpVelocity;
        float g = playerMovement.EffectiveGravity;
        float s = playerMovement.MoveSpeed;
        if (g <= 0.0001f)
            return; // sıfıra bölme koruması; Inspector değerlerini koru.

        // 1 world unit = 1 tile varsayımı (Grid Cell Size (1,1,1) olmalı).
        float apexHeightTiles = (v * v) / (2f * g);   // maksimum zıplama yüksekliği
        float totalAirTime = 2f * v / g;              // toplam havada kalma süresi
        float flatRangeTiles = s * totalAirTime;      // düz zeminde maksimum atlama menzili

        reachUpTiles = Mathf.Max(1, Mathf.FloorToInt(apexHeightTiles * stepUpSafetyMargin));
        maxGapTiles = Mathf.Max(1, Mathf.FloorToInt(flatRangeTiles * gapSafetyMargin));
        minGapTiles = Mathf.Clamp(minGapTiles, 1, maxGapTiles);

        if (logReachabilityOnAwake)
        {
            Debug.Log($"[Map] Reachability: v={v:F2} g={g:F2} s={s:F2} " +
                      $"apex={apexHeightTiles:F1} range={flatRangeTiles:F1} " +
                      $"=> reachUp={reachUpTiles} maxGap={maxGapTiles}");
        }
    }

    /// <summary>Tüm tasarım parametrelerini reachUp/menzil/height sınırlarına göre güvenli aralığa çeker.</summary>
    private void ClampDesignParams()
    {
        reachUpTiles = Mathf.Max(1, reachUpTiles);

        // Tepeler: hem tırmanılabilir (<= reachUp) hem de temizleme alanına sığacak kadar (< height).
        maxTerrainRise = Mathf.Clamp(maxTerrainRise, 0, Mathf.Min(reachUpTiles, Mathf.Max(0, height - 4)));
        terrainStepMaxTiles = Mathf.Clamp(terrainStepMaxTiles, 1, reachUpTiles);
        terrainMinRunTiles = Mathf.Max(1, terrainMinRunTiles);

        // Çukurlar: derinlik reachUp'ı, genişlik ise atlama menzilini (maxGap) aşamaz.
        gapDepthTiles = Mathf.Clamp(gapDepthTiles, 1, reachUpTiles);
        gapMaxWidthCap = Mathf.Clamp(gapMaxWidthCap, 1, Mathf.Max(1, maxGapTiles));
        gapMinSpacingTiles = Mathf.Max(2, gapMinSpacingTiles);
        spawnSafeRadiusTiles = Mathf.Max(0, spawnSafeRadiusTiles);

        // Platformlar: taban boşluğu ve kalınlık makul kalsın.
        platformThickness = Mathf.Max(1, platformThickness);
        platformMinRunTiles = Mathf.Max(1, platformMinRunTiles);
        platformMaxRunTiles = Mathf.Max(platformMinRunTiles, platformMaxRunTiles);

        // KRİTİK: Platform ancak "clr + (thickness-1) <= reachUp" ise üretilebilir. Aksi halde
        // BuildFloatingPlatforms'daki (maxTopY >= minTopY) koşulu her sütunda false döner ve
        // HİÇBİR platform yazılmaz (platformlar "kaybolur"). Bu yüzden kalınlık ve clearance'ı
        // oyuncunun gerçek zıplama menziline (reachUp) göre güvenli aralığa çekiyoruz.
        platformThickness = Mathf.Clamp(platformThickness, 1, reachUpTiles);
        int maxClearance = reachUpTiles - (platformThickness - 1); // en az 1 olacak şekilde
        platformClearanceTiles = Mathf.Clamp(platformClearanceTiles, 1, Mathf.Max(1, maxClearance));

        // Platform aralıkları da tutarlı olsun (min <= max) — autoCompute kapalı olsa bile geçerli.
        minGapTiles = Mathf.Max(1, minGapTiles);
        maxGapTiles = Mathf.Max(minGapTiles, maxGapTiles);
    }

    /// <summary>Zemin yüzeyinin dünya-Y'si (geriye dönük uyum; düz taban varsayımı).</summary>
    public float GroundSurfaceWorldY() => GroundSurfaceWorldYAt(0f);

    /// <summary>Verilen dünya-X'indeki zemin YÜZEYİNİN dünya-Y'si (oyuncuyu doğru tepeye/çukura oturtmak için).</summary>
    public float GroundSurfaceWorldYAt(float worldX)
    {
        int xTile = Mathf.FloorToInt(worldX);
        int topCell = SurfaceCellYAt(xTile);
        // topCell hücresinin ÜST yüzeyi = (topCell+1) hücresinin taban dünya-Y'si.
        if (targetTilemap != null)
            return targetTilemap.CellToWorld(new Vector3Int(0, topCell + 1, 0)).y;
        return topCell + 1f;
    }

    /// <summary>Verilen tile-X'inde en üstteki katı hücrenin Y'si. Üretilmemişse groundY (düz taban) döner.</summary>
    private int SurfaceCellYAt(int xTile)
    {
        foreach (var kvp in segmentSurfaces)
        {
            SurfaceCache sc = kvp.Value;
            int local = xTile - sc.startXTile;
            if (local >= 0 && local < sc.topY.Length)
                return sc.topY[local];
        }
        return groundY;
    }

    /// <summary>Segment sınır yüksekliği (anchor) — Perlin ile deterministik; komşu segmentlerle ortak.</summary>
    private int TerrainAnchorY(int segIndex)
    {
        float n = Mathf.PerlinNoise((segIndex + worldSeed * 0.137f) * 0.35f, 3.14f);
        int rise = Mathf.RoundToInt(n * Mathf.Max(0, maxTerrainRise));
        return groundY + Mathf.Clamp(rise, 0, Mathf.Max(0, maxTerrainRise));
    }

    /// <summary>
    /// Belirtilen index'teki segmenti üretir. Deterministik: aynı index => aynı sonuç.
    /// </summary>
    public SegmentResult GenerateSegment(int segmentIndex, int startXTile, int widthTiles, int clearRectPad = 2)
    {
        var result = new SegmentResult();
        if (targetTilemap == null || groundTile == null || widthTiles <= 0)
            return result;

        int segXMin = startXTile;
        int segXMax = startXTile + widthTiles - 1;
        int floorBottom = groundY - Mathf.Max(1, groundThickness) + 1;
        int terrainMinY = groundY;
        int terrainMaxY = groundY + Mathf.Max(0, maxTerrainRise);

        int yMin = Mathf.Min(groundY - clearRectPad, floorBottom - clearRectPad);
        int yMax = terrainMaxY + reachUpTiles + clearRectPad; // tepeler + üstündeki platformlar sığsın
        yMax = Mathf.Min(yMax, groundY + height + clearRectPad);

        BoundsInt clearBounds = new BoundsInt(segXMin, yMin, 0, widthTiles, yMax - yMin + 1, 1);
        result.bounds = clearBounds;

        // Tek bir blok: null = boş. Terrain + platformları buraya işleyip TEK SetTilesBlock ile yazacağız
        // (hem eski tile'ları temizler hem yenisini basar; per-cell'den çok daha hızlı).
        TileBase[] block = new TileBase[clearBounds.size.x * clearBounds.size.y];

        // --- Deterministik RNG: global state'i bozmadan segment'e özel seed kullan ---
        Random.State prevState = Random.state;
        Random.InitState(unchecked(worldSeed * 73856093 ^ segmentIndex * 19349663));

        // 1) Yüzey yükseklik haritası (tepeler) — sınırlar anchor'lara bağlı, her adım <= step.
        int[] surface = BuildSurface(segmentIndex, widthTiles, terrainMinY, terrainMaxY);

        // 2) Boşluklar (çukurlar) — atlanabilir genişlik, tırmanılabilir derinlik.
        bool[] gapCol;
        int[] pitTopY;
        BuildGaps(segXMin, widthTiles, surface, floorBottom, out gapCol, out pitTopY);

        // 3) Katı zemini doldur (yüzeyden -> floorBottom). Çukur sütunlarında yüzey = çukur tabanı.
        int[] fillTopY = new int[widthTiles];
        for (int i = 0; i < widthTiles; i++)
        {
            int wx = segXMin + i;
            int top = gapCol[i] ? pitTopY[i] : surface[i];
            fillTopY[i] = top;
            for (int y = top; y >= floorBottom; y--)
                SetBlock(block, clearBounds, wx, y, groundTile);
        }

        // 4) Havada süzülen platformlar — yerel zemin yüzeyinin GÜVENLİ boşluk kadar üstünde.
        if (enableFloatingPlatforms)
            BuildFloatingPlatforms(segXMin, segXMax, surface, block, clearBounds, yMax);

        Random.state = prevState;

        // Tek yazım.
        targetTilemap.SetTilesBlock(clearBounds, block);

        // Oyuncuyu doğru yüzeye oturtabilmek için sütun üst-Y'lerini önbelleğe al.
        segmentSurfaces[segmentIndex] = new SurfaceCache { startXTile = segXMin, topY = fillTopY };

        return result;
    }

    /// <summary>
    /// Segment yüzey yüksekliklerini üretir. col0 = TerrainAnchorY(seg), son sütun = TerrainAnchorY(seg+1)
    /// (komşu segmentle birebir aynı => sınırda uçurum yok). Aradaki her adım |Δ| <= terrainStepMaxTiles.
    /// </summary>
    private int[] BuildSurface(int segIndex, int widthTiles, int minY, int maxY)
    {
        int[] surface = new int[widthTiles];
        int step = Mathf.Max(1, terrainStepMaxTiles);
        int minRun = Mathf.Max(1, terrainMinRunTiles);

        int startY = Mathf.Clamp(TerrainAnchorY(segIndex), minY, maxY);
        int endY = Mathf.Clamp(TerrainAnchorY(segIndex + 1), minY, maxY);

        surface[0] = startY;
        int cy = startY;
        int last = widthTiles - 1;

        // Aynı yükseklikte tutulacak kalan sütun sayısı (plato). Bu >= 1 kaldıkça yükseklik
        // sabit kalır => hiçbir tepe/çukur tek-tile genişliğinde olamaz.
        int hold = Random.Range(minRun, minRun * 2 + 1) - 1;

        for (int i = 1; i <= last; i++)
        {
            int rem = last - i; // bu sütundan sonra kalan sütun sayısı
            // endY'ye kalan sütunlarda ulaşabilmek için fizibilite penceresi:
            int loFeasible = endY - rem * step;
            int hiFeasible = endY + rem * step;

            // endY'ye zamanında yetişmek için mecburen kımıldamamız gerekiyor mu?
            bool mustMove = cy < loFeasible || cy > hiFeasible;

            int ny;
            if (hold > 0 && !mustMove)
            {
                // Plato devam ediyor: yüksekliği sabit tut.
                ny = cy;
                hold--;
            }
            else
            {
                // Yeni kademe: en fazla 'step' kadar değiş, fizibilite penceresine kilitle,
                // ve yeni bir plato (en az minRun geniş) başlat.
                int rnd = Random.Range(-step, step + 1);
                ny = Mathf.Clamp(cy + rnd, minY, maxY);
                ny = Mathf.Clamp(ny, loFeasible, hiFeasible);
                hold = Random.Range(minRun, minRun * 2 + 1) - 1;
            }

            surface[i] = ny;
            cy = ny;
        }
        // rem=0 olan son sütun otomatik olarak endY'ye kilitlenir (sınır sürekliliği).

        // Son geçiş: TEK-TILE genişliğindeki tepe/çukurları (uygun köşe sprite'ı olmayan ince
        // kuleleri/oyukları) komşu seviyesine düzleştir. Sınır sütunlarına (0 ve last) dokunmayız
        // => segment sürekliliği korunur. Sivri ucu komşularından birinin yüksekliğine indirmek
        // |Δ| <= step kuralını da bozmaz (komşular zaten step içindeydi). Düzleştirmek yeni bir
        // sivri uç doğurabileceğinden birkaç geçiş yapıp erken çıkıyoruz.
        for (int pass = 0; pass < 4; pass++)
        {
            bool changed = false;
            for (int i = 1; i < last; i++)
            {
                int l = surface[i - 1];
                int r = surface[i + 1];
                if (surface[i] > l && surface[i] > r)       // tek-tile tepe
                {
                    surface[i] = Mathf.Max(l, r);
                    changed = true;
                }
                else if (surface[i] < l && surface[i] < r)  // tek-tile çukur
                {
                    surface[i] = Mathf.Min(l, r);
                    changed = true;
                }
            }
            if (!changed)
                break;
        }

        return surface;
    }

    /// <summary>Deterministik çukur maskesi üretir. Çukur tabanı katı kalır (void yok), derinlik/genişlik güvenli.</summary>
    private void BuildGaps(int segXMin, int widthTiles, int[] surface, int floorBottom,
                           out bool[] gapCol, out int[] pitTopY)
    {
        gapCol = new bool[widthTiles];
        pitTopY = new int[widthTiles];

        bool doGaps = enableGaps && solidGroundFloor;
        if (!doGaps)
            return;

        int wMax = Mathf.Clamp(Mathf.Min(maxGapTiles, gapMaxWidthCap), 1, Mathf.Max(1, widthTiles - 2));
        int wMin = Mathf.Clamp(minGapTiles, 1, wMax);
        int spacing = Mathf.Max(2, gapMinSpacingTiles);

        int i = spacing + Random.Range(0, spacing + 1); // ilk çukur segment içinde biraz ileride
        while (i >= 1 && i < widthTiles - 1)
        {
            int w = Random.Range(wMin, wMax + 1);
            if (i + w > widthTiles - 1) // son sütunu daima katı bırak (sınır sürekliliği)
                break;

            // Doğum bölgesinde (orijin etrafı) çukur açma.
            bool nearSpawn = false;
            for (int k = 0; k < w; k++)
            {
                if (Mathf.Abs(segXMin + i + k) <= spawnSafeRadiusTiles) { nearSpawn = true; break; }
            }

            if (!nearSpawn)
            {
                int rimY = surface[i - 1]; // sol kenar zemini
                int pitFloor = Mathf.Max(floorBottom, rimY - Mathf.Max(1, gapDepthTiles));
                for (int k = 0; k < w; k++)
                {
                    gapCol[i + k] = true;
                    pitTopY[i + k] = pitFloor;
                }
            }

            i += w + spacing + Random.Range(0, spacing + 1);
        }
    }

    /// <summary>
    /// Havada süzülen platformları yerleştirir. Her platform, altındaki zemin yüzeyinin en az
    /// 'platformClearanceTiles' kadar üstünde durur (asla zeminle birleşip duvar olmaz) ve en fazla
    /// reachUp kadar üstünde durur (oyuncu üstüne zıplayabilir).
    /// </summary>
    private void BuildFloatingPlatforms(int segXMin, int segXMax, int[] surface,
                                        TileBase[] block, BoundsInt bounds, int yMax)
    {
        int width = surface.Length;
        int thickness = Mathf.Max(1, platformThickness);
        int clr = Mathf.Max(1, platformClearanceTiles);

        int cx = segXMin + Random.Range(minGapTiles, maxGapTiles + 1);
        int guard = 0;

        while (cx < segXMax - 1 && guard++ < 128)
        {
            int run = Random.Range(platformMinRunTiles, platformMaxRunTiles + 1);
            int x0 = cx;
            int x1 = Mathf.Min(cx + run - 1, segXMax - 1);

            // Bu platformun kapladığı sütunlar altındaki en YÜKSEK zemin yüzeyi.
            int groundTop = int.MinValue;
            for (int x = x0; x <= x1; x++)
            {
                int li = x - segXMin;
                if (li >= 0 && li < width)
                    groundTop = Mathf.Max(groundTop, surface[li]);
            }
            if (groundTop == int.MinValue)
                groundTop = groundY;

            // Platform gövdesinin ALT sınırı zeminden en az clr yukarıda; ÜST sınırı reachUp içinde.
            int minTopY = groundTop + clr + (thickness - 1);
            int maxTopY = Mathf.Min(groundTop + reachUpTiles, yMax - 1);

            if (maxTopY >= minTopY)
            {
                int py = Random.Range(minTopY, maxTopY + 1);
                int bottomY = Mathf.Max(groundTop + clr, py - (thickness - 1)); // zeminle boşluğu koru
                for (int x = x0; x <= x1; x++)
                    for (int y = py; y >= bottomY; y--)
                        SetBlock(block, bounds, x, y, groundTile);
            }

            cx = x1 + 1 + Random.Range(minGapTiles, maxGapTiles + 1);
        }
    }

    private static void SetBlock(TileBase[] block, BoundsInt bounds, int x, int y, TileBase tile)
    {
        int lx = x - bounds.xMin;
        int ly = y - bounds.yMin;
        if (lx < 0 || ly < 0 || lx >= bounds.size.x || ly >= bounds.size.y)
            return; // blok dışına taşan hücreleri sessizce yut (güvenlik).
        block[ly * bounds.size.x + lx] = tile;
    }

    /// <summary>Verilen tile dikdörtgenini tek seferde temizler (eski segmentleri silmek için).</summary>
    public void ClearBounds(BoundsInt bounds)
    {
        if (targetTilemap == null) return;
        TileBase[] empty = new TileBase[bounds.size.x * bounds.size.y * Mathf.Max(1, bounds.size.z)];
        targetTilemap.SetTilesBlock(bounds, empty);
    }

    /// <summary>Tilemap sınırlarını sıkıştırır (culling/performans; sadece cleanup sonrası çağır).</summary>
    public void CompressTilemapBounds()
    {
        if (targetTilemap != null)
            targetTilemap.CompressBounds();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class mapdesign : MonoBehaviour
{
    [Header("Map Generator")]
    [SerializeField] private Tilemap targetTilemap;

    // Inspector'dan seçilecek tile (Rule Tile/normal tile olabilir).
    [SerializeField] private Tile groundTile;

    [SerializeField] private int width = 120;
    [SerializeField] private int height = 30;
    [SerializeField] private int startX = -60;

    [Tooltip("Her yeni platform için yükseliş kuralı. (tile birimi)")]
    [SerializeField] private int maxStepUpTiles = 2;

    [Tooltip("Platform boşluğu genişliği (tile birimi). Oyuncunun zıplamasına göre küçült/ büyüt.")]
    [SerializeField] private int maxGapTiles = 2;

    [Tooltip("Her X ilerleyişinde ne kadar 'yatay mesafe' açılacağını belirler.")]
    [SerializeField] private int segmentLength = 6;

    [Tooltip("Platform geneli için en az kaç tile dolu olsun.")]
    [SerializeField] private int platformMinRunTiles = 2;

    [Tooltip("Platform geneli için en fazla kaç tile dolu olsun.")]
    [SerializeField] private int platformMaxRunTiles = 5;

    [Header("Safety / Validation")]
    [SerializeField] private bool clearOnStart = true;
    [SerializeField] private int groundY = -2;

    private struct Platform
    {
        public int x0;
        public int x1; // inclusive
        public int y;  // tile y
    }

    private void Awake()
    {
        if (targetTilemap == null)
            targetTilemap = GetComponentInChildren<Tilemap>();
    }

    private void Start()
    {
        if (clearOnStart)
            Generate();
    }

    [Header("Distance Constraints")]
    [Tooltip("Platformlar arası en az boşluk (gap) zorlaması. Birim: tile.")]
    [SerializeField] private int minGapTilesAll = 7;

    [Tooltip("Sol/yukarı gidilen segmentlerde de boşluk sağlamak için uygulanır: Eğer gapTiles bu değerden küçükse zorla büyütülür.")]
    [SerializeField] private int minGapTilesAllForSides = 7;


    public void Generate()
    {
        if (targetTilemap == null)
        {
            Debug.LogError("[mapdesign] targetTilemap bulunamadı. Inspector'dan Tilemap ataman gerekiyor.");
            return;
        }


        if (groundTile == null)
        {
            Debug.LogError("[mapdesign] groundTile boş. Inspector'dan kullanacağın Tile'ı seçmelisin (Rule Tile / normal tile).");
            return;
        }

        // Önce temizle
        targetTilemap.ClearAllTiles();

        // Sabit zemin: en alt satırı garanti dolu yap
        for (int x = startX - 10; x < startX + width + 10; x++)
            SetTile(x, groundY);

        // Değişken platformları üret
        List<Platform> platforms = new List<Platform>(8);

        int currentX = startX;
        int currentY = groundY + 1; // ilk platformu zemin üstüne koy

        // İlk platform
        platforms.Add(MakePlatform(currentX, currentY));
        int startPlatformX1 = platforms[0].x1;
        currentX = startPlatformX1 + 1;

        // Sonuna kadar üret
        while (currentX < startX + width - segmentLength)
        {
            // Bir segment boyunca bazı yerleri boşluk yapacağız ama boşluk çapını kural ile sınırlayacağız.
            int gapTiles = Random.Range(0, maxGapTiles + 1);
            int stepUp = Random.Range(-1, maxStepUpTiles + 1); // aşağı da olabilir

            int nextY = Mathf.Clamp(currentY + stepUp, groundY + 1, groundY + height - 2);

            // Tüm platformlar arası boşluğu zorla: oyuncunun sıkışmasını engellemek için en az minGapTilesAll kadar boşluk kalsın.
            if (gapTiles < minGapTilesAll)
                gapTiles = minGapTilesAll;



            // bir sonraki platformun başlangıcına boşluk ekle
            currentX += gapTiles;

            // platform koşusu
            Platform p = MakePlatform(currentX, nextY);
            platforms.Add(p);

            currentX = p.x1 + 1;
            currentY = nextY;

            // çok yükselme/çok düşme durumunu önlemek için seyrek clamp
            if (platforms.Count > 50) break;
        }


        // Çakışma/boşluk kontrolü (grid seviyesinde temel kontrol)
        Validate(platforms);

        // Görsel olarak oyuncu için daha okunur bir boşluk yapma: sadece debug
        Debug.Log($"[mapdesign] Generated platforms: {platforms.Count}");
    }

    private Platform MakePlatform(int xStart, int y)
    {
        int run = Random.Range(platformMinRunTiles, platformMaxRunTiles + 1);

        // platformu segmentLength içinde tutmaya çalış
        run = Mathf.Min(run, segmentLength);

        int x0 = xStart;
        int x1 = x0 + run - 1;

        for (int x = x0; x <= x1; x++)
            SetTile(x, y);

        return new Platform { x0 = x0, x1 = x1, y = y };
    }

    private void SetTile(int x, int y)
    {
        targetTilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
    }

    private void Validate(List<Platform> platforms)
    {
        // Basit kural: Ardışık platformların yatay mesafesi (x0 - prev.x1 - 1) <= maxGapTiles olmalı
        for (int i = 1; i < platforms.Count; i++)
        {
            int prevRight = platforms[i - 1].x1;
            int gap = platforms[i].x0 - prevRight - 1;
            gap = Mathf.Max(0, gap);

            if (gap > maxGapTiles)
            {
                Debug.LogWarning($"[mapdesign] Gap kuralı ihlali: index={i} gap={gap} maxGapTiles={maxGapTiles}");
            }

            int stepUp = platforms[i].y - platforms[i - 1].y;
            if (stepUp > maxStepUpTiles)
            {
                Debug.LogWarning($"[mapdesign] StepUp kuralı ihlali: index={i} stepUp={stepUp} maxStepUpTiles={maxStepUpTiles}");
            }
        }
    }

#if UNITY_EDITOR
    // Unity editörde tıklanabilir
    [ContextMenu("Generate")]
    private void ContextGenerate() => Generate();
#endif
}


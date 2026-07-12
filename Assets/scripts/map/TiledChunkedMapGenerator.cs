using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// mapdesign.cs yerine/yanına geçebilecek örnek: bir segmenti belirli bir world-X offset’ine göre üretir.
/// NOT: Bu sınıf, InfiniteMapRunner için mapdesign’e GenerateAt eklemesini kolaylaştırmak amacıyla referans.
/// İstersen doğrudan bunu kullanacak şekilde generator yönetimini de değiştirebiliriz.
/// </summary>
public class TiledChunkedMapGenerator : MonoBehaviour
{
    [Header("Map Generator")]
    [SerializeField] private Tilemap targetTilemap;

    [SerializeField] private Tile groundTile;

    [SerializeField] private int width = 120;
    [SerializeField] private int height = 30;
    [SerializeField] private int groundY = -2;

    [Tooltip("Her yeni platform için yükselme kuralı. (tile birimi)")]
    [SerializeField] private int maxStepUpTiles = 2;

    [Tooltip("Platform min yüksekliği (groundY+2 gibi).")]
    [SerializeField] private int minPlatformY = -999;

    [Tooltip("Platform boşluğu minimum (tile birimi). 0 olursa çok dar boşluklar çıkabiliyor.")]
    [SerializeField] private int minGapTiles = 1;

    [Tooltip("Platform boşluğu genişliği maksimum (tile birimi).")]
    [SerializeField] private int maxGapTiles = 2;

    [Tooltip("Her X ilerleyişinde ne kadar 'yatay mesafe' açılacağını belirler.")]
    [SerializeField] private int segmentLength = 6;

    [SerializeField] private int platformMinRunTiles = 2;
    [SerializeField] private int platformMaxRunTiles = 5;

    private struct Platform
    {
        public int x0;
        public int x1;
        public int y;
    }

    private void Awake()
    {
        if (targetTilemap == null)
            targetTilemap = GetComponentInChildren<Tilemap>();
    }

    public void GenerateSegment(int startXTile, int clearRectPad = 2)
    {
        if (targetTilemap == null || groundTile == null)
            return;

        // Sadece segmentin kendi alanını temizle.
        // Burada yan komşu chunk’ların alanına taşarsak mevcut platformlar silinir ve oyuncu düşer.
        int segXMin = startXTile;
        int segXMax = startXTile + width - 1;

        // Zemini ve platformları üretmek için yeterli dikey alanı temizleyelim.
        int yMin = groundY - 2;
        int yMax = groundY + height + 2;

        for (int x = segXMin; x <= segXMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                targetTilemap.SetTile(new Vector3Int(x, y, 0), null);
            }
        }

        // Sabit zemin + platformlar (sadece bu segmente karşılık gelen x aralığında)
        for (int x = segXMin; x <= segXMax; x++)
            SetTile(x, groundY);


        List<Platform> platforms = new List<Platform>(8);
        int currentX = startXTile;
        // Başlangıç platformu daha yukarıda başlasın (oyuncunun boşluk/atlama sorununu azaltır)
        int currentY = groundY + 2;

        platforms.Add(MakePlatform(currentX, currentY));
        currentX = platforms[platforms.Count - 1].x1 + 1;

        while (currentX < startXTile + width - segmentLength)

        {
            int gapTiles = Random.Range(minGapTiles, maxGapTiles + 1);
            int stepUp = Random.Range(-1, maxStepUpTiles + 1);

            int minY = (minPlatformY <= -999) ? (groundY + 2) : minPlatformY;
            int nextY = Mathf.Clamp(currentY + stepUp, minY, groundY + height - 2);

            currentX += gapTiles;

            Platform p = MakePlatform(currentX, nextY);
            platforms.Add(p);

            currentX = p.x1 + 1;
            currentY = nextY;

            if (platforms.Count > 50) break;
        }

        Validate(platforms);
    }

    private Platform MakePlatform(int xStart, int y)
    {
        int run = Random.Range(platformMinRunTiles, platformMaxRunTiles + 1);
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
        for (int i = 1; i < platforms.Count; i++)
        {
            int prevRight = platforms[i - 1].x1;
            int gap = platforms[i].x0 - prevRight - 1;
            gap = Mathf.Max(0, gap);
            if (gap > maxGapTiles)
                Debug.LogWarning($"[TiledChunkedMapGenerator] Gap ihlali index={i} gap={gap}");

            int stepUp = platforms[i].y - platforms[i - 1].y;
            if (stepUp > maxStepUpTiles)
                Debug.LogWarning($"[TiledChunkedMapGenerator] StepUp ihlali index={i} stepUp={stepUp}");
        }
    }
}


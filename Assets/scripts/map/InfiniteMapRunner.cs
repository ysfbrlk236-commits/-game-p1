using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun etrafında PENCERE tabanlı sonsuz map akışı yönetir.
/// - Her frame oyuncunun etrafındaki segmentlerin var olduğundan emin olur (eksikse üretir).
///   Oyuncu geri dönerse temizlenmiş segmentler DETERMİNİSTİK olarak birebir yeniden üretilir.
/// - Oyuncudan yeterince uzaklaşan segmentler tilemap'ten silinir (optimizasyon).
/// - Başlangıçta oyuncunun çevresi baştan doldurulur ve oyuncu zemine oturtulur (boşlukta başlamaz).
/// </summary>
public class InfiniteMapRunner : MonoBehaviour
{
    [Header("Streaming Generator")]
    [SerializeField] private TiledChunkedMapGenerator chunkGenerator;
    [SerializeField] private Transform player;

    [Header("Streaming")]
    [Tooltip("Oyuncunun önünde/arkasında kaç world-unit ileriye kadar map hazır tutulsun.")]
    [SerializeField] private float spawnAheadX = 40f;

    [Tooltip("Bir segmentin (chunk) kaç tile genişliğinde olacağı.")]
    [SerializeField] private int segmentWidthTiles = 40;

    [Tooltip("Oyuncunun etrafında hafızada tutulacak maksimum segment yarıçapı. Bundan uzak chunk'lar silinir.")]
    [SerializeField] private int maxSegments = 6;

    [Tooltip("Segment index 0'ın world sol X'i (tile hizası). Grid ile aynı origin olmalı.")]
    [SerializeField] private float firstSegmentLeftWorldX = -60f;

    [Header("Başlangıç")]
    [Tooltip("Oyuncuyu başlangıçta zemin yüzeyine oturt (boşlukta/havada başlamayı engeller).")]
    [SerializeField] private bool snapPlayerToGroundOnStart = true;
    [SerializeField] private float startGroundClearance = 1f;

    private readonly Dictionary<int, BoundsInt> segments = new Dictionary<int, BoundsInt>();
    private bool initialized;

    private void Reset()
    {
        chunkGenerator = GetComponent<TiledChunkedMapGenerator>();
    }

    private void Start()
    {
        if (chunkGenerator == null || player == null)
        {
            Debug.LogWarning("[InfiniteMapRunner] chunkGenerator veya player atanmadı. Streaming devre dışı.");
            enabled = false;
            return;
        }

        // Oyuncunun çevresini baştan doldur => başlangıçta ayağının altında zemin garanti.
        int playerSeg = SegmentIndexForWorldX(player.position.x);
        EnsureWindow(playerSeg);

        if (snapPlayerToGroundOnStart)
            SnapPlayerToGround();

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        int playerSeg = SegmentIndexForWorldX(player.position.x);
        EnsureWindow(playerSeg);
        CleanupFarSegments(playerSeg);
    }

    private int SpawnRadius() => Mathf.Max(1, Mathf.CeilToInt(spawnAheadX / segmentWidthTiles) + 1);
    private int CleanupRadius() => Mathf.Max(maxSegments, SpawnRadius() + 1);

    private float WorldLeftOfSegment(int segmentIndex) => firstSegmentLeftWorldX + segmentIndex * segmentWidthTiles;

    private int SegmentIndexForWorldX(float worldX) =>
        Mathf.FloorToInt((worldX - firstSegmentLeftWorldX) / segmentWidthTiles);

    private void EnsureWindow(int centerSeg)
    {
        int r = SpawnRadius();
        for (int idx = centerSeg - r; idx <= centerSeg + r; idx++)
            EnsureSegment(idx);
    }

    private void EnsureSegment(int idx)
    {
        if (segments.ContainsKey(idx))
            return; // zaten var => yeniden üretme (deterministik olduğu için de aynı olurdu).

        int startXTile = Mathf.RoundToInt(WorldLeftOfSegment(idx));
        var result = chunkGenerator.GenerateSegment(idx, startXTile, segmentWidthTiles);
        segments[idx] = result.bounds;
    }

    private void CleanupFarSegments(int playerSeg)
    {
        int radius = CleanupRadius();

        List<int> toRemove = null;
        foreach (var kvp in segments)
        {
            if (Mathf.Abs(kvp.Key - playerSeg) > radius)
                (toRemove ??= new List<int>()).Add(kvp.Key);
        }

        if (toRemove == null)
            return;

        foreach (int idx in toRemove)
        {
            chunkGenerator.ClearBounds(segments[idx]);
            segments.Remove(idx);
        }

        chunkGenerator.CompressTilemapBounds();
    }

    private void SnapPlayerToGround()
    {
        // Zemin artık düz değil; oyuncunun bulunduğu X'teki gerçek yüzeye (tepe/çukur) oturt.
        float y = chunkGenerator.GroundSurfaceWorldYAt(player.position.x) + startGroundClearance;
        Vector3 p = player.position;
        p.y = y;
        player.position = p;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }
}

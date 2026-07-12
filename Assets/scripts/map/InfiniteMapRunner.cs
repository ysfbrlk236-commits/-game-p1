using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Oyuncu ilerledikçe sağa doğru yeni map segmentleri üretir.
/// mapdesign içindeki Generate() tek sefer üretim yapıyorsa, bu sınıf
/// birden fazla “segment” üretip Tilemap’i yerinde günceller.
/// </summary>
public class InfiniteMapRunner : MonoBehaviour
{
    [Header("Streaming Generator")]
    [Tooltip("Segment bazlı tile üretimini yapan generator")]
    [SerializeField] private TiledChunkedMapGenerator chunkGenerator;

    [SerializeField] private Transform player;


    [Header("Streaming")]
    [Tooltip("Oyuncu bu X değerini geçince yeni segment üret")]
    [SerializeField] private float spawnAheadX = 35f;

    [Tooltip("Bir segmentin kaç tile genişliğinde olacağı. mapdesign.width/segment mantığına göre ayarla")]
    [SerializeField] private int segmentWidthTiles = 40;

    [Tooltip("Hafızada tutulacak maksimum segment adedi (eski segmentler silinir)")]
    [SerializeField] private int maxSegments = 6;

    private int producedSegments = 0;

    private void Reset()
    {
        chunkGenerator = GetComponent<TiledChunkedMapGenerator>();
    }


    private int rightmostSegmentIndex = 0; // inclusive
    private int leftmostSegmentIndex = 0;  // inclusive
    private bool initialized = false;

    private void Start()
    {
        // İlk segmenti hemen üret.
        float firstLeft = firstSegmentLeftWorldX;
        int firstIndex = 0;
        ProduceSegmentByIndex(firstIndex, firstLeft);
        rightmostSegmentIndex = firstIndex;
        leftmostSegmentIndex = firstIndex;
        producedSegments = 1;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || chunkGenerator == null || player == null)
            return;

        float playerX = player.position.x;

        // Segment index’ine göre world sol sınırını hesaplayacağız.
        // worldLeft(index) = firstSegmentLeftWorldX + index * segmentWidthTiles
        float rightSpawnTrigger = WorldLeftOfSegment(rightmostSegmentIndex + 1) + spawnAheadX;
        if (playerX >= rightSpawnTrigger)
        {
            int nextIndex = rightmostSegmentIndex + 1;
            ProduceSegmentByIndex(nextIndex, WorldLeftOfSegment(nextIndex));
            rightmostSegmentIndex = nextIndex;
            producedSegments++;
            CleanupOldSegments();
        }

        float leftSpawnTrigger = WorldLeftOfSegment(leftmostSegmentIndex) - spawnAheadX;
        if (playerX <= leftSpawnTrigger)
        {
            int prevIndex = leftmostSegmentIndex - 1;
            ProduceSegmentByIndex(prevIndex, WorldLeftOfSegment(prevIndex));
            leftmostSegmentIndex = prevIndex;
            producedSegments++;
            CleanupOldSegments();
        }
    }

    [Header("Streaming")]
    [SerializeField] private float firstSegmentLeftWorldX = -60f;

    private float WorldLeftOfSegment(int segmentIndex) => firstSegmentLeftWorldX + segmentIndex * segmentWidthTiles;

    private void ProduceSegmentByIndex(int segmentIndex, float segmentLeftWorldX)
    {
        int startXTile = Mathf.RoundToInt(segmentLeftWorldX);
        chunkGenerator?.GenerateSegment(startXTile);
    }

    // (İhtiyaç olursa) segment world-left değerinden üretmek için tek yardımcı.
    // Not: Şu an Update() ve Start() ProduceSegmentByIndex kullanıyor.
    private void ProduceSegment(float segmentLeftWorldX)
    {
        int startXTile = Mathf.RoundToInt(segmentLeftWorldX);
        if (chunkGenerator != null)
            chunkGenerator.GenerateSegment(startXTile);
        else
            Debug.LogWarning("[InfiniteMapRunner] chunkGenerator atanmadı. Tile üretimi yapılamıyor.");
    }




    private void CleanupOldSegments()
    {
        // Basit versiyon: şu an boş. İstersen segmentleri Tilemap üzerinden cell bazında silip
        // hafifletecek şekilde genişletebiliriz.
    }
}


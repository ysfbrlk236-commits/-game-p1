using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class enemyai : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float stopDistance = 1.5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Edge Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float edgeCheckXOffset = 0.5f;
    [SerializeField] private float edgeCheckLength = 1.5f;

    [Header("Patrol")]
    [SerializeField] private float patrolRayLength = 1f;
    [SerializeField] private LayerMask obstacleLayer;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private Transform Player;
    private float facingDir = 1f;
    private bool isChasing;
    private float flipCooldown;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        DetectPlayer();

        if (isChasing && Player != null)
            ChasePlayer();
        else
            Patrol();
    }

    RaycastHit2D RaycastIgnoreSelf(Vector2 origin, Vector2 dir, float distance, LayerMask mask)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, distance, mask);
        foreach (var hit in hits)
        {
            if (hit.collider != ownCollider)
                return hit;
        }
        return default;
    }

    void DetectPlayer()
    {
        Vector2 origin = (Vector2)transform.position;

        RaycastHit2D hitRight = RaycastIgnoreSelf(origin, Vector2.right, detectionRange, playerLayer);
        RaycastHit2D hitLeft  = RaycastIgnoreSelf(origin, Vector2.left,  detectionRange, playerLayer);

        bool rightHit = hitRight.collider != null && hitRight.collider.CompareTag("Player");
        bool leftHit  = hitLeft.collider  != null && hitLeft.collider.CompareTag("Player");

        if (rightHit || leftHit)
        {
            isChasing = true;
            Player = rightHit ? hitRight.collider.transform : hitLeft.collider.transform;
        }
        else
        {
            isChasing = false;
        }

        Debug.DrawRay(origin, Vector2.right * detectionRange, rightHit ? Color.green : Color.yellow);
        Debug.DrawRay(origin, Vector2.left  * detectionRange, leftHit  ? Color.green : Color.yellow);
    }

    void Patrol()
    {
        flipCooldown -= Time.deltaTime;

        Vector2 origin = (Vector2)transform.position;
        Vector2 dir = new(facingDir, 0f);

        RaycastHit2D wallHit = RaycastIgnoreSelf(origin, dir, patrolRayLength, obstacleLayer);

        Vector2 edgeOrigin = new(transform.position.x + facingDir * edgeCheckXOffset, transform.position.y);
        Vector2 edgeDir    = new Vector2(facingDir * 0.5f, -1f).normalized;
        RaycastHit2D edgeHit = Physics2D.Raycast(edgeOrigin, edgeDir, edgeCheckLength, groundLayer);

        Debug.DrawRay(origin, dir * patrolRayLength, wallHit.collider != null ? Color.magenta : Color.white);
        Debug.DrawRay(edgeOrigin, edgeDir * edgeCheckLength, edgeHit.collider != null ? Color.cyan : Color.red);

        if (flipCooldown <= 0f && (wallHit.collider != null || edgeHit.collider == null))
        {
            facingDir = -facingDir;
            flipCooldown = 0.2f;
        }

        rb.linearVelocity = new Vector2(facingDir * moveSpeed, rb.linearVelocity.y);
        FlipSprite();
    }

    void ChasePlayer()
    {
        float distToPlayer = Vector2.Distance(transform.position, Player.position);

        if (distToPlayer <= stopDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        facingDir = Player.position.x > transform.position.x ? 1f : -1f;

        // Çapraz kenar tespiti: ileriye ve aşağıya doğru raycast
        Vector2 edgeOrigin = new(transform.position.x + facingDir * edgeCheckXOffset, transform.position.y);
        Vector2 edgeDir    = new Vector2(facingDir * 0.5f, -1f).normalized;

        RaycastHit2D edgeHit = Physics2D.Raycast(edgeOrigin, edgeDir, edgeCheckLength, groundLayer);

        Debug.DrawRay(edgeOrigin, edgeDir * edgeCheckLength, edgeHit.collider != null ? Color.cyan : Color.red);

        // Kenar tespit edildi → dur
        if (edgeHit.collider == null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(facingDir * moveSpeed, rb.linearVelocity.y);
        FlipSprite();
    }

    void FlipSprite()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDir;
        transform.localScale = scale;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, Vector2.right * detectionRange);
        Gizmos.DrawRay(transform.position, Vector2.left  * detectionRange);

        Gizmos.color = Color.red;
        Vector2 edgeOrigin = new(transform.position.x + facingDir * edgeCheckXOffset, transform.position.y);
        Vector2 edgeDir    = new Vector2(facingDir * 0.5f, -1f).normalized;
        Gizmos.DrawRay(edgeOrigin, edgeDir * edgeCheckLength);
    }
}

using UnityEngine;

public class TargetHit : MonoBehaviour
{
    [Header("Settings")]
    public string gloveTag = "PlayerHand"; // The tag we check for
    public GameObject hitParticlePrefab;   // Optional: Explosion effect
    public AudioClip hitSound;             // Sound to play on hit

    // Global Score Tracker
    public static int score = 0;

    // We use OnTriggerEnter because we don't want physical bouncing (physics),
    // we just want to detect the overlap.
    void OnTriggerEnter(Collider other)
    {
        // Check if the object hitting us is the Glove
        if (other.CompareTag(gloveTag))
        {
            RegisterHit();
        }
    }

    void RegisterHit()
    {
        // 1. Increment Score
        score++;
        Debug.Log("Target Hit! Score: " + score);

        // 2. (Optional) Spawn visual effect
        if (hitParticlePrefab != null)
        {
            Instantiate(hitParticlePrefab, transform.position, Quaternion.identity);
        }

        // 3. Play Sound
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        // 4. Destroy the Target
        // This makes 'activeTargets' list inside TargetSpawner behave correctly (it removes nulls).
        Destroy(gameObject);
    }
}
using UnityEngine;

public class TargetHit : MonoBehaviour
{
    [Header("Settings")]
    public string gloveTag = "PlayerHand"; // The tag we check for
    public GameObject hitParticlePrefab;   // Optional: Explosion effect
    public AudioClip hitSound;              // Sound to play on hit

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
        // 1. (Optional) Spawn visual effect
        if (hitParticlePrefab != null)
        {
            Instantiate(hitParticlePrefab, transform.position, Quaternion.identity);
        }

        // 2. Play Sound
        AudioSource.PlayClipAtPoint(hitSound, transform.position);

        // 3. Destroy the Target
        // This makes 'currentTarget' null in the TargetSpawner, causing a new one to spawn.
        Destroy(gameObject);
    }
}
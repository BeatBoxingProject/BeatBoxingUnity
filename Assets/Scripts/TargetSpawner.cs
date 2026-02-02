using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for Lists

public class TargetSpawner : MonoBehaviour
{
    [Header("Configuration")]
    public GameObject targetPrefab; // Assign your Red Sphere prefab here
    public Transform bagCenter;     // Assign your Bag Cylinder here
    public float bagRadius = 0.19f;  // Radius of your bag in Unity units
    public float bagHeight = 0.4f;  // Height of punchable area

    [Tooltip("The total angle width. Example: 90 will result in -45 to +45 degrees.")]
    public float spawnAngleRange = 90f;

    [Tooltip("How many targets should exist at the same time.")]
    public int maxConcurrentTargets = 2; // NEW: Set this to 3, 5, etc.

    // Internal tracker for multiple active targets
    private List<GameObject> activeTargets = new List<GameObject>();

    void Start()
    {
        // 1. Auto-create bag center if missing
        if (bagCenter == null)
        {
            bagCenter = new GameObject("BagCenter").transform;
        }

        // Initial fill
        MaintainTargetCount();
    }

    void Update()
    {
        // Check if any targets are gone (hit by player)
        // Remove null entries from the list (targets that were destroyed)
        activeTargets.RemoveAll(item => item == null);

        // If we have fewer targets than the max, spawn more immediately
        MaintainTargetCount();
    }

    void MaintainTargetCount()
    {
        while (activeTargets.Count < maxConcurrentTargets)
        {
            SpawnTarget();
        }
    }

    void SpawnTarget()
    {
        // 1. Pick a random angle based on the configured range
        // We limit it to the front area so targets don't spawn behind the bag
        float angle = Random.Range(-spawnAngleRange / 2f, spawnAngleRange / 2f);

        // 2. Pick a random height offset
        float height = Random.Range(-bagHeight / 2f, bagHeight / 2f);

        // 3. Convert Polar (Angle) to Cartesian (X, Z)
        // Assuming Bag is centered at (0,0,0) and Up is Y
        float rad = angle * Mathf.Deg2Rad;
        float x = Mathf.Sin(rad) * bagRadius;
        float z = Mathf.Cos(rad) * bagRadius;

        // Note: Depending on how you rotated your cylinder during calibration,
        // you might need to swap sin/cos or x/z. Use the Scene view to verify.

        Vector3 spawnPos = bagCenter.position + new Vector3(x, height, -z); // -z to face camera usually

        // 4. Instantiate
        GameObject newTarget = Instantiate(targetPrefab, spawnPos, Quaternion.identity);

        // Add to our list of active targets so we can track it
        activeTargets.Add(newTarget);

        // 5. Apply User Scale
        newTarget.transform.localScale = new Vector3(0.1f, 0.1f, 0.01f);

        // 6. Rotation Logic (Keep X rotation 0)
        // Make it look at the center of the bag horizontally, but match the target's height vertically.
        // This ensures it rotates only around the Y axis.
        Vector3 lookAtPos = new Vector3(bagCenter.position.x, newTarget.transform.position.y, bagCenter.position.z);
        newTarget.transform.LookAt(lookAtPos);
    }
}
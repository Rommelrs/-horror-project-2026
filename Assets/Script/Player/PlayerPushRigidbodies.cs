using UnityEngine;

public class PlayerPushRigidbodies : MonoBehaviour
{
    [Header("Push Settings")]
    [SerializeField] private float pushPower = 2.0f;
    [Tooltip("Layer mask for objects that can be pushed (e.g. soccer ball)")]
    [SerializeField] private LayerMask pushableLayers = -1; // Default: everything
    
    [Header("Kick Settings")]
    [SerializeField] private bool enableKicking = true;
    [SerializeField] private KeyCode kickKey = KeyCode.E;
    [SerializeField] private float kickForce = 10f;
    [SerializeField] private float kickRadius = 1.5f;
    [SerializeField] private float kickCooldown = 0.5f;
    
    private CharacterController characterController;
    private float lastKickTime = -999f;
    
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("PlayerPushRigidbodies requires a CharacterController component!");
        }
    }
    
    void Update()
    {
        // Handle kicking input
        if (enableKicking && Input.GetKeyDown(kickKey) && Time.time > lastKickTime + kickCooldown)
        {
            TryKickNearbyObjects();
        }
    }
    
    // Called automatically when CharacterController collides with something
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Check if we hit a rigidbody
        Rigidbody body = hit.collider.attachedRigidbody;
        
        // Exit if no rigidbody or rigidbody is kinematic
        if (body == null || body.isKinematic)
            return;
        
        // Check if the object is on a pushable layer
        if (((1 << hit.gameObject.layer) & pushableLayers) == 0)
            return;
        
        // Don't push objects below us (prevents pushing things we're standing on)
        if (hit.moveDirection.y < -0.3f)
            return;
        
        // Calculate push direction (horizontal only for more realistic ball pushing)
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        
        // Apply force based on player's movement speed
        float pushStrength = characterController.velocity.magnitude * pushPower;
        
        // Apply the force
        body.AddForceAtPosition(pushDir.normalized * pushStrength, hit.point, ForceMode.Impulse);
    }
    
    void TryKickNearbyObjects()
    {
        // Find all colliders in kick radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, kickRadius, pushableLayers);
        
        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                // Calculate kick direction (away from player, with upward lift)
                Vector3 directionToObject = (col.transform.position - transform.position).normalized;
                Vector3 kickDirection = directionToObject + Vector3.up * 0.5f; // Add upward component
                
                // Apply kick force
                rb.AddForce(kickDirection * kickForce, ForceMode.Impulse);
                
                lastKickTime = Time.time;
                
                // Only kick one object at a time
                break;
            }
        }
    }
    
    // Visualize kick radius in editor
    void OnDrawGizmosSelected()
    {
        if (enableKicking)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, kickRadius);
        }
    }
}

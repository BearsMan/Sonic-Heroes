using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Cannon;

/// <summary>
/// Sonic Heroes Cannon System
/// 
/// CANNON TYPES & CHARACTER INTERACTIONS (Sonic Heroes US):
/// =====================================================
/// 
/// SPEED CANNON (Default/Forward):
/// - Activates Speed Formation (Sonic/Shadow/Cream)
/// - Launches team forward in a straight line
/// - Bypasses speed-based obstacles
/// 
/// VERTICAL CANNON (Upward):
/// - Activates Fly Formation (Tails/Rouge/Charmy)
/// - Launches team upward and across gaps
/// - Provides access to aerial sections
/// 
/// SIDEWAYS CANNON (Side-facing):
/// - Activates Fly Formation (Tails/Rouge/Charmy)
/// - Launches team sideways
/// - For horizontal aerial passages
/// 
/// CHARACTER TYPES & ABILITIES:
/// ===========================
/// 
/// SPEED TYPE: Sonic, Shadow, Cream
/// - Fastest movement speed
/// - Break through certain objects while running
/// - Optimal for Speed Cannons launching forward
/// 
/// POWER TYPE: Knuckles, Omega, Big
/// - Strongest physical strength
/// - DESTROY obstacles blocking paths (rocks, metal blocks, cages)
/// - Can ROTATE and AIM cannons freely (unlike other types)
/// - Break through Power-specific barriers
/// 
/// FLY TYPE: Tails, Rouge, Charmy
/// - Flight and hover abilities
/// - Reach high/distant platforms
/// - Optimal for Vertical and Sideways Cannons
/// - Can bypass ground-based obstacles
/// 
/// LEVEL UP SYSTEM:
/// ================
/// All characters start at Level 1 and can level up to Level 3
/// - Level 1: Normal abilities
/// - Level 2: Enhanced abilities
/// - Level 3: Can DESTROY CANNONS (all character types)
/// 
/// CANNON DESTRUCTION:
/// ===================
/// Characters at LEVEL 3+ can destroy cannons on contact:
/// 1. Cannon checks character level when touched
/// 2. If level >= 3, cannon is instantly destroyed
/// 3. Destruction effects and sounds play
/// 4. Cannon becomes unusable
/// 5. Destruction works for ALL character types at level 3+
/// 
/// CANNON MECHANICS (Sonic Heroes):
/// ================================
/// 1. Different teams activate cannons based on formation leader
/// 2. Power-type characters can rotate cannons for targeting
/// 3. Cannons reset after launching (return to default orientation)
/// 4. Multiple paths may require different character types
/// 5. Obstacles before cannons must be cleared by matching type
/// 6. Level 3+ characters can destroy cannons as alternate paths
/// </summary>

public class Cannon : MonoBehaviour
{
    // Cannon Types (matching Sonic Heroes)
    public enum CannonType
    {
        Speed,          // Launches forward (Speed-type characters)
        Vertical,       // Launches upward (Fly-type characters)
        Sideways,       // Launches to the side (Fly-type characters)
        CustomAngle     // Custom direction
    }

    // Character Types in Sonic Heroes
    public enum CharacterType
    {
        Speed,      // Sonic, Shadow, Cream - Fast movement
        Power,      // Knuckles, Omega, Big - Destroys obstacles
        Fly         // Tails, Rouge, Charmy - Flight & height
    }

    [Header("Cannon Settings")]
    public CannonType cannonType = CannonType.Speed;
    public float launchForce = 50f;
    public float launchDuration = 0.5f;
    public bool isMultiUse = false;

    [Header("Sonic Heroes Character Types")]
    [SerializeField]
    private string cannonInfoText = "Speed: Sonic/Shadow/Cream launches forward\n" +
                                    "Fly: Tails/Rouge/Charmy launches upward\n" +
                                    "Power: Knuckles/Omega/Big can aim cannon freely\n\n" +
                                    "Power-type characters can destroy obstacles\n" +
                                    "blocking cannon access";

    [Header("Custom Angle (if CustomAngle type)")]
    public Vector3 customDirection = Vector3.forward;
    public float customAngle = 45f;

    [Header("Audio")]
    public AudioClip launchClip;
    public AudioClip chargeClip;

    [Header("Effects")]
    [SerializeField]
    private ParticleSystem launchEffect;

    [Header("Visuals")]
    [SerializeField]
    private GameObject launchVisual;

    [Header("Cannon Destruction (Level 3+ Characters)")]
    public bool canBeDestroyed = true;
    public AudioClip destructionSoundClip;
    public ParticleSystem destructionEffect;

    private bool isDestroyed = false;
    private int cannonHealth = 1; // Cannons are destroyed in one hit at level 3+

    private bool cannon1Activated = false;
    private GameObject audioSource;
    private Rigidbody playerRigidbody;
    private UltimatePlayerMovement playerMovement;
    private CharacterType currentCharacterType = CharacterType.Speed;

    void Start()
    {
        // Initialize cannon visual if not assigned
        if (launchVisual == null)
            launchVisual = gameObject;
    }

    void Update()
    {
        // Reset cannon if multi-use and player has left
        if (isMultiUse && cannon1Activated)
        {
            cannon1Activated = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDestroyed)
            return;

        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        Rigidbody rb = other.GetComponent<Rigidbody>();

        if (player != null && rb != null)
        {
            // Detect character type from player component
            DetectCharacterType(player);

            // Check if character can destroy the cannon (level 3+)
            if (canBeDestroyed && CanDestroyCannonAtLevel3(player))
            {
                DestroyCannon();
                return;
            }

            // Otherwise, use the cannon normally
            if (!cannon1Activated || isMultiUse)
            {
                cannon1Activated = true;
                playerMovement = player;
                playerRigidbody = rb;

                // Play charge sound if available
                if (chargeClip != null)
                {
                    PlayAudio(chargeClip);
                }

                StartCoroutine(LaunchSequence());
            }
        }
    }

    /// <summary>
    /// Check if character is at level 3 or higher and can destroy the cannon
    /// All character types can destroy cannons at level 3+
    /// </summary>
    private bool CanDestroyCannonAtLevel3(UltimatePlayerMovement player)
    {
        // Try to get character level from player component
        int characterLevel = GetCharacterLevel(player);

        if (characterLevel >= 3)
        {
            Debug.Log($"{player.name} is level {characterLevel} - can destroy cannon!");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the current level of the character
    /// This should be customized based on your character system
    /// </summary>
    private int GetCharacterLevel(UltimatePlayerMovement player)
    {
        // Check if player has a Level component or property
        // Example implementations:

        // Option 1: Check for CharacterLevel component
        var levelComponent = player.GetComponent<CharacterLevel>();
        if (levelComponent != null)
        {
            return levelComponent.GetLevel();
        }

        // Option 2: Check if player has a Level property/field
        var levelField = player.GetType().GetField("level",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.IgnoreCase);
        if (levelField != null)
        {
            return (int)levelField.GetValue(player);
        }

        // Option 3: Check for property
        var levelProperty = player.GetType().GetProperty("Level",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.IgnoreCase);
        if (levelProperty != null)
        {
            return (int)levelProperty.GetValue(player);
        }

        // Default: return 1 (level not found)
        Debug.LogWarning($"Could not find level on {player.name}. Make sure character has a Level system implemented.");
        return 1;
    }

    /// <summary>
    /// Destroy the cannon
    /// All character types at level 3+ can destroy cannons
    /// </summary>
    private void DestroyCannon()
    {
        if (isDestroyed)
            return;

        isDestroyed = true;
        Debug.Log("Cannon destroyed by level 3+ character!");

        // Play destruction sound
        if (destructionSoundClip != null)
        {
            PlayAudio(destructionSoundClip);
        }

        // Create destruction effect
        if (destructionEffect != null)
        {
            ParticleSystem effect = Instantiate(destructionEffect, transform.position, Quaternion.identity);
            // Optional: destroy effect after it finishes
            Destroy(effect.gameObject, destructionEffect.main.duration + 0.5f);
        }

        // Disable cannon collider so it can't be used
        Collider cannonCollider = GetComponent<Collider>();
        if (cannonCollider != null)
        {
            cannonCollider.enabled = false;
        }

        // Optional: Disable cannon visuals
        if (launchVisual != null)
        {
            launchVisual.SetActive(false);
        }

        // Optional: Play destruction animation
        StartCoroutine(DestructionSequence());
    }

    /// <summary>
    /// Destruction animation sequence
    /// </summary>
    private IEnumerator DestructionSequence()
    {
        // Shake cannon or play destruction animation
        Vector3 originalPosition = transform.position;
        float shakeAmount = 0.1f;
        float shakeDuration = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < shakeDuration)
        {
            transform.position = originalPosition + Random.insideUnitSphere * shakeAmount;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPosition;

        // Optionally destroy the game object after a delay
        // Uncomment if you want the cannon to disappear completely:
        // yield return new WaitForSeconds(1f);
        // Destroy(gameObject);
    }

    private void DetectCharacterType(UltimatePlayerMovement player)
    {
        // This method should be customized based on your character identification system
        // Example: Check player name, tag, or a character component
        
        string playerName = player.name.ToLower();

        // Speed Characters: Sonic, Shadow, Cream
        if (playerName.Contains("sonic") || playerName.Contains("shadow") || playerName.Contains("cream"))
        {
            currentCharacterType = CharacterType.Speed;
        }
        // Power Characters: Knuckles, Omega, Big
        else if (playerName.Contains("knuckles") || playerName.Contains("omega") || playerName.Contains("big"))
        {
            currentCharacterType = CharacterType.Power;
        }
        // Fly Characters: Tails, Rouge, Charmy
        else if (playerName.Contains("tails") || playerName.Contains("rouge") || playerName.Contains("charmy") || playerName.Contains("charmy"))
        {
            currentCharacterType = CharacterType.Fly;
        }
        else
        {
            currentCharacterType = CharacterType.Speed; // Default
        }

        Debug.Log($"Cannon activated by {currentCharacterType} type character: {player.name}");
    }

    private IEnumerator LaunchSequence()
{
    // Wait for charge time
    yield return new WaitForSeconds(launchDuration * 0.3f);

    // Play launch sound
    if (launchClip != null)
    {
        PlayAudio(launchClip);
    }

    // Create launch effect
    if (launchEffect != null)
    {
        Instantiate(launchEffect, transform.position, Quaternion.identity);
    }

    // Apply launch
    ApplyLaunch();

    yield return new WaitForSeconds(launchDuration * 0.7f);
}

private void ApplyLaunch()
{
    if (playerRigidbody == null)
        return;

    Vector3 launchDirection = GetLaunchDirection();

    // Clear current velocity and apply new launch force
    playerRigidbody.linearVelocity = Vector3.zero;
    playerRigidbody.AddForce(launchDirection * launchForce, ForceMode.Impulse);

    // Set player state to launched (if playerMovement has this)
    if (playerMovement != null)
    {
        // Add any custom player state updates here
        // Example: playerMovement.SetLaunched(true);
    }
}

private Vector3 GetLaunchDirection()
{
    switch (cannonType)
    {
        case CannonType.Speed:
            // Speed characters: Launch forward
            // Power characters: Can rotate and aim freely
            if (currentCharacterType == CharacterType.Power)
            {
                return customDirection.normalized;
            }
            return transform.forward;

        case CannonType.Vertical:
            // Fly characters: Launch upward efficiently
            // Power characters: Can redirect if needed
            if (currentCharacterType == CharacterType.Power)
            {
                return customDirection.normalized;
            }
            return Vector3.up;

        case CannonType.Sideways:
            // Fly characters: Optimal for accessing side paths
            // Power characters: Can rotate and aim
            if (currentCharacterType == CharacterType.Power)
            {
                return customDirection.normalized;
            }
            return transform.right;

        case CannonType.CustomAngle:
            // All characters can use custom angle
            // Power characters have full control
            Vector3 direction = customDirection.normalized;
            Quaternion rotation = Quaternion.AngleAxis(customAngle, Vector3.right);
            return rotation * direction;

        default:
            return transform.forward;
    }
}

private void PlayAudio(AudioClip clip)
{
    audioSource = Resources.Load<GameObject>("Audio Object");
    if (audioSource != null)
    {
        GameObject audioInstance = Instantiate(audioSource, transform.position, Quaternion.identity);
        AudioObject audioComponent = audioInstance.GetComponent<AudioObject>();
        if (audioComponent != null)
        {
            audioComponent.Setup(clip, transform);
        }
    }
}

// Public methods for external control
public void SetCannonType(CannonType type)
{
    cannonType = type;
}

public void SetLaunchForce(float force)
{
    launchForce = force;
}

/// <summary>
/// Set the character type currently using the cannon
/// Useful for manual control or testing different character interactions
/// </summary>
public void SetCharacterType(CharacterType type)
{
    currentCharacterType = type;
}

/// <summary>
/// Get the current character type using the cannon
/// </summary>
public CharacterType GetCurrentCharacterType()
{
    return currentCharacterType;
}

/// <summary>
/// Rotate cannon for aiming (Power-type characters only)
/// Called when Power-type character has manual cannon control
/// </summary>
public void RotateCannonForAiming(float angle)
{
    if (currentCharacterType == CharacterType.Power)
    {
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * Quaternion.identity;
        customAngle = angle;
    }
}

/// <summary>
/// Check if cannon has been destroyed
/// </summary>
public bool IsDestroyed()
{
    return isDestroyed;
}

/// <summary>
/// Force destroy the cannon (for debugging or special events)
/// </summary>
public void ForceDestroy()
{
    DestroyCannon();
}

/// <summary>
/// Reset cannon to usable state (for debugging or level restart)
/// </summary>
public void ResetCannonState()
{
    isDestroyed = false;
    cannon1Activated = false;

    // Re-enable collider
    Collider cannonCollider = GetComponent<Collider>();
    if (cannonCollider != null)
    {
        cannonCollider.enabled = true;
    }

    // Re-enable visuals
    if (launchVisual != null)
    {
        launchVisual.SetActive(true);
    }

    // Reset rotation to default
    transform.rotation = Quaternion.identity;

    if (playerRigidbody != null)
        playerRigidbody.linearVelocity = Vector3.zero;

    Debug.Log("Cannon reset to usable state");
}

// Debug visualization
private void OnDrawGizmosSelected()
{
    Vector3 direction = GetLaunchDirection();
    Gizmos.color = Color.yellow;
    Gizmos.DrawRay(transform.position, direction * 5f);
}
}

/// <summary>
/// Character Level System for Sonic Heroes
/// Attach this component to characters to track their level (1-3)
/// Characters at level 3+ can destroy cannons
/// </summary>
public class CharacterLevel : MonoBehaviour
{
    [SerializeField]
    private int currentLevel = 1;

    [SerializeField]
    private int maxLevel = 3;

    [SerializeField]
    private List<int> experiencePerLevel = new List<int> { 0, 100, 250 };

    private int currentExperience = 0;

    public int GetLevel()
    {
        return currentLevel;
    }

    public void SetLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, maxLevel);
        Debug.Log($"{gameObject.name} is now level {currentLevel}");
    }

    public void AddExperience(int amount)
    {
        currentExperience += amount;
        CheckLevelUp();
    }

    private void CheckLevelUp()
    {
        if (currentLevel >= maxLevel)
            return;

        if (currentLevel < experiencePerLevel.Count &&
            currentExperience >= experiencePerLevel[currentLevel])
        {
            currentLevel++;
            Debug.Log($"{gameObject.name} leveled up to {currentLevel}!");

            // Notify UI or other systems
            OnLevelUp();
        }
    }

    private void OnLevelUp()
    {
        // Called when character levels up
        // You can add level-up effects, sounds, etc. here
        if (currentLevel >= 3)
        {
            Debug.Log($"{gameObject.name} reached level 3! Can now destroy cannons!");
        }
    }

    public bool CanDestroyObjects()
    {
        return currentLevel >= 3;
    }
}
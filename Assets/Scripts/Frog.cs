using UnityEngine;
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]

/// <summary>
/// Frog character controller with two color states, jump animation,
/// rain drop particle effects, and croak sound.
/// Attach this script to your Frog GameObject in Unity.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Frog : MonoBehaviour
{
    public enum FrogColor { Green, Black }

    [Header("Frog Settings")]
    public FrogColor currentColor = FrogColor.Green;
    public Material greenMaterial;
    public Material blackMaterial;
    public Renderer frogRenderer;

    [Header("Jump Settings")]
    public float jumpForce = 5f;
    public ParticleSystem rainDropEffect;
    public AudioClip croakSound;

    private Animator animator;
    private AudioSource audioSource;
    private Rigidbody rb;

    void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();

        if (frogRenderer == null)
        {
            frogRenderer = GetComponentInChildren<Renderer>();
        }

        ApplyColor();
    }

    void Update()
    {
        // Example: Press Space to jump
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }

        // Example: Press C to toggle color
        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleColor();
        }
    }

    /// <summary>
    /// Makes the frog jump, plays animation, particles, and sound.
    /// </summary>
    public void Jump()
    {
        if (rb != null)
        {
            // reset vertical velocity (use linearVelocity instead of obsolete velocity)
            var lv = rb.linearVelocity;
            lv = new Vector3(lv.x, 0, lv.z);
            rb.linearVelocity = lv;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        if (animator != null)
        {
            animator.SetTrigger("Jump"); // Animator must have a "Jump" trigger
        }

        if (rainDropEffect != null)
        {
            rainDropEffect.Play();
        }

        PlayCroak();
    }

    /// <summary>
    /// Plays the croak sound.
    /// </summary>
    private void PlayCroak()
    {
        if (croakSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(croakSound);
        }
    }

    /// <summary>
    /// Switches between green and black colors.
    /// </summary>
    public void ToggleColor()
    {
        currentColor = (currentColor == FrogColor.Green) ? FrogColor.Black : FrogColor.Green;
        ApplyColor();
    }

    /// <summary>
    /// Applies the current color to the frog's material.
    /// </summary>
    private void ApplyColor()
    {
        if (frogRenderer != null)
        {
            if (currentColor == FrogColor.Green && greenMaterial != null)
            {
                frogRenderer.material = greenMaterial;
            }
            else if (currentColor == FrogColor.Black && blackMaterial != null)
            {
                frogRenderer.material = blackMaterial;
            }
        }
    }
}


using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private SpriteRenderer glowSpriteRenderer;
    [SerializeField] private Color activatedColor = Color.green; // You can change this in the Inspector
    [SerializeField] private AudioClip activationSoundOneShot; // Sound A: Plays once on activation
    [SerializeField] private AudioClip activationSoundLoop;    // Sound B: Loops after activation

    private bool isActivated = false;
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false; // Ensure it doesn't play automatically
        audioSource.loop = false; // Default to not looping
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActivated && other.CompareTag("Player"))
        {
            ActivateCheckpoint();
        }
    }

    private void ActivateCheckpoint()
    {
        isActivated = true;

        if (glowSpriteRenderer != null)
        {
            glowSpriteRenderer.color = activatedColor;
        }

        // Play Sound A once
        if (activationSoundOneShot != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSoundOneShot);
        }

        // Start looping Sound B after Sound A (or immediately if Sound A is null)
        if (activationSoundLoop != null && audioSource != null)
        {
            audioSource.clip = activationSoundLoop;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetActiveCheckpoint(transform.position);
        }
    }
}

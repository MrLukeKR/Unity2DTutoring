using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class CharacterAnimator : MonoBehaviour
{
    public enum CharacterState { Idle, Walk, Run, Attack, Dead, Hurt }
    public CharacterState currentState = CharacterState.Idle;

    [System.Serializable]
    public class AnimationData
    {
        public CharacterState state;
        public Texture2D spriteStrip;
        public int frameCount;
        public float frameRate = 0.1f;
    }

    [System.Serializable]
    public class AudioData
    {
        public CharacterState state;
        public List<AudioClip> audio;
        public bool isLoop;
    }

    public List<AnimationData> animations;
    public List<AudioData> sfx;
    public float moveSpeed = 2f;
    public float health = 100f;

    private Dictionary<CharacterState, Sprite[]> animationFrames;
    private Dictionary<CharacterState, AudioClip[]> sfxClips;
    private Dictionary<CharacterState, bool> sfxLoopable;

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float timer;
    private bool facingRight = true;
    private Rigidbody2D rb;

    private bool isAttacking = false;
    private bool isDead = false;
    private bool hurtAnimationPlayed = false;

    public Image healthBarImage;

    private AudioSource audioSource;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        LoadAnimations();
        LoadSFX();
        StartCoroutine(Animate());
    }

    private void LoadAnimations()
    {
        animationFrames = new Dictionary<CharacterState, Sprite[]>();

        foreach (var anim in animations)
        {
            animationFrames[anim.state] = LoadSpriteStrip(anim.spriteStrip, anim.frameCount);
        }
    }

    private void LoadSFX()
    {
        sfxClips = new Dictionary<CharacterState, AudioClip[]>();
        sfxLoopable = new Dictionary<CharacterState, bool>();

        foreach (var clip in sfx)
        {
            sfxClips[clip.state] = clip.audio.ToArray();
            sfxLoopable[clip.state] = clip.isLoop;
        }
    }

    private Sprite[] LoadSpriteStrip(Texture2D spriteStrip, int frameCount)
    {
        int frameWidth = spriteStrip.width / frameCount;
        int frameHeight = spriteStrip.height;
        Sprite[] frames = new Sprite[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = Sprite.Create(spriteStrip, new Rect(i * frameWidth, 0, frameWidth, frameHeight), new Vector2(0.5f, 0.5f));
        }

        return frames;
    }

    private IEnumerator Animate()
    {
        while (true)
        {
            if (animationFrames.ContainsKey(currentState))
            {
                var frames = animationFrames[currentState];
                spriteRenderer.sprite = frames[currentFrame];

                currentFrame = (currentFrame + 1) % frames.Length;

                // If in attack state, check if animation is complete and reset flag
                if (currentFrame == 0)
                {
                    isAttacking = false;
                }

                // If dead, stop the animation after the dead animation finishes
                if (currentState == CharacterState.Dead && currentFrame == 0)
                {
                    isDead = true;  // Mark the character as dead
                    break;  // Stop the animation loop
                }

                if (currentState == CharacterState.Hurt && !hurtAnimationPlayed)
                {
                        if (currentFrame == 0)
                            hurtAnimationPlayed = true;  // Mark the animation as played
                        if (health <= 0f)
                        {
                            SetState(CharacterState.Dead);  // Transition to Dead if health is zero
                        }
                        else
                        {
                            SetState(CharacterState.Idle);  // Return to idle or other state if not dead
                        }
                }

               
            }

            yield return new WaitForSeconds(GetCurrentFrameRate());
        }
    }

    private float GetCurrentFrameRate()
    {
        foreach (var anim in animations)
        {
            if (anim.state == currentState)
                return anim.frameRate;
        }
        return 0.1f; // Default fallback
    }

    private void Update()
    {
        if (isDead) return;

        HandleMovement();
        UpdateHealthBar();
    }

    private void HandleMovement()
    {
        if (currentState == CharacterState.Dead || currentState == CharacterState.Hurt)
        {
            return; // Don't process movement while attacking or dead
        }

        float moveInput = Input.GetAxisRaw("Horizontal");

        if (moveInput > 0)
        {
            rb.linearVelocity = new Vector2(moveSpeed, rb.linearVelocity.y);
            if (!facingRight)
                Flip();
            if (Input.GetKey(KeyCode.LeftShift))
                SetState(CharacterState.Run);
            else
                SetState(CharacterState.Walk);
        }
        else if (moveInput < 0)
        {
            rb.linearVelocity = new Vector2(-moveSpeed, rb.linearVelocity.y);
            if (facingRight)
                Flip();
            if (!isAttacking && health > 0)
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    SetState(CharacterState.Run);
                else
                    SetState(CharacterState.Walk);
            }
        }
        else
        {
            if (!isAttacking && health > 0)
            {
                SetState(CharacterState.Idle);
            }
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            moveSpeed = 4f;
            if (!isAttacking && health > 0)
            {
                SetState(CharacterState.Run);
            }
        }
        else
        {
            moveSpeed = 2f;
        }

        if (Input.GetKeyDown(KeyCode.Space) && !isAttacking)  // Prevent attack spam
        {
            SetState(CharacterState.Attack);
            isAttacking = true;  // Set attacking flag
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            health = 0;
            SetState(CharacterState.Hurt);
        }
        
        if (Input.GetKeyDown(KeyCode.H))  // Example input to trigger Hurt state
        {
            health -= 10;
            SetState(CharacterState.Hurt);
        }

    }

    private void PlaySFX()
    {
        if (sfxClips.ContainsKey(currentState))
        {
            var clips = sfxClips[currentState];  // Get the array of sound clips for the current state
            var loopable = sfxLoopable[currentState];
            var clipsCount = clips.Length;

            if (clipsCount > 0)
            {
                // Pick a random clip index
                int randomIndex = Random.Range(0, clipsCount);
                AudioClip selectedClip = clips[randomIndex];
                // Play the selected clip using the AudioSource

                if (audioSource != null)
                {
                    if (loopable)
                    {
                        if (audioSource.isPlaying && audioSource.loop)
                        {
                            audioSource.Stop();  // Stop any currently playing sound
                        }

                        audioSource.clip = selectedClip;
                        audioSource.loop = true;  // Set looping
                        audioSource.Play();  // Play the loopable sound
                    }
                    else
                    {
                        audioSource.loop = false;
                        audioSource.PlayOneShot(selectedClip);
                    }
                }
            }
        } 
        else
        {
            if (audioSource.isPlaying && audioSource.loop)  // Set looping)
            {
                audioSource.loop = false;
                audioSource.Stop();  // Stop any currently playing sound
            }
        }

    }

    private void SetState(CharacterState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            currentFrame = 0;
            hurtAnimationPlayed = false;
            PlaySFX();
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        transform.localScale = new Vector3(facingRight ? 2 : -2, 2, 2);
    }

    private void UpdateHealthBar()
    {
        if (healthBarImage != null)
        {
            healthBarImage.fillAmount = health / 100f;  // Convert health to fillAmount (0 to 1 range)
        }
    }
}

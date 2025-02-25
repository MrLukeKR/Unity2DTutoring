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
    public float runSpeed = 4f;
    public float health = 100f;

    [Header("Level Boundaries")]
    public float leftBoundary = -10f;    // Left-most point the player can go
    public float rightBoundary = 10f;    // Right-most point the player can go

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

    // Exposed to allow camera to check if player is moving
    [HideInInspector]
    public Vector2 currentVelocity;

    // Reference to camera controller to get boundaries from
    private CameraController cameraController;


    [Header("Combat Settings")]
    public float attackDamage = 20f;
    public float attackRadius = 1.2f;
    public LayerMask enemyLayers;
    private bool hasDealtDamage = false;


    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        cameraController = Camera.main.GetComponent<CameraController>();

        // If camera controller exists, use its boundaries
        if (cameraController != null)
        {
            leftBoundary = cameraController.leftBoundary;
            rightBoundary = cameraController.rightBoundary;
        }

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
                    if (currentState == CharacterState.Attack)
                        isAttacking = false;
                    hasDealtDamage = false;
                }
                else if (currentState == CharacterState.Attack && currentFrame == frames.Length / 2)
                {
                    // Check for hits at the middle of the attack animation
                    CheckAttackHit();
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
                    {
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
            rb.linearVelocity = Vector2.zero;
            currentVelocity = Vector2.zero;
            return; // Don't process movement while attacking or dead
        }

        float moveInput = Input.GetAxisRaw("Horizontal");
        float jumpInput = Input.GetAxisRaw("Vertical");
        Vector2 targetVelocity = Vector2.zero;

        if(jumpInput != 0)
        {
            targetVelocity = new Vector2(rb.linearVelocity.x, jumpInput);
        }

        if (moveInput != 0 && !isAttacking)
        {
            float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : moveSpeed;

            // Check boundaries before moving
            bool canMoveLeft = transform.position.x > leftBoundary && moveInput < 0;
            bool canMoveRight = transform.position.x < rightBoundary && moveInput > 0;

            if (canMoveLeft || canMoveRight)
            {
                targetVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

                if ((moveInput > 0 && !facingRight) || (moveInput < 0 && facingRight))
                    Flip();

                if (Input.GetKey(KeyCode.LeftShift))
                    SetState(CharacterState.Run);
                else
                    SetState(CharacterState.Walk);
            }
            else
            {
                // At boundary but trying to move further - set to idle
                targetVelocity = new Vector2(0, rb.linearVelocity.y);
                SetState(CharacterState.Idle);
            }
        }
        else if (!isAttacking && health > 0)
        {
            targetVelocity = new Vector2(0, rb.linearVelocity.y);
            SetState(CharacterState.Idle);
        }

        rb.linearVelocity = targetVelocity;
        currentVelocity = targetVelocity;

        // Handle attack input
        if (Input.GetKeyDown(KeyCode.Space) && !isAttacking)
        {
            SetState(CharacterState.Attack);
            isAttacking = true;
        }

        // Debug inputs
        if (Input.GetKeyDown(KeyCode.K))
        {
            health = 0;
            SetState(CharacterState.Hurt);
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            health -= 10;
            if (health < 0) health = 0;
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
            if (audioSource.isPlaying && audioSource.loop)
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

    void OnDrawGizmosSelected()
    {
        // Draw character boundaries for debugging
        Gizmos.color = Color.yellow;
        Vector3 position = transform.position;
        Gizmos.DrawLine(new Vector3(leftBoundary, position.y + 1, 0), new Vector3(leftBoundary, position.y - 1, 0));
        Gizmos.DrawLine(new Vector3(rightBoundary, position.y + 1, 0), new Vector3(rightBoundary, position.y - 1, 0));

        if (currentState == CharacterState.Attack)
        {
            Gizmos.color = Color.red;
            Vector2 attackPos = new Vector2(
                transform.position.x + (facingRight ? attackRadius / 2 : -attackRadius / 2),
                transform.position.y
            );
            Gizmos.DrawWireSphere(attackPos, attackRadius);
        }
    }

    public void TakeDamage(float damage)
    {
        // Only take damage if not already dead or in hurt state
        if (currentState != CharacterState.Dead && currentState != CharacterState.Hurt)
        {
            health -= damage;
            if (health < 0) health = 0;
            SetState(CharacterState.Hurt);

            // Play hurt sound if available
            if (audioSource != null && sfxClips.ContainsKey(CharacterState.Hurt))
            {
                var clips = sfxClips[CharacterState.Hurt];
                if (clips.Length > 0)
                {
                    int randomIndex = Random.Range(0, clips.Length);
                    audioSource.PlayOneShot(clips[randomIndex]);
                }
            }
        }
    }

    private void CheckAttackHit()
    {
        if (currentState == CharacterState.Attack && !hasDealtDamage)
        {
            // Calculate attack position (in front of the character based on facing direction)
            Vector2 attackPos = new Vector2(
                transform.position.x + (facingRight ? attackRadius / 2 : -attackRadius / 2),
                transform.position.y
            );

            // Find all enemies in attack radius
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPos, attackRadius, enemyLayers);

            // Damage each enemy found
            foreach (Collider2D enemy in hitEnemies)
            {
                // Check if it's an NPC
                NPC npc = enemy.GetComponent<NPC>();
                if (npc != null)
                {
                    npc.TakeDamage(attackDamage);
                    hasDealtDamage = true;
                } 
            }
        }
    }
}
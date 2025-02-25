using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class NPC : MonoBehaviour
{
    public enum CharacterState { Idle, Walk, Run, Attack, Dead, Hurt }
    public CharacterState currentState = CharacterState.Idle;

    [Header("Combat Settings")]
    public float attackDamage = 20f;

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
    public float leftBoundary = -10f;
    public float rightBoundary = 10f;

    [Header("AI Settings")]
    public float detectionRange = 5f;
    public float attackRange = 1.5f;
    public float idleTimeMin = 2f;
    public float idleTimeMax = 5f;
    public float wanderTimeMin = 3f;
    public float wanderTimeMax = 7f;
    public Transform playerTransform;
    public float attackCooldown = 1.5f;

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
    private float lastAttackTime = 0f;

    // AI variables
    private enum AIState { Idle, Wander, Chase, Attack }
    private AIState currentAIState = AIState.Idle;
    private float stateTimer = 0f;
    private int moveDirection = 0; // -1 = left, 0 = none, 1 = right
    private float distanceToPlayer = Mathf.Infinity;

    public Image healthBarImage;

    private AudioSource audioSource;

    // Exposed to allow camera to check if NPC is moving
    [HideInInspector]
    public Vector2 currentVelocity;

    // Reference to camera controller to get boundaries from
    private CameraController cameraController;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        cameraController = Camera.main.GetComponent<CameraController>();

        // Find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        // If camera controller exists, use its boundaries
        if (cameraController != null)
        {
            leftBoundary = cameraController.leftBoundary;
            rightBoundary = cameraController.rightBoundary;
        }

        LoadAnimations();
        LoadSFX();
        StartCoroutine(Animate());

        // Start with random idle time
        stateTimer = Random.Range(idleTimeMin, idleTimeMax);
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

        UpdateAI();
        UpdateHealthBar();
    }

    private void UpdateAI()
    {
        if (currentState == CharacterState.Dead || currentState == CharacterState.Hurt)
        {
            rb.linearVelocity = Vector2.zero;
            currentVelocity = Vector2.zero;
            return;
        }

        // If we're attacking, don't process AI movement until attack is complete
        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            currentVelocity = Vector2.zero;
            return;
        }

        // Check distance to player if player exists
        if (playerTransform != null)
        {
            distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        }
        else
        {
            distanceToPlayer = Mathf.Infinity;
        }

        // Update state timer
        stateTimer -= Time.deltaTime;

        // Determine AI state based on player distance
        if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            currentAIState = AIState.Attack;
        }
        else if (distanceToPlayer <= detectionRange)
        {
            currentAIState = AIState.Chase;
        }
        else
        {
            // Only change wandering states if timer has expired
            if (stateTimer <= 0)
            {
                if (currentAIState == AIState.Idle)
                {
                    currentAIState = AIState.Wander;
                    stateTimer = Random.Range(wanderTimeMin, wanderTimeMax);
                    // Choose random direction: -1 (left) or 1 (right)
                    moveDirection = Random.Range(0, 2) * 2 - 1;
                }
                else if (currentAIState == AIState.Wander)
                {
                    currentAIState = AIState.Idle;
                    stateTimer = Random.Range(idleTimeMin, idleTimeMax);
                    moveDirection = 0;
                }
            }
        }

        // Execute behavior based on current AI state
        switch (currentAIState)
        {
            case AIState.Idle:
                HandleIdleState();
                break;
            case AIState.Wander:
                HandleWanderState();
                break;
            case AIState.Chase:
                HandleChaseState();
                break;
            case AIState.Attack:
                HandleAttackState();
                break;
        }
    }

    private void HandleIdleState()
    {
        rb.linearVelocity = Vector2.zero;
        currentVelocity = Vector2.zero;
        SetState(CharacterState.Idle);
    }

    private void HandleWanderState()
    {
        // Check boundaries before moving
        bool canMoveLeft = transform.position.x > leftBoundary && moveDirection < 0;
        bool canMoveRight = transform.position.x < rightBoundary && moveDirection > 0;

        if ((moveDirection < 0 && !canMoveLeft) || (moveDirection > 0 && !canMoveRight))
        {
            // Hit a boundary, reverse direction
            moveDirection *= -1;
        }

        // Set facing direction based on movement
        if ((moveDirection > 0 && !facingRight) || (moveDirection < 0 && facingRight))
        {
            Flip();
        }

        // Move in the chosen direction
        Vector2 targetVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
        rb.linearVelocity = targetVelocity;
        currentVelocity = targetVelocity;
        SetState(CharacterState.Walk);
    }

    private void HandleChaseState()
    {
        if (playerTransform != null)
        {
            // Determine direction to player
            float directionToPlayer = Mathf.Sign(playerTransform.position.x - transform.position.x);

            // Check boundaries before moving
            bool canMoveLeft = transform.position.x > leftBoundary && directionToPlayer < 0;
            bool canMoveRight = transform.position.x < rightBoundary && directionToPlayer > 0;

            // If we can move in the player's direction
            if ((directionToPlayer < 0 && canMoveLeft) || (directionToPlayer > 0 && canMoveRight))
            {
                // Set facing direction
                if ((directionToPlayer > 0 && !facingRight) || (directionToPlayer < 0 && facingRight))
                {
                    Flip();
                }

                // Move towards player at run speed
                Vector2 targetVelocity = new Vector2(directionToPlayer * runSpeed, rb.linearVelocity.y);
                rb.linearVelocity = targetVelocity;
                currentVelocity = targetVelocity;
                SetState(CharacterState.Run);
            }
            else
            {
                // At boundary but player is beyond - stop and wait
                rb.linearVelocity = Vector2.zero;
                currentVelocity = Vector2.zero;
                SetState(CharacterState.Idle);
            }
        }
    }

 

    private void PlaySFX()
    {
        if (sfxClips.ContainsKey(currentState))
        {
            var clips = sfxClips[currentState];
            var loopable = sfxLoopable[currentState];
            var clipsCount = clips.Length;

            if (clipsCount > 0)
            {
                int randomIndex = Random.Range(0, clipsCount);
                AudioClip selectedClip = clips[randomIndex];

                if (audioSource != null)
                {
                    if (loopable)
                    {
                        if (audioSource.isPlaying && audioSource.loop)
                        {
                            audioSource.Stop();
                        }

                        audioSource.clip = selectedClip;
                        audioSource.loop = true;
                        audioSource.Play();
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
                audioSource.Stop();
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
            healthBarImage.fillAmount = health / 100f;
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentState != CharacterState.Dead && currentState != CharacterState.Hurt)
        {
            health -= damage;
            if (health < 0) health = 0;
            SetState(CharacterState.Hurt);

            // Optional: Add knockback effect
            if (playerTransform != null)
            {
                Vector2 knockbackDirection = (transform.position - playerTransform.position).normalized;
                rb.AddForce(knockbackDirection * 5f, ForceMode2D.Impulse);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw character boundaries for debugging
        Gizmos.color = Color.yellow;
        Vector3 position = transform.position;
        Gizmos.DrawLine(new Vector3(leftBoundary, position.y + 1, 0), new Vector3(leftBoundary, position.y - 1, 0));
        Gizmos.DrawLine(new Vector3(rightBoundary, position.y + 1, 0), new Vector3(rightBoundary, position.y - 1, 0));

        // Draw detection and attack ranges
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, detectionRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(position, attackRange);
    }

    private void HandleAttackState()
    {
        // Keep existing code for facing and movement

        // Stop movement and attack
        rb.linearVelocity = Vector2.zero;
        currentVelocity = Vector2.zero;
        SetState(CharacterState.Attack);
        isAttacking = true;
        lastAttackTime = Time.time;

        // Check if player is in range and damage them
        if (playerTransform != null && distanceToPlayer <= attackRange)
        {
            CharacterAnimator player = playerTransform.GetComponent<CharacterAnimator>();
            if (player != null)
            {
                player.TakeDamage(20f); // Use a fixed damage amount for now
            }
        }
    }

}
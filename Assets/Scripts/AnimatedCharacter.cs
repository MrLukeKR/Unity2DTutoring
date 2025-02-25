using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    public List<AnimationData> animations;
    public float moveSpeed = 2f;
    public float health = 100f;

    private Dictionary<CharacterState, Sprite[]> animationFrames;
    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float timer;
    private bool facingRight = true;
    private Rigidbody2D rb;

    private bool isAttacking = false;
    private bool isDead = false;
    private bool hurtAnimationPlayed = false;

    public Image healthBarImage;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        LoadAnimations();
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
                SetState(CharacterState.Dead);
        }
        
        if (Input.GetKeyDown(KeyCode.H))  // Example input to trigger Hurt state
        {
            health -= 10;
            SetState(CharacterState.Hurt);
        }

    }

    private void SetState(CharacterState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            currentFrame = 0;
            hurtAnimationPlayed = false;
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

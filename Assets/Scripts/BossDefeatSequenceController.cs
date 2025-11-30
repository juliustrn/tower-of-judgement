using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BossDefeatSequenceController : MonoBehaviour
{
    [Header("Door References")]
    [Tooltip("Transform của door để camera focus vào")]
    public Transform doorTarget;
    
    [Tooltip("Animator của door để trigger OpenDoor")]
    public Animator doorAnimator;
    
    [Header("Player References")]
    [Tooltip("Transform của player để điều khiển movement")]
    public Transform playerTransform;
    
    [Tooltip("PlayerController để freeze/unfreeze player")]
    public PlayerController playerController;
    
    [Header("Cutscene Settings")]
    [Tooltip("Vị trí player sẽ đi đến (white space sau door)")]
    public Transform walkTargetPosition;
    
    [Tooltip("Tốc độ player đi bộ trong cutscene")]
    public float walkSpeed = 2f;
    
    [Tooltip("Thời gian chờ sau khi door mở trước khi player bắt đầu đi")]
    public float delayAfterDoorOpen = 1f;
    
    [Header("Scene Transition")]
    [Tooltip("Tên scene sẽ chuyển đến sau khi cutscene kết thúc")]
    public string nextSceneName;
    
    [Tooltip("Thời gian chờ trước khi chuyển scene (sau khi player đến vị trí)")]
    public float delayBeforeSceneTransition = 1f;
    
    private Rigidbody2D playerRb;
    private Animator playerAnimator;
    private bool isSequenceActive = false;

    private void Awake()
    {
        // Tự động tìm references nếu chưa được assign
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
        
        if (playerTransform == null && playerController != null)
            playerTransform = playerController.transform;
        
        if (playerTransform != null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody2D>();
            playerAnimator = playerTransform.GetComponentInChildren<Animator>();
        }
        
        if (doorAnimator == null)
        {
            // Tìm door animator trong scene
            GameObject doorParent = GameObject.Find("DoorParent");
            if (doorParent != null)
            {
                doorAnimator = doorParent.GetComponent<Animator>();
                if (doorAnimator == null)
                {
                    // Thử tìm trong children
                    doorAnimator = doorParent.GetComponentInChildren<Animator>();
                }
            }
        }
        
        if (doorTarget == null && doorAnimator != null)
        {
            doorTarget = doorAnimator.transform;
        }
    }

    /// <summary>
    /// Bắt đầu sequence sau khi dialogue kết thúc
    /// Gọi từ timeline hoặc BossDamageable
    /// </summary>
    public void StartBossDefeatSequence()
    {
        if (isSequenceActive) return;
        
        isSequenceActive = true;
        StartCoroutine(BossDefeatSequenceRoutine());
    }

    private IEnumerator BossDefeatSequenceRoutine()
    {
        Debug.Log("[BossDefeatSequence] Bắt đầu sequence sau boss fight");
        
        // 1. Freeze player
        if (playerController != null)
        {
            playerController.canMove = false;
            playerController.canAttack = false;
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }
            if (playerAnimator != null)
            {
                playerAnimator.SetBool("isMoving", false);
            }
        }
        
        // Freeze game
        GameFreezeManager.Instance?.FreezeGame();
        
        // 2. Move camera to door
        if (doorTarget != null && CameraFocusController.Instance != null)
        {
            CameraFocusController.Instance.FocusOnTarget(doorTarget);
            yield return new WaitForSecondsRealtime(1f);
        }
        
        // 3. Play door opening animation
        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger("OpenDoor");
            Debug.Log("[BossDefeatSequence] Triggered OpenDoor animation");
            yield return new WaitForSecondsRealtime(1.5f); // Chờ animation hoàn thành
        }
        else
        {
            Debug.LogWarning("[BossDefeatSequence] Door animator không được tìm thấy!");
            yield return new WaitForSecondsRealtime(1.5f);
        }
        
        // 4. Return camera to player
        if (CameraFocusController.Instance != null)
        {
            CameraFocusController.Instance.ReturnToPlayer();
            yield return new WaitForSecondsRealtime(0.5f);
        }
        
        // 5. Unfreeze game để player có thể di chuyển
        GameFreezeManager.Instance?.UnfreezeGame();
        
        // Chờ một chút trước khi player bắt đầu đi
        yield return new WaitForSecondsRealtime(delayAfterDoorOpen);
        
        // 6. Cutscene: Player đi bộ về phía door
        if (walkTargetPosition != null && playerTransform != null && playerRb != null)
        {
            yield return StartCoroutine(WalkPlayerToTarget());
        }
        else
        {
            Debug.LogWarning("[BossDefeatSequence] Walk target hoặc player không được tìm thấy!");
        }
        
        // 7. Chờ một chút trước khi chuyển scene
        yield return new WaitForSecondsRealtime(delayBeforeSceneTransition);
        
        // 8. Transfer to new scene
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log($"[BossDefeatSequence] Chuyển đến scene: {nextSceneName}");
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[BossDefeatSequence] Next scene name không được set!");
        }
    }

    private IEnumerator WalkPlayerToTarget()
    {
        if (playerTransform == null || walkTargetPosition == null || playerRb == null)
            yield break;
        
        Debug.Log("[BossDefeatSequence] Player bắt đầu đi bộ về phía door");
        
        // Enable player movement nhưng chỉ cho phép đi về phía target
        if (playerController != null)
        {
            playerController.canMove = true;
        }
        
        Vector2 targetPos = walkTargetPosition.position;
        float distanceThreshold = 0.1f;
        
        while (Vector2.Distance(playerTransform.position, targetPos) > distanceThreshold)
        {
            // Tính toán hướng di chuyển
            Vector2 direction = (targetPos - (Vector2)playerTransform.position).normalized;
            
            // Di chuyển player
            playerRb.linearVelocity = direction * walkSpeed;
            
            // Update animator
            if (playerAnimator != null)
            {
                playerAnimator.SetFloat("moveX", direction.x);
                playerAnimator.SetFloat("moveY", direction.y);
                playerAnimator.SetBool("isMoving", true);
            }
            
            yield return null;
        }
        
        // Dừng player khi đến đích
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
        }
        
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isMoving", false);
        }
        
        if (playerController != null)
        {
            playerController.canMove = false;
        }
        
        Debug.Log("[BossDefeatSequence] Player đã đến vị trí target");
    }
}

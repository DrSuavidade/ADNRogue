using UnityEngine;

public class AnimatorSyncer : MonoBehaviour
{
    public Animator masterAnimator; // Drag the BOSS Animator here
    private Animator myAnimator;    // The STAFF Animator
    private int _lastStateHash;

    void Awake()
    {
        myAnimator = GetComponent<Animator>();
        if (myAnimator != null)
        {
            // IMPORTANT: Disable root motion so the staff doesn't try to move 
            // independently of the boss's hand bone!
            myAnimator.applyRootMotion = false;
        }
    }

    void Update()
    {
        if (masterAnimator != null && myAnimator != null)
        {
            // Mirror parameters used in Blend Trees every frame
            // Without this, the staff stays in the "Idle" part of the blend tree!
            float moveY = masterAnimator.GetFloat("MoveY");
            myAnimator.SetFloat("MoveY", moveY);

            // Only force the animation state when it actually changes (e.g., from Walking to Attacking)
            // This prevents the "double movement" jitters by letting the animation play naturally
            var masterState = masterAnimator.GetCurrentAnimatorStateInfo(0);
            if (masterState.fullPathHash != _lastStateHash)
            {
                _lastStateHash = masterState.fullPathHash;
                myAnimator.Play(_lastStateHash, 0, masterState.normalizedTime);
            }
        }
    }
}

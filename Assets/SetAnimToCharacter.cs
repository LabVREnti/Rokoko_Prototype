using System.Collections.Generic;
using UnityEngine;

public class SetAnimToCharacter : MonoBehaviour
{
    [SerializeField] private Animator teacherAnimator;
    [SerializeField] private Animator studentAnimator;

    [Header("Animations to apply")]
    [SerializeField] private AnimationClip teacherAnim;
    [SerializeField] private AnimationClip studentAnim;

    void Start()
    {
        
    }

    private void OnValidate()
    {
        if (teacherAnimator && teacherAnim)
        {
            AnimatorOverrideController aoc = new AnimatorOverrideController(teacherAnimator.runtimeAnimatorController);
            var anims = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var a in aoc.animationClips)
                anims.Add(new KeyValuePair<AnimationClip, AnimationClip>(a, teacherAnim));
            aoc.ApplyOverrides(anims);
            teacherAnimator.runtimeAnimatorController = aoc;
        } 

        if (studentAnimator && studentAnim)
        {
            AnimatorOverrideController aoc = new AnimatorOverrideController(studentAnimator.runtimeAnimatorController);
            var anims = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var a in aoc.animationClips)
                anims.Add(new KeyValuePair<AnimationClip, AnimationClip>(a, studentAnim));
            aoc.ApplyOverrides(anims);
            studentAnimator.runtimeAnimatorController = aoc;
        }
    }
}

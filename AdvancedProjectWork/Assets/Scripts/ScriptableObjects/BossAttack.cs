using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBossAttack", menuName = "Boss/Attack", order = 2)]
public class BossAttack : ScriptableObject {
    [Header("Visuals and Animation")]
    public Material _attackMaterial;
    public float _attackSpeed;
    public string _telegraphAnimationTrigger;

    [Header("Combat Values")]
    public float _attackChargeAmount;

    [Header("Audio")]
    public AudioClip _telegraphAudio;
    public float _audioScale;
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBossAttack", menuName = "Boss/Phase", order = 1)]
public class BossPhase : ScriptableObject {
    public float _attackCooldown = 5f;
    public bool _containsMovement;
    public float _movementSpeed = 1f;
    public string _transitionFunctionName = "";
    public Vector2Int _healthThreshold;
    public List<BossAttack> _bossAttacks = new List<BossAttack>();
    public bool _randomAttackOrder;

    // COULD REFACTOR: Feels a bit redundant with _bossAttacks present, but it works for now
    public List<BossAttack> _attackOrder = new List<BossAttack>();

    public bool IsWithinPhaseThreshold(float healthValue)
    {
        return (healthValue >= _healthThreshold.x && healthValue <= _healthThreshold.y);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowManager : MonoBehaviour
{
    public float _movementSpeed = 5f;
    public BossManager _bossManager;

    private Transform _currentTarget;

    void Update()
    {
        CheckForTargets();
        if(_currentTarget == null)
        {
            return;
        }

        transform.LookAt(_currentTarget, Vector3.up);
    }

    private void CheckForTargets()
    {
        foreach(var projectile in _bossManager._projectiles)
        {
            if(projectile != _currentTarget && DistanceToPlayer(projectile.transform) < DistanceToPlayer(_currentTarget))
            {
                _currentTarget = projectile.transform;
            }
        }
        if(_currentTarget == null)
        {
            _currentTarget = _bossManager._bossContainer.transform;
        }
    }

    private float DistanceToPlayer(Transform transform)
    {
        return transform != null ? (_bossManager._playerHead.position - transform.position).magnitude : 100000f;
    }
}

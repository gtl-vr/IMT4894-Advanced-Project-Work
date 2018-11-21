using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireflyManager : MonoBehaviour
{
    public float _movementSpeed = 5f;
    public BossManager _bossManager;
    public float _offsetFromTarget = 5f;
    public float _errorRange = 0.5f;

    private Transform _currentTarget;

    void Update()
    {
        CheckForTargets();
        if(_currentTarget == null)
        {
            return;
        }
        
        // A direction that is perpendicular towards the left with the vector between the player and the target
        var perpendicularOffset = Vector3.Cross(_bossManager._playerHead.position - transform.position, Vector3.up).normalized * _offsetFromTarget;

        // Rather mathsy, but the gist is that the firefly will move to the left of its target relative when you are looking at it.
        // The if test just checks whether the firefly is "close enough" to the target so we dont have to worry about potential jitter.
        if (((_currentTarget.transform.position + perpendicularOffset) - transform.position).magnitude > _errorRange)
        {
            var movementDirection = ((_currentTarget.position - transform.position) + perpendicularOffset).normalized;
            transform.position += movementDirection * _movementSpeed * Time.deltaTime;
        }
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

﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour {
    public GameManager _gameManager;
    public float _maxBatonCharge = 100f;
    public MeshRenderer _batonRenderer;
    public float _chargedAnimationSpeed = 5f;
    public float _pointerLineLength = 50f;
    public float _shotDamage = 50f;

    private float _currentCharge = 0f;

    private float _chargedAnimationTimer = 0f;
    private LineRenderer _batonLineRenderer;
    private Transform _pointerOrigin;

    private void Start()
    {
        // Needed for runtime modification of emission colour
        _batonRenderer.material.EnableKeyword("_EMISSION");
        _batonLineRenderer = _batonRenderer.transform.parent.parent.GetComponent<LineRenderer>();
        _batonLineRenderer.enabled = true;
        _pointerOrigin = _batonLineRenderer.transform.GetChild(0);
        _currentCharge = _maxBatonCharge;
    }

    private void Update()
    {
        if(_currentCharge >= _maxBatonCharge)
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                Shoot();
                return;
            }

            _chargedAnimationTimer += Time.deltaTime * _chargedAnimationSpeed;
            var sineTimer = Utilities.Remap(-1f, 1f, 0, 1, Mathf.Sin(_chargedAnimationTimer));

            // +1 is added to these calculations so the emission strength is a bit more noticeable
            _batonRenderer.material.SetColor("_EmissionColor", new Color(sineTimer, sineTimer, sineTimer, 1.0f) * (sineTimer+1));

            _batonLineRenderer.SetPosition(0, _pointerOrigin.position);
            _batonLineRenderer.SetPosition(1, _pointerOrigin.position + _pointerOrigin.forward * _pointerLineLength);
        }
    }

    public void AddCharge(float amount)
    {
        _currentCharge = Mathf.Clamp(amount + _currentCharge, 0, _maxBatonCharge);
        if(_currentCharge < _maxBatonCharge)
        {
            var remappedValue = Utilities.Remap(0, _maxBatonCharge, 0, 1, _currentCharge);
            _batonRenderer.material.SetColor("_EmissionColor", new Color(remappedValue, remappedValue, remappedValue, 1.0f) * (remappedValue+1));
        }
        else
        {
            _chargedAnimationTimer = 0f;
            _batonLineRenderer.enabled = true;
        }
    }

    private void Shoot()
    {
        RaycastHit hit;
        if(Physics.Raycast(_pointerOrigin.position, _pointerOrigin.forward, out hit, _pointerLineLength))
        {
            if(hit.collider.CompareTag("Boss"))
            {
                _batonLineRenderer.enabled = false;
                _currentCharge = 0;
                _gameManager.TakeDamage(_shotDamage);

                // Hacky way to quickly reset stuff :p
                AddCharge(0);

                // TODO: Play animation, sounds and stuff
            }
        }
    }
}

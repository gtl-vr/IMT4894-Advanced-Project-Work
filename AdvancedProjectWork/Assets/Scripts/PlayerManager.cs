﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Valve.VR;

public class PlayerManager : MonoBehaviour {
    public BossManager _bossManager;
    public float _maxBatonCharge = 100f;
    public MeshRenderer _batonRenderer;
    public float _chargedAnimationSpeed = 5f;
    public float _pointerLineLength = 50f;
    public float _shotDamage = 50f;
    public float _shotAnimationNoise = 1f;
    public float _takeDamageAnimationSpeed = 5f;

    [Header("Audio")]
    public AudioClip _attackSound;
    public AudioClip _absorbSound;
    public AudioClip _takeDamageSound;
    [HideInInspector] public AudioSource _audioSource;

    [Header("VR")]
    public SteamVR_Input_Sources _batonHand;

    private float _currentCharge = 0f;

    private float _chargedAnimationTimer = 0f;
    private LineRenderer _batonLineRenderer;
    private LineRenderer _attackLineRenderer;
    private Transform _pointerOrigin;
    private ParticleSystem _batonParticleSystem;
    private const float _DEBUG_HEAD_SPEED = 100;
    private Vignette _vignette;
    private Coroutine _takeDamageRoutine;


    private void Awake()
    {
        // Needed for runtime modification of emission colour
        _batonRenderer.material.EnableKeyword("_EMISSION");
        _batonLineRenderer = _batonRenderer.transform.parent.parent.GetChild(0).GetComponent<LineRenderer>();
        _batonLineRenderer.enabled = true;
        _pointerOrigin = _batonLineRenderer.transform;
        _attackLineRenderer = _batonRenderer.transform.parent.parent.GetChild(1).GetComponent<LineRenderer>();
        _batonParticleSystem = _pointerOrigin.transform.GetChild(0).GetComponent<ParticleSystem>();
        _audioSource = GetComponent<AudioSource>();

        _batonLineRenderer.SetPosition(0, _pointerOrigin.InverseTransformPoint(_pointerOrigin.position));
        _batonLineRenderer.SetPosition(1, _pointerOrigin.InverseTransformPoint(_pointerOrigin.position + _pointerOrigin.forward * _pointerLineLength));

        _vignette = GameObject.Find("PostProcessing").GetComponent<PostProcessVolume>().profile.GetSetting<Vignette>();

        AddCharge(_maxBatonCharge);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.P) || (SteamVR.active && SteamVR_Input._default.inActions.Teleport.GetStateDown(_batonHand)))
        {
            AddCharge(100);
        }

        if (Input.GetKey(KeyCode.A))
        {
            transform.Rotate(Vector3.up, Time.deltaTime * -_DEBUG_HEAD_SPEED);
        }

        if (Input.GetKey(KeyCode.D))
        {
            transform.Rotate(Vector3.up, Time.deltaTime * _DEBUG_HEAD_SPEED);
        }
#endif

        if (_currentCharge >= _maxBatonCharge)
        {
            if (Input.GetKeyDown(KeyCode.S) || (SteamVR.active && SteamVR_Input._default.inActions.GrabPinch.GetStateDown(_batonHand)))
            {
                Shoot();
                return;
            }

            _chargedAnimationTimer += Time.deltaTime * _chargedAnimationSpeed;
            var sineTimer = Utilities.Remap(-1f, 1f, 0, 1, Mathf.Sin(_chargedAnimationTimer));

            // +1 is added to these calculations so the emission strength is a bit more noticeable
            _batonRenderer.material.SetColor("_EmissionColor", new Color(sineTimer, sineTimer, sineTimer, 1.0f) * (sineTimer+1));
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

    public void TakeDamage()
    {
        _bossManager._amountOfPlayerHits++;
        if(_takeDamageRoutine != null)
        {
            StopCoroutine(_takeDamageRoutine);
        }
        _takeDamageRoutine = StartCoroutine(TakeDamageAnimation());
    }

    private void Shoot()
    {
        // To allow for infinite shots until the game has started
        if (_bossManager._gameActive)
        {
            ResetBaton();
        }
        _audioSource.PlayOneShot(_attackSound);

        var attackEndPoint = _pointerOrigin.position + _pointerOrigin.forward * _pointerLineLength;

        RaycastHit hit;
        if(Physics.Raycast(_pointerOrigin.position, _pointerOrigin.forward, out hit, _pointerLineLength))
        {
            if (hit.collider.CompareTag("Boss"))
            {
                // To make sure that the baton resets when it hits the boss for the first time
                if (!_bossManager._gameActive)
                {
                    ResetBaton();
                }

                _bossManager.TakeDamage(_shotDamage);
                attackEndPoint = hit.point;
            }
        }

        StartCoroutine(ShotAnimation(attackEndPoint));
    }

    private void ResetBaton()
    {
        _batonLineRenderer.enabled = false;
        _currentCharge = 0;
        // Hacky way to quickly reset stuff :p
        AddCharge(0);
    }

    private IEnumerator ShotAnimation(Vector3 endPoint)
    {
        // Generating the lineRenderer positions for the attack
        for (int i = 0; i < _attackLineRenderer.positionCount; i++)
        {
            var currentPosition = (i != 0) ? Vector3.Lerp(_pointerOrigin.position, endPoint, ((i + 1f) / _attackLineRenderer.positionCount)) : _pointerOrigin.position;
            if (i != 0)
            {
                currentPosition += new Vector3(Random.Range(-_shotAnimationNoise, _shotAnimationNoise), Random.Range(-_shotAnimationNoise, _shotAnimationNoise), Random.Range(-_shotAnimationNoise, _shotAnimationNoise));
            }
            _attackLineRenderer.SetPosition(i, currentPosition);
        }

        _attackLineRenderer.enabled = true;
        _batonParticleSystem.Play();
        yield return new WaitForSeconds(0.25f);
        _attackLineRenderer.enabled = false;
    }

    private IEnumerator TakeDamageAnimation()
    {
        var lerpTimer = 0f;
        while(Mathf.Sin(lerpTimer) >= 0f)
        {
            yield return null;
            lerpTimer += Time.deltaTime * _takeDamageAnimationSpeed;
            _vignette.intensity.value = Mathf.Lerp(0f, 0.5f, Mathf.Clamp(Mathf.Sin(lerpTimer), 0, 1));
        }

        _vignette.intensity.value = 0f;
    }
}

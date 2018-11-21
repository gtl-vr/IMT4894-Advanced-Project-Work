using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BossManager : MonoBehaviour
{
    [Header("MetaInformation")]
    public GameObject _playerObject;
    public GameObject _bossContainer;
    public GameObject _projectilePrefab;
    public Transform _bossRotationPivot;
    public GameObject _eyesObject;
    public Image _bossHealthBar;

    [Header("GameSettings")]
    public float _attackCooldown = 2f;
    public float _bossMovementSpeed = 5f;
    public float _offsetFromPivot = 10f;
    public float _bossHealth = 100f;

    [Header("Audio")]
    public AudioClip _gameStartAudio;
    public AudioClip _gameSoundtrack;
    public AudioClip _poofSound;
    public AudioClip _impactSound;
    public AudioClip _throwSound;
    private AudioSource _audioSource;

    [Header("UI")]
    public float _healthBarDisplayDuration = 3f;
    public AnimationCurve _healthBarScaleDuringDisplay;

    [HideInInspector] public List<GameObject> _projectiles;
    [HideInInspector] public Transform _playerHead;

    private bool _gameActive = false;
    private float _attackTimer = 0f;
    private float _currentOrbitAngle = 0f;
    private bool _cooldownPeriod = true;
    private const float _ATTACK_TO_MOVEMENT_COOLDOWN = 0.5f;
    private bool _stunned = false;
    private IdleInstrumentAnimation _bossIdleAnimation;
    private ParticleSystem _bossSmokeParticles;
    private ParticleSystem _bossSparkParticles;
    private TrailRenderer _bossTrailRenderer;
    private List<BossAttack> _bossAttacks = new List<BossAttack>();
    private GameObject _bossVisuals;
    private float _maxHealth;

    private void Start()
    {
        _bossVisuals = _bossContainer.transform.GetChild(0).gameObject;
        _audioSource = GetComponent<AudioSource>();
        _eyesObject.SetActive(false);
        _bossIdleAnimation = _bossVisuals.transform.GetChild(0).GetComponent<IdleInstrumentAnimation>();
        _bossSmokeParticles = _bossVisuals.transform.GetChild(2).GetComponent<ParticleSystem>();
        _bossSparkParticles = _bossVisuals.transform.GetChild(3).GetComponent<ParticleSystem>();
        _bossTrailRenderer = _bossVisuals.GetComponent<TrailRenderer>();
        _playerHead = _playerObject.GetComponentInChildren<Camera>().transform;
        _bossAttacks.AddRange(Resources.LoadAll<BossAttack>("Attacks"));
        _bossHealthBar.transform.parent.localScale = Vector3.zero;
        _maxHealth = _bossHealth;
    }

    private void Update()
    {
        if (_stunned)
        {
            return;
        }
        _bossVisuals.transform.LookAt(_playerHead, Vector3.up);
        if (!_gameActive)
        {
            return;
        }

        if (_cooldownPeriod)
        {
            _attackTimer += Time.deltaTime;
            if (_attackTimer >= _attackCooldown)
            {
                _attackTimer -= _attackCooldown;
                _cooldownPeriod = false;
                TelegraphAttack();
            }
        }

    }

    public void TakeDamage(float damage)
    {
        _bossHealth = Mathf.Clamp(_bossHealth - damage, 0, _maxHealth);
        _stunned = true;
        _cooldownPeriod = false;
        _bossSparkParticles.Play();
        StopAllCoroutines();
        StartCoroutine(TakeDamageAnimation());
        StartCoroutine(DisplayBossHealth());
    }

    public void DestroyProjectile(GameObject projectile)
    {
        _projectiles.Remove(projectile);
        Destroy(projectile);
    }

    private void StartGame()
    {
        // Start Animations
        _audioSource.PlayOneShot(_gameStartAudio);
        _eyesObject.SetActive(true);

        StartCoroutine(StartGameAnimation());
    }

    private void TelegraphAttack()
    {
        var attackIndex = Random.Range(0, _bossAttacks.Count);
        var projectileSettings = _bossAttacks[attackIndex];

        if (projectileSettings._telegraphAnimationTrigger != "")
        {
            StartCoroutine(projectileSettings._telegraphAnimationTrigger, attackIndex);
        }
        else
        {
            _cooldownPeriod = true;
            AttackPlayer(attackIndex);
        }

        if(projectileSettings._telegraphAudio != null)
        {
            _audioSource.PlayOneShot(projectileSettings._telegraphAudio, projectileSettings._audioScale);
        }
    }

    private void AttackPlayer(int attackIndex)
    {
        var projectileSettings = _bossAttacks[attackIndex];

        _audioSource.PlayOneShot(_throwSound);

        // The projectile spawns with a small offset in forward direction so it does not spawn inside the boss
        var projectileObject = Instantiate(_projectilePrefab, _bossVisuals.transform.position + _bossVisuals.transform.forward, Quaternion.identity);
        var projectileComponent = projectileObject.GetComponent<Projectile>();

        projectileComponent._targetTransform = _playerHead;
        projectileComponent._movementSpeed = projectileSettings._attackSpeed;
        projectileComponent._mainMeshRenderer.material = projectileSettings._attackMaterial;
        projectileComponent._bossManager = this;

        _projectiles.Add(projectileObject);

        var newAngle = Random.Range(0f, 360f);
        StartCoroutine(OrbitLerpRoutine(newAngle));
    }

    #region Animations/Interpolations
    private IEnumerator OrbitLerpRoutine(float newAngle)
    {
        yield return new WaitForSeconds(_ATTACK_TO_MOVEMENT_COOLDOWN);
        var startAngle = _currentOrbitAngle;
        var lerpTimer = 0f;
        while (lerpTimer < 1)
        {
            _currentOrbitAngle = Mathf.Lerp(startAngle, newAngle, lerpTimer);
            _bossRotationPivot.transform.rotation = Quaternion.AngleAxis(_currentOrbitAngle, Vector3.up);
            lerpTimer += Time.deltaTime * _bossMovementSpeed;
            yield return null;
        }
    }

    private IEnumerator DoubleJumpAnimation(int attackIndex)
    {
        var lerpTimer = 0f;
        var basePosition = _bossVisuals.transform.position;

        // Rather hacky, but it coincides with two jumps at a speed of 5 :P
        while (lerpTimer < 4f)
        {
            lerpTimer += Time.deltaTime * 5;
            var pingPongLerp = Mathf.PingPong(lerpTimer, 1);
            var newPosition = _bossVisuals.transform.position;
            newPosition.y = Mathf.Lerp(basePosition.y, basePosition.y + 1, pingPongLerp);
            _bossVisuals.transform.position = newPosition; 
            yield return null;
        }
        _bossVisuals.transform.position = basePosition;

        _cooldownPeriod = true;
        AttackPlayer(attackIndex);
    }

    private IEnumerator SpinAnimation(int attackIndex)
    {
        var lerpTimer = 0f;
        var currentAngle = 0f;
        var upAxis = _bossVisuals.transform.up;
        while (lerpTimer < 1)
        {
            currentAngle = Mathf.Lerp(0, 360, lerpTimer);
            _bossVisuals.transform.localRotation = Quaternion.AngleAxis(currentAngle, upAxis);
            lerpTimer += Time.deltaTime;
            yield return null;
        }
        _bossVisuals.transform.localRotation = Quaternion.AngleAxis(0, upAxis);

        _cooldownPeriod = true;
        AttackPlayer(attackIndex);
    }

    private IEnumerator StartGameAnimation()
    {
        // Letting the initial audioclip finish before moving on
        yield return new WaitForSeconds(2);
        var targetPosition = _bossRotationPivot.position - (Vector3.forward * _offsetFromPivot);
        var startPosition = _bossContainer.transform.position;
        var lerpTimer = 0f;

        while (lerpTimer < 1f)
        {
            lerpTimer += Time.deltaTime * 5;
            _bossContainer.transform.position = Vector3.Lerp(startPosition, targetPosition, lerpTimer);
            yield return null;
        }

        _bossContainer.transform.position = targetPosition;
        _bossContainer.transform.SetParent(_bossRotationPivot);
        _bossTrailRenderer.enabled = true;
        _gameActive = true;
        _audioSource.clip = _gameSoundtrack;
        _audioSource.loop = true;
        _audioSource.Play();
    }

    private IEnumerator TakeDamageAnimation()
    {
        // We need to find out what y position we want to fall to
        // Layer 9 contains terrain
        var positionToFallTowards = _bossVisuals.transform.position;
        var layerBitMask = 1 << 9;
        RaycastHit hit;
        if(Physics.Raycast(_bossVisuals.transform.position, Vector3.down, out hit, 50, layerBitMask))
        {
            positionToFallTowards.y = hit.point.y + 0.75f;
        }

        // TODO: Tilt the boss slightly upwards, replace eye texture with a hurt one

        // We want a small bounce at the start of the fall animation so we "start" at a later stage in the animation before falling using a sine wave
        _bossIdleAnimation.enabled = false;
        var basePosition = _bossVisuals.transform.position;
        var offsetBasePosition = basePosition;
        offsetBasePosition.y += 1;
        // InverseLerp calculates the t parameter of the current position
        var positionLerpTimer = Mathf.InverseLerp(positionToFallTowards.y, offsetBasePosition.y, basePosition.y);
        while (Mathf.Sin(positionLerpTimer) > 0.0f)
        {
            _bossVisuals.transform.position = Vector3.Lerp(positionToFallTowards, offsetBasePosition, Mathf.Sin(positionLerpTimer));
            positionLerpTimer += (Time.deltaTime * 4);
            yield return null;
        }

        // As the boss faces down the position has to go down a bit as well so it looks like it is on the floor
        _bossVisuals.transform.LookAt(Vector3.down * 1000, Vector3.up);
        _bossVisuals.transform.position += Vector3.down * 0.5f;
        _audioSource.PlayOneShot(_impactSound);

        // Creates a small bit of wiggle after the fall by modifying the idle animation temporarily
        yield return new WaitForSeconds(1f);
        _bossTrailRenderer.enabled = false;
        var oldIdleSpeed = _bossIdleAnimation._speed;
        _bossIdleAnimation._speed *= 10;
        _bossIdleAnimation.enabled = true;
        yield return new WaitForSeconds(0.5f);
        _bossIdleAnimation.enabled = false;
        _bossIdleAnimation._speed = oldIdleSpeed;
        yield return new WaitForSeconds(1.5f);
        _bossSmokeParticles.Play();
        _audioSource.PlayOneShot(_poofSound, 0.75f);


        if (!_gameActive)
        {
            _bossVisuals.transform.position = basePosition;
            StartGame();
        }
        else
        {
            // This is mostly to make sure the boss doesn't change height if the damage animation happens right as another height modifying animation was active
            // basePosition is the stored position at the time of impact, hence switching it back to the height of the pivot as it is consistent
            var newPosition = basePosition;
            newPosition.y = _bossRotationPivot.position.y;
            _bossVisuals.transform.position = newPosition;
        }

        _bossTrailRenderer.enabled = true;
        _stunned = false;
        _cooldownPeriod = true;
        _bossIdleAnimation.enabled = true;
    }

    private IEnumerator DisplayBossHealth()
    {
        var targetFill = Utilities.Remap(0, _maxHealth, 0, 1, _bossHealth);
        var initialFill = _bossHealthBar.fillAmount;
        var healthAnimationTimer = 0f;

        while(healthAnimationTimer <= _healthBarDisplayDuration)
        {
            yield return null;
            healthAnimationTimer += Time.deltaTime;
            _bossHealthBar.fillAmount = Mathf.Lerp(initialFill, targetFill, healthAnimationTimer * 2f);
            _bossHealthBar.transform.parent.localScale = Vector3.one * _healthBarScaleDuringDisplay.Evaluate(Utilities.Remap(0, _healthBarDisplayDuration, 0, 1, healthAnimationTimer));
        }

        _bossHealthBar.fillAmount = targetFill;
    }
    #endregion
}

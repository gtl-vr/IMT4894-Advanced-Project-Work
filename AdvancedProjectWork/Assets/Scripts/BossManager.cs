using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

public class BossManager : MonoBehaviour
{
    public int _experimentID = 0;
    public string _dataFileName = "experimentData.csv";
    [Header("MetaInformation")]
    public GameObject _bossContainer;
    public GameObject _projectilePrefab;
    public Transform _bossRotationPivot;
    public GameObject _eyesObject;
    public Image _bossHealthBar;
    public Material _happyEyesMaterial;

    [Header("GameSettings")]
    public float _offsetFromPivot = 10f;
    public float _bossHealth = 100f;
    public float _timeBetweenAttackAndMovement = 0.5f;
    public int _scoreDecreasePerHit = 50;
    public int _maxScore = 1000;
    public float _timeBeforeInattention = 20f;

    [Header("Audio")]
    public AudioClip _gameStartAudio;
    public AudioClip _gameSoundtrack;
    public AudioClip _poofSound;
    public AudioClip _impactSound;
    public AudioClip _throwSound;
    public AudioClip _phaseTransitionSound;
    public AudioClip _explosionSound;
    public AudioClip _fanfareSound;
    private AudioSource _audioSource;

    [Header("UI")]
    public float _healthBarDisplayDuration = 3f;
    public AnimationCurve _healthBarScaleDuringDisplay;

    [HideInInspector] public List<GameObject> _projectiles;
    [HideInInspector] public Transform _playerHead;
    [HideInInspector] public float _timeTakenToWin = 0f;
    [HideInInspector] public int _amountOfPlayerHits = 0;
    [HideInInspector] public bool _gameActive = false;

    private GameObject _playerObject;
    private float _attackTimer = 0f;
    private float _currentOrbitAngle = 0f;
    private bool _cooldownPeriod = true;
    private bool _stunned = false;
    private IdleInstrumentAnimation _bossIdleAnimation;
    private ParticleSystem _bossSmokeParticles;
    private ParticleSystem _bossSparkParticles;
    private TrailRenderer _bossTrailRenderer;
    private List<BossPhase> _bossPhases = new List<BossPhase>();
    private BossPhase _currentPhase;
    private GameObject _bossVisuals;
    private float _maxHealth;
    private int _currentAttackOrderIndex = 0;
    private ParticleSystem _explosionParticles;
    private UIManager _uiManager;
    private GorillaManager _gorillaManager;
    private int _participantID;
    private List<int> _previousScores = new List<int>();

    private void Start()
    {
        if (SteamVR.active)
        {
            _playerObject = GameObject.Find("[CameraRig]");
        }
        else
        {
            _playerObject = GameObject.Find("DebugPlayerHead");
        }

        _bossVisuals = _bossContainer.transform.GetChild(0).gameObject;
        _audioSource = GetComponent<AudioSource>();
        _eyesObject.SetActive(false);
        _bossIdleAnimation = _bossVisuals.transform.GetChild(0).GetComponent<IdleInstrumentAnimation>();
        _bossSmokeParticles = _bossVisuals.transform.GetChild(2).GetComponent<ParticleSystem>();
        _bossSparkParticles = _bossVisuals.transform.GetChild(3).GetComponent<ParticleSystem>();
        _bossTrailRenderer = _bossVisuals.GetComponent<TrailRenderer>();
        _playerHead = _playerObject.GetComponentInChildren<Camera>().transform;
        _bossPhases.AddRange(Resources.LoadAll<BossPhase>("Phases"));
        _bossHealthBar.transform.parent.localScale = Vector3.zero;
        _maxHealth = _bossHealth;
        _explosionParticles = _bossVisuals.transform.GetChild(6).GetComponent<ParticleSystem>();
        _uiManager = GameObject.Find("UIManager").GetComponent<UIManager>();
        _gorillaManager = GameObject.Find("GorillaContainer").GetComponent<GorillaManager>();
        FindCurrentID();
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
            if (_attackTimer >= _currentPhase._attackCooldown)
            {
                _attackTimer -= _currentPhase._attackCooldown;
                _cooldownPeriod = false;
                TelegraphAttack();
            }
        }

    }

    private void StartGame()
    {
        // Start Animations
        _audioSource.PlayOneShot(_gameStartAudio);
        _eyesObject.SetActive(true);
        _timeTakenToWin = Time.realtimeSinceStartup;
        _uiManager.CloseTutorials();

        StartCoroutine(StartGameAnimation());
    }

    private void FindNewPivotAngle()
    {
        var newAngle = Random.Range(0f, 360f);
        StartCoroutine(OrbitLerpRoutine(newAngle));
    }

    private int FindScorePlacement(int newScore)
    {
        var sortedList = new List<int>(_previousScores);
        sortedList.Add(newScore);
        sortedList.Sort();
        sortedList.Reverse();

        for(int i = 0; i < sortedList.Count; i++)
        {
            if(sortedList[i] == newScore)
            {
                return i+1;
            }
        }
        return 1;
    }

    #region Damage/Phase Transition Logic
    private bool CheckForPhaseChange()
    {
        foreach (var phase in _bossPhases)
        {
            if (phase.IsWithinPhaseThreshold(_bossHealth) && phase != _currentPhase)
            {
                _currentPhase = phase;
                _currentAttackOrderIndex = 0;
                if (_currentPhase._transitionFunctionName != "")
                {
                    var method = this.GetType().GetMethod(_currentPhase._transitionFunctionName, BindingFlags.Instance | BindingFlags.NonPublic);
                    method?.Invoke(this, null);
                }
                return true;
            }
        }
        return false;
    }

    public void TakeDamage(float damage)
    {
        _bossHealth = Mathf.Clamp(_bossHealth - damage, 0, _maxHealth);
        _stunned = true;
        _cooldownPeriod = false;
        _bossSparkParticles.Play();
        StopAllCoroutines();
        var hasPhaseChanged = CheckForPhaseChange();
        StartCoroutine(DisplayBossHealth());

        if (_bossHealth > 0)
        {
            StartCoroutine(TakeDamageAnimation(hasPhaseChanged));
        }
        else
        {
            StartCoroutine(BossDefeatedAnimation());
        }
    }

    public void DestroyProjectile(GameObject projectile)
    {
        _projectiles.Remove(projectile);
        Destroy(projectile);
    }
    #endregion
    #region Attack Logic
    private void TelegraphAttack()
    {
        BossAttack newAttack;
        if (_currentPhase._randomAttackOrder)
        {
            newAttack = _currentPhase._bossAttacks[Random.Range(0, _currentPhase._bossAttacks.Count)];
        }
        else
        {
            newAttack = GetOrderedAttack();
        }

        if (newAttack._telegraphAnimationTrigger != "")
        {
            StartCoroutine(newAttack._telegraphAnimationTrigger, newAttack);
        }
        else
        {
            _cooldownPeriod = true;
            AttackPlayer(newAttack);
        }

        if (newAttack._telegraphAudio != null)
        {
            _audioSource.PlayOneShot(newAttack._telegraphAudio, newAttack._audioScale);
        }
    }

    private void AttackPlayer(BossAttack attack)
    {
        _audioSource.PlayOneShot(_throwSound);

        // The projectile spawns with a small offset in forward direction so it does not spawn inside the boss
        var projectileObject = Instantiate(_projectilePrefab, _bossVisuals.transform.position + _bossVisuals.transform.forward, Quaternion.identity);
        var projectileComponent = projectileObject.GetComponent<Projectile>();

        projectileComponent._targetTransform = _playerHead;
        projectileComponent._movementSpeed = attack._attackSpeed;
        projectileComponent._mainMeshRenderer.material = attack._attackMaterial;
        projectileComponent._bossManager = this;
        projectileComponent._chargeValue = attack._attackChargeAmount;

        _projectiles.Add(projectileObject);

        if (_currentPhase._containsMovement)
        {
            var newAngle = Random.Range(0f, 360f);
            StartCoroutine(OrbitLerpRoutine(newAngle));
        }
    }

    private BossAttack GetOrderedAttack()
    {
        var index = _currentAttackOrderIndex;
        _currentAttackOrderIndex++;

        if (_currentAttackOrderIndex >= _currentPhase._attackOrder.Count)
        {
            _currentAttackOrderIndex = 0;
        }

        return _currentPhase._attackOrder[index];
    }
    #endregion
    #region Transition Functions
    private void FinalPhaseStart()
    {
        SetSweatState(true);
        _gorillaManager.RunAnimation(_timeBeforeInattention);
    }
    private void SetSweatState(bool state)
    {
        _bossVisuals.transform.GetChild(4).gameObject.SetActive(state);
    }
    #endregion
    #region Animations/Interpolations
    private IEnumerator OrbitLerpRoutine(float newAngle)
    {
        yield return new WaitForSeconds(_timeBetweenAttackAndMovement);
        var startAngle = _currentOrbitAngle;
        var lerpTimer = 0f;
        while (lerpTimer < 1)
        {
            _currentOrbitAngle = Mathf.Lerp(startAngle, newAngle, lerpTimer);
            _bossRotationPivot.transform.rotation = Quaternion.AngleAxis(_currentOrbitAngle, Vector3.up);
            lerpTimer += Time.deltaTime * _currentPhase._movementSpeed;
            yield return null;
        }
    }

    private IEnumerator DoubleJumpAnimation(BossAttack attack)
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
        AttackPlayer(attack);
    }

    private IEnumerator SpinAnimation(BossAttack attack)
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
        AttackPlayer(attack);
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

    private IEnumerator TakeDamageAnimation(bool includesPhaseTransition)
    {
        // We need to find out what y position we want to fall to
        // Layer 9 contains terrain
        var positionToFallTowards = _bossVisuals.transform.position;
        var layerBitMask = 1 << 9;
        RaycastHit hit;
        if (Physics.Raycast(_bossVisuals.transform.position, Vector3.down, out hit, 50, layerBitMask))
        {
            positionToFallTowards.y = hit.point.y + 0.75f;
        }

        // TODO(maybe): Tilt the boss slightly upwards, replace eye texture with a hurt one

        // We want a small bounce at the start of the fall animation so we "start" at a later stage in the animation before falling using a sine wave
        _bossIdleAnimation.enabled = false;
        var basePosition = _bossVisuals.transform.position;
        var offsetBasePosition = basePosition;
        offsetBasePosition.y += 1;
        // InverseLerp calculates the t parameter of the current position
        var positionLerpTimer = Mathf.InverseLerp(positionToFallTowards.y, offsetBasePosition.y, basePosition.y);
        while (Mathf.Sin(positionLerpTimer) > 0.0f)
        {
            yield return null;
            _bossVisuals.transform.position = Vector3.Lerp(positionToFallTowards, offsetBasePosition, Mathf.Sin(positionLerpTimer));
            positionLerpTimer += (Time.deltaTime * 4);
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
        _bossIdleAnimation.enabled = true;
        _attackTimer = 0f;

        if (includesPhaseTransition && _gameActive)
        {
            StartCoroutine(PhaseTransitionAnimation());
        }
        else
        {
            _cooldownPeriod = true;
        }
    }

    private IEnumerator DisplayBossHealth()
    {
        var targetFill = Utilities.Remap(0, _maxHealth, 0, 1, _bossHealth);
        var initialFill = _bossHealthBar.fillAmount;
        var healthAnimationTimer = 0f;

        while (healthAnimationTimer <= _healthBarDisplayDuration)
        {
            yield return null;
            healthAnimationTimer += Time.deltaTime;
            _bossHealthBar.fillAmount = Mathf.Lerp(initialFill, targetFill, healthAnimationTimer * 2f);
            _bossHealthBar.transform.parent.localScale = Vector3.one * _healthBarScaleDuringDisplay.Evaluate(Utilities.Remap(0, _healthBarDisplayDuration, 0, 1, healthAnimationTimer));
        }

        _bossHealthBar.fillAmount = targetFill;
    }

    private IEnumerator PhaseTransitionAnimation()
    {
        var light = _bossContainer.GetComponentInChildren<Light>();
        var baseLightColor = light.color;
        var baseScale = _bossVisuals.transform.localScale;
        var targetScale = baseScale * 2f;
        var lerpTimer = 0f;
        var cosTimer = 0f;
        _audioSource.PlayOneShot(_phaseTransitionSound);
        var smokeParticles = _bossVisuals.transform.GetChild(5).gameObject;
        smokeParticles.SetActive(true);

        while (lerpTimer <= _phaseTransitionSound.length)
        {
            yield return null;
            lerpTimer += Time.deltaTime;
            cosTimer += Time.deltaTime * 10f;

            light.color = Color.Lerp(baseLightColor, Color.red, _healthBarScaleDuringDisplay.Evaluate(Utilities.Remap(0, _phaseTransitionSound.length, 0, 1, lerpTimer)));

            // This way, cos(cosTimer) starts at -1 and gets remapped for a [0, 1] pingpong
            _bossVisuals.transform.localScale = Vector3.Lerp(baseScale, targetScale, Utilities.Remap(-1, 1, 0, 1, Mathf.Cos(cosTimer) * -1));
        }

        light.color = baseLightColor;
        _bossVisuals.transform.localScale = baseScale;
        smokeParticles.SetActive(false);

        _cooldownPeriod = true;

        // It is kind of weird if the boss just stands still for a few seconds once the transition is done so it starts moving after this
        if (_currentPhase._containsMovement)
        {
            FindNewPivotAngle();
        }
    }

    // Similar to TakeDamageAnimation(), but slower and with some explosions.
    // It's technically a different animation, but with some similarities, hence the copy pasta.
    // Separating similar code into functions when using coroutines can be a bit rough (Due to yields).
    private IEnumerator BossDefeatedAnimation()
    {
        _timeTakenToWin = Time.realtimeSinceStartup - _timeTakenToWin;
        _audioSource.Stop();

        var positionToFallTowards = _bossVisuals.transform.position;
        var layerBitMask = 1 << 9;
        RaycastHit hit;
        if (Physics.Raycast(_bossVisuals.transform.position, Vector3.down, out hit, 50, layerBitMask))
        {
            positionToFallTowards.y = hit.point.y + 0.75f;
        }

        _bossIdleAnimation.enabled = false;
        var basePosition = _bossVisuals.transform.position;
        var offsetBasePosition = basePosition;
        offsetBasePosition.y += 1;
        var positionLerpTimer = Mathf.InverseLerp(positionToFallTowards.y, offsetBasePosition.y, basePosition.y);
        Explode();
        while (Mathf.Sin(positionLerpTimer) > 0.0f)
        {
            yield return null;
            _bossVisuals.transform.position = Vector3.Lerp(positionToFallTowards, offsetBasePosition, Mathf.Sin(positionLerpTimer));
            positionLerpTimer += (Time.deltaTime * 1.5f);
        }
        Explode();
        _bossVisuals.transform.LookAt(Vector3.down * 1000, Vector3.up);
        _bossVisuals.transform.position += Vector3.down * 0.5f;
        _audioSource.PlayOneShot(_impactSound);

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

        _stunned = false;
        _bossIdleAnimation.enabled = true;
        _attackTimer = 0f;

        // The boss wants to be on the ground again once the game is done
        _bossVisuals.transform.position += Vector3.up * 1.5f;
        _eyesObject.GetComponent<MeshRenderer>().material = _happyEyesMaterial;
        SetSweatState(false);
        _audioSource.PlayOneShot(_fanfareSound);
        var intTimeTakenToWin = (int)_timeTakenToWin;
        var fullScore = Mathf.Clamp(_maxScore - (intTimeTakenToWin) - (_amountOfPlayerHits * _scoreDecreasePerHit), 0, _maxScore);
        _uiManager.DisplayScore(intTimeTakenToWin, _amountOfPlayerHits, fullScore, _participantID, _experimentID, FindScorePlacement(fullScore), _previousScores.Count + 1);
        StartCoroutine(OrbitLerpRoutine(0f));
        AppendScoreToFile(fullScore);
    }

    private void Explode()
    {
        _audioSource.PlayOneShot(_explosionSound, 2f);
        _explosionParticles.Play();
    }
    #endregion
    #region Data Collection
    private void AppendScoreToFile(int score)
    {
        if (File.Exists(Application.dataPath + "/" + _dataFileName))
        {
            // Append
            using (var writer = File.AppendText(Application.dataPath + "/" + _dataFileName))
            {
                var column1 = _participantID.ToString();
                var column2 = _experimentID.ToString();
                var column3 = ((int)_timeTakenToWin).ToString();
                var column4 = _amountOfPlayerHits.ToString();
                var column5 = score.ToString();
                var line = string.Format("{0},{1},{2},{3},{4}", column1, column2, column3, column4, column5);
                writer.WriteLine(line);
                writer.Flush();
            }
        }
        else
        {
            // Write
            using (var writer = new StreamWriter(Application.dataPath + "/" + _dataFileName))
            {
                var column1 = "ParticipantID";
                var column2 = "ExperimentID";
                var column3 = "TimeTaken";
                var column4 = "HitsTaken";
                var column5 = "Score";
                var line = string.Format("{0},{1},{2},{3},{4}", column1, column2, column3, column4, column5);
                writer.WriteLine(line);
                writer.Flush();

                column1 = _participantID.ToString();
                column2 = _experimentID.ToString();
                column3 = ((int)_timeTakenToWin).ToString();
                column4 = _amountOfPlayerHits.ToString();
                column5 = score.ToString();
                line = string.Format("{0},{1},{2},{3},{4}", column1, column2, column3, column4, column5);
                writer.WriteLine(line);
                writer.Flush();
            }
        }
    }

    private void FindCurrentID()
    {
        if (!File.Exists(Application.dataPath + "/" + _dataFileName))
        {
            _participantID = 0;
            return;
        }
        using (var reader = new StreamReader(Application.dataPath + "/" + _dataFileName))
        {
            var list1 = new List<string>();
            var list2 = new List<string>();
            var list3 = new List<string>();
            var list4 = new List<string>();
            var list5 = new List<string>();
            reader.ReadLine();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');

                list1.Add(values[0]);
                list2.Add(values[1]);
                list3.Add(values[2]);
                list4.Add(values[3]);
                list5.Add(values[4]);
            }

            _participantID = int.Parse(list1[list1.Count - 1]) + 1;
            _previousScores = list5.Select(int.Parse).ToList();
        }
    }
    #endregion
}
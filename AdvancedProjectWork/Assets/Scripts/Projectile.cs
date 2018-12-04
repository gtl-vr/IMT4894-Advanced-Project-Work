using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float _rotationSpeed = 3;
    public float _movementSpeed = 1;
    public MeshRenderer _mainMeshRenderer;
    public float _destructionSpeed = 5;

    [HideInInspector] public float _damageValue = 0f;
    [HideInInspector] public float _chargeValue = 10f;
    [HideInInspector] public Transform _targetTransform;
    [HideInInspector] public BossManager _bossManager;
    private Vector3 _rotationAxis;
    private bool _translating;
    private BoxCollider _collider;

    // Use this for initialization
    void Start()
    {
        _rotationAxis = new Vector3(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f)).normalized;
        _translating = true;
        _collider = GetComponent<BoxCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        if (_translating)
        {
            transform.Rotate(_rotationAxis, Time.deltaTime * _rotationSpeed);
            transform.position += (_targetTransform.position - transform.position).normalized * Time.deltaTime * _movementSpeed;
        }
    }

    public void Destroy()
    {
        _translating = false;
        _collider.enabled = false;
        StartCoroutine(DestructionAnimation());
    }

    private IEnumerator DestructionAnimation()
    {
        var timer = 0f;
        var baseScale = transform.localScale;
        while(timer <= 1)
        {
            timer += Time.deltaTime * _destructionSpeed;
            transform.localScale = Vector3.Lerp(baseScale, Vector3.zero, timer);
            yield return null;
        }
        _bossManager.DestroyProjectile(gameObject);
    }
}
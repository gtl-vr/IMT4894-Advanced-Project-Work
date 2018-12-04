using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GorillaManager : MonoBehaviour
{
    public float _displayDuration = 5f;
    public float _verticalOffset = 2f;
    public AnimationCurve _interpolationStateOverLifetime;

    private List<Transform> _gorillaTransforms = new List<Transform>();
    private List<Vector3> _basePositions = new List<Vector3>();

    private void Start()
    {
        foreach(Transform child in transform)
        {
            _gorillaTransforms.Add(child);
            _basePositions.Add(child.position);
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.G))
        {
            RunAnimation(0f);
        }
#endif
    }

    public void RunAnimation(float waitTime)
    {
        StartCoroutine(PositionLerp(waitTime));
    }

    private IEnumerator PositionLerp(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        var timer = 0f;
        while(timer <= _displayDuration)
        {
            timer += Time.deltaTime;
            for(int i = 0; i < _gorillaTransforms.Count; i++)
            {
                _gorillaTransforms[i].position = Vector3.Lerp(_basePositions[i], 
                                                              _basePositions[i] + (Vector3.up * _verticalOffset), 
                                                              _interpolationStateOverLifetime.Evaluate(Utilities.Remap(0, _displayDuration, 0, 1, timer)));
            }
            yield return null;
        }

        for (int i = 0; i < _gorillaTransforms.Count; i++)
        {
            _gorillaTransforms[i].position = _basePositions[i];
        }
    }
}

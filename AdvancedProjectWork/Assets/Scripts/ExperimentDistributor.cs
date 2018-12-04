using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExperimentDistributor : MonoBehaviour {
    private Vector2Int _sceneIndexRange = new Vector2Int(1, 3);

    // https://scottlilly.com/create-better-random-numbers-in-c/
    private void Awake()
    {
        var randomGenerator = new System.Security.Cryptography.RNGCryptoServiceProvider();
        var randomNumber = new byte[1];
        randomGenerator.GetBytes(randomNumber);
        var asciiValueOfRandomCharacter = System.Convert.ToDouble(randomNumber[0]);
        
        // We are using Math.Max, and substracting 0.00000000001, 
        // to ensure "multiplier" will always be between 0.0 and .99999999999
        // Otherwise, it's possible for it to be "1", which causes problems in our rounding.
        double multiplier = Math.Max(0, (asciiValueOfRandomCharacter / 255d) - 0.00000000001d);

        // We need to add one to the range, to allow for the rounding done with Math.Floor
        int range = _sceneIndexRange.y - _sceneIndexRange.x + 1;
        double randomValueInRange = Math.Floor(multiplier * range);

        var randomInteger = (int)(_sceneIndexRange.x + randomValueInRange);

        SceneManager.LoadScene(randomInteger);
    }
}
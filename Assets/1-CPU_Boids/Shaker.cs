using UnityEngine;
using System.Collections;

[System.Serializable]
public class Shaker
{
    #region Public attributes

    public float frequency = 0.1f;
    public float amount = 1.0f;
    public int octave = 3;

    #endregion

    #region Private variables

    Vector2[] vectors;
    float time;

    #endregion

    #region Public properties

    public float Scalar {
        get {
            ResetIfNeed ();
            return Perlin.Fbm (vectors [0] * time, octave) * amount * 2;
        }
    }

    public Vector3 Position {
        get {
            ResetIfNeed ();
            return new Vector3 (
                Perlin.Fbm (vectors [0] * time, octave),
                Perlin.Fbm (vectors [1] * time, octave),
                Perlin.Fbm (vectors [2] * time, octave)) * (amount * 2);
        }
    }

    public Quaternion YawPitch {
        get {
            ResetIfNeed ();
            return
                Quaternion.AngleAxis (Perlin.Fbm (vectors [0] * time, octave) * amount * 2, Vector3.up) *
                Quaternion.AngleAxis (Perlin.Fbm (vectors [1] * time, octave) * amount * 2, Vector3.right);
        }
    }

    #endregion

    #region Public functions

    public void Update (float delta)
    {
        ResetIfNeed ();
        time += delta * frequency;
    }

    public void Reset ()
    {
        vectors = new Vector2[3];
        time = Random.value * 10.0f;
        
        for (var i = 0; i < 3; i++)
        {
            var theta = Random.value * Mathf.PI * 2;
            vectors [i].Set (Mathf.Cos (theta), Mathf.Sin (theta));
        }
    }

    #endregion

    #region Private functions

    public void ResetIfNeed ()
    {
        if (vectors == null)
        {
            Reset ();
        }
    }

    #endregion
}

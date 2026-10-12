using System;
using UnityEngine;

public static class GlobalAudioEvents
{
    // Событие: передает позицию, откуда раздался звук выстрела
    public static Action<Vector3> OnGunshotFired;
}

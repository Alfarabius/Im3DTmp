using System;
using UnityEngine;

public static class GlobalAudioEvents
{
    // Событие: передает позицию, откуда раздался звук выстрела, и нормализованный вектор направления
    public static Action<Vector3, Vector3> OnGunshotFired;
}

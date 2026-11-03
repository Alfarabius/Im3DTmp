using UnityEngine;

internal static class BloodEffectFactory
{
    private static ParticleSystem _stains;

    public static ParticleSystem CreateSplatter(Transform parent, Material material)
    {
        var splatter = ParticleEffectFactory.Create(parent, "Blood Splatter", material, 48);
        var main = splatter.main;
        main.gravityModifier = 0.8f;
        var renderer = splatter.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.08f;
        renderer.lengthScale = 1.4f;
        ParticleEffectFactory.SetFade(splatter, 0.45f);
        return splatter;
    }

    public static void EmitSplatter(ParticleSystem splatter, Vector3 point, Vector3 normal, int count)
    {
        splatter.Play(false);
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = (normal + Random.insideUnitSphere * 0.8f + Vector3.up * 0.35f).normalized;
            splatter.Emit(new ParticleSystem.EmitParams
            {
                position = point + normal * 0.025f,
                velocity = direction * Random.Range(1.2f, 3f),
                startSize = Random.Range(0.025f, 0.085f),
                startLifetime = Random.Range(0.25f, 0.55f),
                rotation = Random.Range(0f, 360f),
                startColor = Color.white * Random.Range(0.75f, 1f)
            }, 1);
        }
    }

    public static Transform GetStainsRoot(Material material)
    {
        EnsureStains(material);
        return _stains.transform;
    }

    public static void EmitStain(Material material, Vector3 position, float size, float lifetime)
    {
        EnsureStains(material);
        _stains.Emit(new ParticleSystem.EmitParams
        {
            position = position,
            velocity = Vector3.zero,
            startSize = size,
            startLifetime = lifetime,
            rotation3D = new Vector3(90f, Random.Range(0f, 360f), 0f),
            startColor = Color.white * Random.Range(0.75f, 1f)
        }, 1);
    }

    private static void EnsureStains(Material material)
    {
        if (_stains != null) return;
        _stains = ParticleEffectFactory.Create(null, "Blood Stains", material, 512);
        var main = _stains.main;
        main.loop = true;
        main.startRotation3D = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.ringBufferMode = ParticleSystemRingBufferMode.PauseUntilReplaced;
        var renderer = _stains.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        ParticleEffectFactory.SetFade(_stains, 0.8f);
        _stains.Play(false);
    }
}

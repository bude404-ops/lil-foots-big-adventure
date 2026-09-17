// Chunky cartoon particles (art bible s9): landing dust bursts, run kicks, leaf drift.
using UnityEngine;

public class FX : MonoBehaviour
{
    ParticleSystem dust, leaves;

    public static FX Build()
    {
        var go = new GameObject("FX");
        var fx = go.AddComponent<FX>();
        fx.dust = fx.MakeDust(new Color(0.94f, 0.90f, 0.80f));
        fx.leaves = fx.MakeLeaves();
        return fx;
    }

    ParticleSystem MakeDust(Color c)
    {
        var go = new GameObject("dust");
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = false; main.playOnAwake = false;
        main.duration = 1f; main.startLifetime = 0.45f; main.startSpeed = 0;
        main.startSize = 0.34f; main.startSize3D = false;
        main.gravityModifier = -0.05f; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = 0;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.55f;
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, 0.9f), new Color(c.r, c.g, c.b, 0f));
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1.6f);
        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.radial = 2.6f; vel.y = 1.6f;
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = new Material(Shader.Find("Sprites/Default"));
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    ParticleSystem MakeLeaves()
    {
        var go = new GameObject("leaves");
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = true; main.playOnAwake = true;
        main.duration = 6f; main.startLifetime = 9f; main.startSpeed = 0.4f;
        main.startSize = 0.16f; main.gravityModifier = 0.008f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = ps.emission; em.rateOverTime = 4f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(56f, 14f, 1f); sh.position = new Vector3(0, 11f, 0);
        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.6f, -0.2f);
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0.8f, 0.8f), new GradientAlphaKey(0f, 1f) },
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.36f, 0.65f, 0.35f), 0f),
                new GradientColorKey(new Color(0.62f, 0.75f, 0.28f), 1f) });
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = new Material(Shader.Find("Sprites/Default"));
        return ps;
    }

    public void DustBurst(Vector3 at, int count, float power)
    {
        var em = dust.emission; em.rateOverTime = 0;
        dust.transform.position = at;
        var main = dust.main;
        main.startSize = 0.28f * power;
        dust.Emit(new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true }, count);
    }

    public void RunKick(Vector3 at, int face)
    {
        var main = dust.main; main.startSize = 0.17f;
        var em = dust.emission;
        dust.Emit(new ParticleSystem.EmitParams
        {
            position = at,
            velocity = new Vector3(-face * 1.6f, 1.1f, 0),
            startSize = 0.17f,
            startLifetime = 0.4f,
            startColor = new Color(0.94f, 0.90f, 0.80f, 0.75f)
        }, 2);
    }
}

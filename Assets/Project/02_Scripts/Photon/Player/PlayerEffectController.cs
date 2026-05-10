using UnityEngine;

public class PlayerEffectController : MonoBehaviour
{
    [Header("SFX")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _sfxClips;

    [Header("파티클 프리팹")]
    [SerializeField] private GameObject[] _hitParticlePrefab;
    [SerializeField] private GameObject[] _deadParticlePrefab;
    [SerializeField] private GameObject _dustParticle;

    private Transform _ownerTransform;
    private PlayerLauncher _launcher;

    private float _lastCollisionEffectTime = -999f;
    private const float CollisionEffectCooldown = 0.5f;
    private const float ParticleSpawnHeightOffset = 1f;
    private const float ParticleDestroyDelay = 2f;

    private void Awake()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }

    public void Initialize(Transform ownerTransform, PlayerLauncher launcher)
    {
        _ownerTransform = ownerTransform;
        _launcher = launcher;
    }

    public bool CanPlayCollisionEffect()
    {
        if (Time.time - _lastCollisionEffectTime < CollisionEffectCooldown)
        {
            return false;
        }

        _lastCollisionEffectTime = Time.time;
        return true;
    }

    public void SpawnDustParticle()
    {
        if (_dustParticle == null) return;
        if (_ownerTransform == null) return;

        GameObject go = Instantiate(_dustParticle,_ownerTransform.position,Quaternion.LookRotation(-_ownerTransform.forward));

        ParticleSystem particle = go.transform.GetComponent<ParticleSystem>();
        if (particle == null) return;

        ParticleSystem.MainModule ms = particle.main;
        float queuedPower = _launcher.QueuedPower;

        ms.startSize = new ParticleSystem.MinMaxCurve(0.1f, queuedPower);
    }

    public void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        PlaySfx(0);
        SpawnParticles(_hitParticlePrefab, position, normal);
    }

    public void PlayDeadEffect(int effectIndex, Vector3 position, Vector3 normal)
    {
        PlaySfx(effectIndex);
        SpawnParticles(_deadParticlePrefab, position, normal);
    }

    private void PlaySfx(int clipIndex)
    {
        _audioSource.PlayOneShot(_sfxClips[clipIndex]);
    }

    private void SpawnParticles(GameObject[] particlePrefabs, Vector3 position, Vector3 normal)
    {
        if (particlePrefabs == null) return;

        Vector3 spawnPosition = position + Vector3.up * ParticleSpawnHeightOffset;

        Quaternion rotation = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        for (int i = 0; i < particlePrefabs.Length; i++)
        {
            if (particlePrefabs[i] == null)
            {
                continue;
            }

            GameObject particle = Instantiate(particlePrefabs[i], spawnPosition, rotation);
            Destroy(particle, ParticleDestroyDelay);
        }
    }
}

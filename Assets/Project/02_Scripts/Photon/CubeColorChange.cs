using Fusion;
using UnityEngine;

public class CubeColorChange : NetworkBehaviour
{
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private Material _RedColor;

    [Networked] public int GridX { get; set; }
    [Networked] public int GridY { get; set; }
    [Networked] private NetworkBool IsWarning { get; set; }

    private bool _lastWarningState;

    private void Awake()
    {
        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        Rigidbody tileRigidbody = GetComponent<Rigidbody>();
        if (tileRigidbody != null)
        {
            tileRigidbody.isKinematic = true;
            tileRigidbody.useGravity = false;
        }
    }

    public override void Spawned()
    {
        FallOffMapShrinker.Instance?.RegisterNetworkTile(this);
        ApplyWarningState();
    }

    public override void Render()
    {
        if (_lastWarningState == IsWarning)
        {
            return;
        }

        ApplyWarningState();
    }

    public void InitGridIndex(int gridX, int gridY)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        GridX = gridX;
        GridY = gridY;
        IsWarning = false;

        Rigidbody tileRigidbody = GetComponent<Rigidbody>();
        if (tileRigidbody != null)
        {
            tileRigidbody.isKinematic = true;
            tileRigidbody.useGravity = false;
        }
    }

    public void SetWarningColor()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            IsWarning = true;
        }

        ApplyWarningState();
    }

    public void StartFall(float fallImpulse)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        Rigidbody tileRigidbody = GetComponent<Rigidbody>();
        if (tileRigidbody == null)
        {
            return;
        }

        tileRigidbody.isKinematic = false;
        tileRigidbody.useGravity = true;
        tileRigidbody.AddForce(Vector3.down * fallImpulse, ForceMode.Impulse);
    }

    private void ApplyWarningState()
    {
        if (_meshRenderer == null)
        {
            Debug.LogWarning($"{gameObject.name} : 머테리얼이 없음");
            return;
        }

        if (!IsWarning)
        {
            _lastWarningState = false;
            return;
        }

        if (_RedColor == null)
        {
            Debug.LogWarning($"{gameObject.name} : 바꿔야 할 머테리얼이 없음");
            return;
        }

        _meshRenderer.material = _RedColor;
        _lastWarningState = true; 
    }
}

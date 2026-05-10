using UnityEngine;

// 조준 힘에 맞춰 화살표 길이와 색을 갱신
public class AimArrowView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAimController _aimController;

    [SerializeField] private Transform _arrowBody;

    [SerializeField] private Transform _arrowHead;

    [SerializeField] private SpriteRenderer _arrowBodyRenderer;
    
    [SerializeField] private SpriteRenderer _arrowHeadRenderer;
    
    [Header("Length")]
    [SerializeField] private float _minLength = 0.5f;

    [SerializeField] private float _maxLength = 3f;

    [SerializeField] private float _bodyWidth = 0.15f;

    [SerializeField] private float _headOffset = 0.15f;

    [Header("Color")]
    [SerializeField] private Gradient _powerColor;

    private void Reset()
    {
        _aimController = GetComponentInParent<PlayerAimController>();
    }

    private void Awake()
    {
        if (_aimController == null)
        {
            _aimController = GetComponentInParent<PlayerAimController>();
        }
    }

    private void Start()
    {
        _aimController.AimChanged += UpdateArrow;

        UpdateArrow(_aimController.AimPower);
    }

    private void OnDestroy()
    {
        _aimController.AimChanged -= UpdateArrow;
    }

    private void UpdateArrow(float aimPower)
    {
        float clampedPower = Mathf.Clamp01(aimPower);
        float currentLength = Mathf.Lerp(_minLength, _maxLength, clampedPower);

        _arrowBody.localScale = new Vector3(_bodyWidth, currentLength, 1f);
        _arrowBody.localPosition = new Vector3(0f, 0f, currentLength * 0.5f);

        _arrowHead.localPosition = new Vector3(0f, 0f, currentLength + _headOffset);

        Color currentColor = _powerColor.Evaluate(clampedPower);

        _arrowBodyRenderer.color = currentColor;
        _arrowHeadRenderer.color = currentColor;
    }

}

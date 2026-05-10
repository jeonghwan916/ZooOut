using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class VolumeControl : MonoBehaviour
{
    // 원래는 Volume Mixer를 써야하지만 간단하게 구현 필요상 AudioSource 하나만의 volume 조절 기능으로 단순화해서 구현
    
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private Slider _bgmVolumeSlider;

    private void Awake()
    {
        _bgmSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        _bgmVolumeSlider.value = _bgmSource.volume;
        _bgmSource.loop = true;
        _bgmSource.Play();
    }

    public void UpdateSlider()
    {
        _bgmSource.volume = _bgmVolumeSlider.value;
    }
}

using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
sealed class ScenicButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Button button;
    Tween press;

    void Awake() => button = GetComponent<Button>();

    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && button.IsInteractable())
            Animate(.965f, .08f);
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left)
            Animate(1f, .14f);
    }

    public void OnPointerExit(PointerEventData data) => Animate(1f, .12f);

    void Animate(float scale, float duration)
    {
        if (Mathf.Approximately(transform.localScale.x, scale) && (press == null || !press.IsActive()))
            return;
        press?.Kill();
        press = transform.DOScale(scale, duration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    void OnDisable()
    {
        press?.Kill();
        transform.localScale = Vector3.one;
    }
}

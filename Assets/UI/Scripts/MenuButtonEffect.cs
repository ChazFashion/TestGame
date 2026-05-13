using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

namespace RacingUI
{
    public class MenuButtonEffect : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Settings")]
        public RectTransform underline; // Ссылка на полоску
        public float animationSpeed = 5f;
        public Vector3 normalScale = new Vector3(0, 1, 1);
        public Vector3 activeScale = new Vector3(1, 1, 1);

        private Coroutine activeCoroutine;

        void Awake()
        {
            if (underline != null) underline.localScale = normalScale;
        }

        public void OnSelect(BaseEventData eventData)
        {
            StartAnimation(activeScale);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            StartAnimation(normalScale);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // При наведении мышкой выбираем этот объект в EventSystem
            EventSystem.current.SetSelectedGameObject(this.gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // При уходе мышки можно снимать выделение, если нужно
            // EventSystem.current.SetSelectedGameObject(null);
        }

        private void StartAnimation(Vector3 targetScale)
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(AnimateScale(targetScale));
        }

        IEnumerator AnimateScale(Vector3 target)
        {
            while (Vector3.Distance(underline.localScale, target) > 0.001f)
            {
                underline.localScale = Vector3.Lerp(underline.localScale, target, Time.unscaledDeltaTime * animationSpeed);
                yield return null;
            }
            underline.localScale = target;
        }
    }
}

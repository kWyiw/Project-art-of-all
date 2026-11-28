using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>把梯形背景上的指针事件转发给菜单项视图和按钮。</summary>
    [DisallowMultipleComponent]
    public sealed class StartMenuOptionHitArea : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        [SerializeField] private StartMenuOptionView option;
        [SerializeField] private Button button;

        public void Configure(StartMenuOptionView view, Button targetButton)
        {
            option = view;
            button = targetButton;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ResolveReferences();
            if (option != null)
            {
                option.OnPointerEnter(eventData);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResolveReferences();
            if (option != null)
            {
                option.OnPointerExit(eventData);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ResolveReferences();
            if (button != null)
            {
                button.onClick.Invoke();
            }
        }

        private void ResolveReferences()
        {
            if (option == null)
            {
                option = GetComponentInParent<StartMenuOptionView>();
            }

            if (button == null && option != null)
            {
                button = option.GetComponent<Button>();
            }
        }
    }
}

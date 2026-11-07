using ProjectArtOfAll.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectArtOfAll.UI
{
    /// <summary>Serializable drag bridge for one row/card in the combat lab.</summary>
    public sealed class CombatLabDragHandler : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [SerializeField] private CombatLabView view;
        private CardDefinition card;
        private bool fromLibrary;
        private bool fromLeft;
        private int index;

        public void Bind(
            CombatLabView owner,
            CardDefinition definition,
            bool library,
            bool left,
            int sourceIndex)
        {
            view = owner;
            card = definition;
            fromLibrary = library;
            fromLeft = left;
            index = sourceIndex;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            view?.BeginDrag(card, fromLibrary, fromLeft, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            view?.DragTo(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            view?.EndDrag(fromLibrary, fromLeft, index, eventData);
        }
    }
}

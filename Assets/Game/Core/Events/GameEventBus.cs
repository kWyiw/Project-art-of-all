using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProjectArtOfAll.Core.Events
{
    /// <summary>
    /// Deterministic synchronous event bus. Dispatch is depth-first, listener order is stable,
    /// and nested event chains are limited to protect against rule recursion.
    /// </summary>
    public sealed class GameEventBus
    {
        public const int MaxDispatchDepth = 8;

        private readonly Dictionary<Type, List<Subscription>> subscriptions =
            new Dictionary<Type, List<Subscription>>();
        private readonly List<GameEvent> history = new List<GameEvent>();
        private long currentTick = -1L;
        private int nextSequence;
        private long nextSubscriptionId;
        private int dispatchDepth;

        public long CurrentTick => currentTick;
        public IReadOnlyList<GameEvent> History =>
            new ReadOnlyCollection<GameEvent>(history);

        public TEvent Publish<TEvent>(long tick, TEvent gameEvent)
            where TEvent : GameEvent
        {
            if (gameEvent == null)
            {
                throw new ArgumentNullException(nameof(gameEvent));
            }

            if (tick < currentTick)
            {
                throw new InvalidOperationException(
                    $"Cannot publish tick {tick} after tick {currentTick}.");
            }

            if (tick > currentTick)
            {
                currentTick = tick;
                nextSequence = 0;
            }

            gameEvent.Tick = tick;
            gameEvent.Sequence = nextSequence++;
            history.Add(gameEvent);

            if (!subscriptions.TryGetValue(gameEvent.GetType(), out List<Subscription> handlers))
            {
                return gameEvent;
            }

            if (dispatchDepth >= MaxDispatchDepth)
            {
                throw new InvalidOperationException(
                    $"Event dispatch exceeded depth {MaxDispatchDepth}.");
            }

            dispatchDepth++;
            try
            {
                Subscription[] snapshot = handlers
                    .OrderBy(handler => handler.Order)
                    .ThenBy(handler => handler.Id)
                    .ToArray();

                foreach (Subscription subscription in snapshot)
                {
                    subscription.Dispatch(gameEvent);
                }
            }
            finally
            {
                dispatchDepth--;
            }

            return gameEvent;
        }

        public IDisposable Subscribe<TEvent>(
            int order,
            Action<TEvent> handler)
            where TEvent : GameEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type eventType = typeof(TEvent);
            if (!subscriptions.TryGetValue(eventType, out List<Subscription> eventSubscriptions))
            {
                eventSubscriptions = new List<Subscription>();
                subscriptions.Add(eventType, eventSubscriptions);
            }

            long subscriptionId = nextSubscriptionId++;
            Subscription subscription = new Subscription(
                subscriptionId,
                order,
                gameEvent => handler((TEvent)gameEvent));
            eventSubscriptions.Add(subscription);

            return new SubscriptionHandle(
                this,
                eventType,
                subscriptionId);
        }

        public void Reset()
        {
            history.Clear();
            currentTick = -1L;
            nextSequence = 0;
            dispatchDepth = 0;
        }

        private void Unsubscribe(Type eventType, long subscriptionId)
        {
            if (!subscriptions.TryGetValue(eventType, out List<Subscription> eventSubscriptions))
            {
                return;
            }

            eventSubscriptions.RemoveAll(
                subscription => subscription.Id == subscriptionId);
        }

        private sealed class Subscription
        {
            private readonly Action<GameEvent> handler;

            public Subscription(
                long id,
                int order,
                Action<GameEvent> handler)
            {
                Id = id;
                Order = order;
                this.handler = handler;
            }

            public long Id { get; }
            public int Order { get; }

            public void Dispatch(GameEvent gameEvent)
            {
                handler(gameEvent);
            }
        }

        private sealed class SubscriptionHandle : IDisposable
        {
            private readonly GameEventBus owner;
            private readonly Type eventType;
            private readonly long subscriptionId;
            private bool disposed;

            public SubscriptionHandle(
                GameEventBus owner,
                Type eventType,
                long subscriptionId)
            {
                this.owner = owner;
                this.eventType = eventType;
                this.subscriptionId = subscriptionId;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                owner.Unsubscribe(eventType, subscriptionId);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LoginAppFramework
{
    public static class NotificationService
    {
        private const int MaxVisiblePerOwner = 3;

        private static readonly Dictionary<Window, ToastQueueState> States = new();

        public static void Success(
            Window owner,
            string message,
            int milliseconds = 2600,
            string title = null)
            => Enqueue(owner, message, ToastType.Success, milliseconds, title);

        public static void Error(
            Window owner,
            string message,
            int milliseconds = 3600,
            string title = null)
            => Enqueue(owner, message, ToastType.Error, milliseconds, title);

        public static void Warning(
            Window owner,
            string message,
            int milliseconds = 3400,
            string title = null)
            => Enqueue(owner, message, ToastType.Warning, milliseconds, title);

        public static void Info(
            Window owner,
            string message,
            int milliseconds = 3000,
            string title = null)
            => Enqueue(owner, message, ToastType.Info, milliseconds, title);

        private static void Enqueue(
            Window owner,
            string message,
            ToastType type,
            int milliseconds,
            string title)
        {
            Window resolvedOwner =
                owner ??
                Application.Current?.Windows
                    .OfType<Window>()
                    .FirstOrDefault(window => window.IsActive) ??
                Application.Current?.MainWindow;

            if (resolvedOwner == null)
            {
                var standalone = CreateToast(
                    null,
                    new ToastRequest(message, type, milliseconds, title));
                standalone.Start();
                return;
            }

            if (!States.TryGetValue(resolvedOwner, out var state))
            {
                state = new ToastQueueState();
                States[resolvedOwner] = state;

                resolvedOwner.Closed += (_, _) =>
                {
                    if (!States.TryGetValue(resolvedOwner, out var ownerState))
                        return;

                    States.Remove(resolvedOwner);
                    ownerState.Pending.Clear();

                    foreach (var toast in ownerState.Visible.ToList())
                    {
                        if (toast.IsVisible)
                            toast.Close();
                    }
                };
            }

            state.Pending.Enqueue(
                new ToastRequest(message, type, milliseconds, title));

            PumpQueue(resolvedOwner, state);
        }

        private static void PumpQueue(
            Window owner,
            ToastQueueState state)
        {
            while (state.Visible.Count < MaxVisiblePerOwner &&
                   state.Pending.Count > 0)
            {
                var request = state.Pending.Dequeue();
                var toast = CreateToast(owner, request);

                toast.ToastClosed += (_, _) =>
                {
                    state.Visible.Remove(toast);
                    Reposition(state);

                    if (States.ContainsKey(owner))
                        PumpQueue(owner, state);
                };

                state.Visible.Add(toast);
                Reposition(state);
                toast.Start();
            }
        }

        private static ToastNotificationWindow CreateToast(
            Window owner,
            ToastRequest request)
            => new(
                owner,
                request.Message,
                request.Type,
                TimeSpan.FromMilliseconds(request.Milliseconds),
                request.Title);

        private static void Reposition(ToastQueueState state)
        {
            for (int i = 0; i < state.Visible.Count; i++)
                state.Visible[i].SetStackIndex(i);
        }

        private sealed class ToastQueueState
        {
            public Queue<ToastRequest> Pending { get; } = new();
            public List<ToastNotificationWindow> Visible { get; } = new();
        }

        private sealed record ToastRequest(
            string Message,
            ToastType Type,
            int Milliseconds,
            string Title);
    }
}

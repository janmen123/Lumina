using System;
namespace Lumina {
    // ShowWindowAsync only queues a restore. Foreground ownership can change
    // before the window leaves its minimized state, especially on rapid clicks.
    internal static class WindowActivationAttempt {
        internal static bool Run(Func<bool> minimized, Func<bool> foreground,
            Action restore, Action focus, Action fallback) {
            if(minimized())restore();
            focus();
            if(foreground()&&!minimized())return true;
            fallback();
            return foreground()&&!minimized();
        }
    }
}

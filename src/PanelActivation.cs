using System;
using System.Windows;

namespace Lumina {
    public partial class Controller {
        // An explicit launch is different from an automatic Dock reveal. It may show the
        // panel during game mode without changing the user's stored game-mode preference.
        public void RevealPanel() {
            if (ShuttingDown) return;
            Board.Open();
        }
    }
}

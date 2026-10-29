Upstream: https://github.com/Widdit/now-playing-service
Pinned commit: 2336c5fc6c57300bc7cf5fa85c7f273042bb86b1
MIT license: LICENSE in this directory.

v0.8.6 directly compiles upstream Helpers/NeteaseMusicHelper.cs, using its
STA worker and IUIAutomation COM implementation. The unmodified source is
NeteaseMusicHelper.upstream.cs.txt; patched source is NeteaseMusicHelper.cs
and its build copy is src/NeteaseMusicHelper.cs. Interop.UIAutomationClient.dll
is taken from the same commit's GetMusicStatus/lib directory and included
beside GlassBoard.exe.

Local patches: C#5 compatibility; match window title to the requested track;
restart a failed worker on a subsequent request after five seconds; retain measured progress for up to four seconds while playing between worker
ticks, and while paused; expose last worker error. The adapter resets vendor
track generation when the full system source/title/artist/album key changes.
These patches do not synthesize elapsed time or add seeking to upstream UIA.
When no writable range is exposed, the timeline is read-only.

No full Java server or web widget is bundled. Existing GlassBoard cover and
transport control bridges remain active. The local COM progress worker works
without a network connection but depends on the player exposing its UI text.

v0.8.7: music view no longer invokes the window timeline worker. Vendor code is retained for build/history compatibility, not requested during normal music polling.


v0.9.23: COM activation uses CLSIDs directly; the build embeds required COM interfaces
from verification/build-refs/Interop.UIAutomationClient.dll using the compiler link option.
The external Interop assembly is no longer a runtime dependency. The tray implementation
is independent of this retained music adapter.
